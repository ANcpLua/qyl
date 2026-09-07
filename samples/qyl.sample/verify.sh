#!/usr/bin/env bash
# Proves the qyl.sdk template extension end to end:
#   build + generator tests → every compile-time generator produced its output → committed OpenAPI contract is current →
#   managed app passes the HTTP scenario → Native AOT publish → native binary passes the same scenario and serves the same contract →
#   (when Docker is available) the container image builds with Native AOT and passes the scenario too →
#   qyl.sdk packs as the Qyl.Api.Sdk MSBuild SDK and a consumer using <Project Sdk="Qyl.Api.Sdk/version"> builds the identical contract.
set -euo pipefail

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
solution="$root_dir/qyl.sample.slnx"
project="$root_dir/qyl.sample/qyl.sample.csproj"
contract="$root_dir/qyl.sample/openapi/qyl.sample.json"
generator_tests="$root_dir/qyl.sdk/Qyl.Sdk.Xml.Generator.Tests/Qyl.Sdk.Xml.Generator.Tests.csproj"
scratch_dir="$(mktemp -d /tmp/qyl-verify.XXXXXX)"
api_pid=""

if [[ -n "${RUNTIME_ID:-}" ]]; then
    runtime_id="$RUNTIME_ID"
else
    case "$(uname -s)-$(uname -m)" in
        Darwin-arm64) runtime_id="osx-arm64" ;;
        Darwin-x86_64) runtime_id="osx-x64" ;;
        Linux-aarch64 | Linux-arm64) runtime_id="linux-arm64" ;;
        Linux-x86_64) runtime_id="linux-x64" ;;
        *) echo "Set RUNTIME_ID to a Native AOT runtime identifier for this platform." >&2; exit 1 ;;
    esac
fi

for tool in curl jq xmllint; do
    command -v "$tool" >/dev/null || { echo "$tool is required." >&2; exit 1; }
done

stop_api() {
    if [[ -n "$api_pid" ]] && kill -0 "$api_pid" 2>/dev/null; then
        kill "$api_pid" 2>/dev/null || true
        wait "$api_pid" 2>/dev/null || true
    fi
    api_pid=""
}

completed=0
cleanup() {
    local status=$?
    # A bash-level error (set -u, set -e inside a function) can reach the trap with status 0; the flag closes that gap.
    if [[ "$status" -eq 0 && "$completed" -ne 1 ]]; then
        status=1
    fi
    stop_api
    if [[ "${KEEP_ARTIFACTS:-0}" == "1" ]]; then
        printf 'Retained artifacts: %s\n' "$scratch_dir"
    elif [[ "$scratch_dir" == /tmp/qyl-verify.* && -d "$scratch_dir" ]]; then
        rm -rf -- "$scratch_dir"
    fi
    exit "$status"
}
trap cleanup EXIT

fail() { echo "FAIL $*" >&2; exit 1; }
pass() { printf 'PASS %-44s -> %s\n' "$1" "$2"; }

# start_api <executable> <label>: starts the API on a free loopback port and sets base_url.
start_api() {
    local executable="$1" label="$2"
    local api_log="$scratch_dir/$label-api.log"
    # Development: the template maps /openapi/v1.json in Development only, and the scenario compares that document.
    ASPNETCORE_ENVIRONMENT=Development "$executable" --urls http://127.0.0.1:0 >"$api_log" 2>&1 &
    api_pid=$!
    for _ in $(seq 1 200); do
        base_url="$(sed -n 's/.*Now listening on: \(http:\/\/[^ ]*\).*/\1/p' "$api_log" | head -1)"
        [[ -n "$base_url" ]] && return
        kill -0 "$api_pid" 2>/dev/null || { cat "$api_log" >&2; fail "the $label API exited before it started listening"; }
        sleep 0.1
    done
    cat "$api_log" >&2
    fail "the $label API did not report a listening address"
}

# run_scenario <label>: the HTTP proof, identical for the managed and the native host.
run_scenario() {
    local label="$1" headers body status

    headers="$(curl -s -D - -o "$scratch_dir/$label-400.json" -X POST "$base_url/todos/" -H 'content-type: application/json' -d '{"title":"no"}')"
    grep -q "^HTTP/1.1 400" <<<"$headers" || fail "$label: invalid POST did not return 400"
    grep -qi "^content-type: application/problem+json" <<<"$headers" || fail "$label: validation problem is not application/problem+json"
    [[ "$(jq -r '.errors.Title | length' "$scratch_dir/$label-400.json")" == "1" ]] || fail "$label: problem details lack errors.Title"
    pass "generated DataAnnotations validation" "400 application/problem+json"

    headers="$(curl -s -D - -o "$scratch_dir/$label-201.json" -X POST "$base_url/todos/" -H 'content-type: application/json' -d '{"title":"Read obj/generated","dueBy":"2026-09-05"}')"
    grep -q "^HTTP/1.1 201" <<<"$headers" || fail "$label: valid POST did not return 201"
    local id; id="$(jq -r '.id' "$scratch_dir/$label-201.json")"
    grep -qi "^location: .*/todos/$id" <<<"$headers" || fail "$label: 201 lacks a Location pointing at /todos/$id"
    pass "validated create" "201 + Location: /todos/$id"

    body="$(curl -s "$base_url/todos/$id")"
    [[ "$(jq -r '.title' <<<"$body")" == "Read obj/generated" ]] || fail "$label: JSON read-back mismatch"
    pass "JSON round-trip (source-generated context)" "200 Todo"

    headers="$(curl -s -D - -o "$scratch_dir/$label.xml" "$base_url/todos/$id/xml")"
    grep -qi "^content-type: application/xml" <<<"$headers" || fail "$label: XML endpoint is not application/xml"
    [[ "$(xmllint --xpath 'string(/todo/@id)' "$scratch_dir/$label.xml")" == "$id" ]] || fail "$label: XML id attribute mismatch"
    [[ "$(xmllint --xpath 'string(/todo/due-by)' "$scratch_dir/$label.xml")" == "2026-09-05" ]] || fail "$label: XML due-by mismatch"
    pass "generated XmlWriter document" "200 application/xml"

    status="$(curl -s -o /dev/null -w '%{http_code}' "$base_url/todos/999")"
    [[ "$status" == "404" ]] || fail "$label: unknown todo returned $status"
    pass "unknown todo" "404"

    curl -s "$base_url/openapi/v1.json" | jq -S 'del(.servers)' >"$scratch_dir/$label-openapi.json"
    jq -S 'del(.servers)' "$contract" >"$scratch_dir/contract.json"
    diff -q "$scratch_dir/contract.json" "$scratch_dir/$label-openapi.json" >/dev/null || {
        diff "$scratch_dir/contract.json" "$scratch_dir/$label-openapi.json" >&2 || true
        fail "$label: runtime OpenAPI document differs from the committed contract"
    }
    [[ "$(jq -r '.paths["/todos"].post.summary' "$scratch_dir/$label-openapi.json")" == "Creates a todo." ]] || fail "$label: XML-comment summary missing"
    [[ "$(jq -r '.paths["/todos"].post.responses["400"].content | keys[0]' "$scratch_dir/$label-openapi.json")" == "application/problem+json" ]] || fail "$label: 400 metadata mismatch"
    [[ "$(jq -r '.paths["/todos/{id}/xml"].get.responses["200"].content | keys[0]' "$scratch_dir/$label-openapi.json")" == "application/xml" ]] || fail "$label: application/xml metadata missing"
    [[ "$(jq -r '.paths["/todos/{id}/xml"].get.responses["200"].content["application/xml"].schema["$ref"]' "$scratch_dir/$label-openapi.json")" == "#/components/schemas/TodoXml" ]] || fail "$label: application/xml is not described by the generated TodoXml schema"
    [[ "$(jq -r '.components.schemas.TodoXml.xml.name' "$scratch_dir/$label-openapi.json")" == "todo" ]] || fail "$label: TodoXml lacks the document element name"
    [[ "$(jq -r '.components.schemas.TodoXml.properties.id.xml.attribute' "$scratch_dir/$label-openapi.json")" == "true" ]] || fail "$label: TodoXml does not mark id as an attribute"
    [[ "$(jq -r '.components.schemas.TodoXml.properties["due-by"].format' "$scratch_dir/$label-openapi.json")" == "date" ]] || fail "$label: TodoXml due-by is not a date"
    pass "OpenAPI = committed contract" "XML comments, 400 problem, application/xml as TodoXml"
}

# require_generated <description> <marker> <pattern> [project]: scoped to one project (default: the sample), because the generator
# tests reference Qyl.Api and the OpenAPI generators run there too, with nothing to record.
require_generated() {
    local description="$1" marker="$2" pattern="$3" project="${4:-qyl.sample}"
    local file; file="$(find "$scratch_dir/managed/obj/$project" -type f -name "$pattern" -print -quit)"
    [[ -n "$file" && -f "$file" ]] || fail "generated $description is missing ($pattern)"
    grep -Fq "$marker" "$file" || fail "generated $description does not contain '$marker': $file"
}

cd "$root_dir"

echo "[1/8] Build the solution into a scratch artifacts directory and run the generator tests"
[[ -f "$contract" ]] || fail "committed contract $contract is missing: run dotnet build once and commit the result"
cp "$contract" "$scratch_dir/contract-before-build.json"
dotnet build "$solution" --configuration Release --artifacts-path "$scratch_dir/managed" --disable-build-servers --no-incremental -p:UseSharedCompilation=false
# The generator's own proof: XmlSerializer parity, pinned snapshots, diagnostics, and incremental caching.
dotnet test "$generator_tests" --configuration Release --no-build -p:ArtifactsPath="$scratch_dir/managed"
pass "generator tests (XmlSerializer parity, snapshots, caching)" "dotnet test"

echo "[2/8] Confirm the committed OpenAPI contract is what the build produced"
# Compared against a copy taken before the build (not git diff), so the check also holds for files not yet committed.
diff -u "$scratch_dir/contract-before-build.json" "$contract" >&2 || fail "qyl.sample/openapi/qyl.sample.json changed during the build: commit the result"

echo "[3/8] Confirm every compile-time generator ran"
require_generated "validation resolver"     "CreateTodoRequest"              "ValidatableInfoResolver.g.cs"
require_generated "request delegates"       "MapPost"                        "GeneratedRouteBuilderExtensions.g.cs"
require_generated "OpenAPI comment cache"   "Creates a todo."                "OpenApiXmlCommentSupport.generated.cs"
require_generated "XML writer"              'WriteXml(writer, "todo", null)' "Qyl_Sample_Todo.GenerateXml.g.cs"
require_generated "JSON context"            "CreateTodoRequest"              "AppJsonSerializerContext.CreateTodoRequest.g.cs"
require_generated "problem JSON context"    "HttpValidationProblemDetails"   "QylProblemJsonContext.HttpValidationProblemDetails.g.cs" Qyl.Api
require_generated "public Program"          "public partial class Program"   "PublicTopLevelProgram.Generated.g.cs"
echo "  All generators produced their output."

echo "[4/8] Run the HTTP scenario against the managed build"
managed_api="$(find "$scratch_dir/managed/bin" -type f -path '*qyl.sample*' -name 'qyl.sample' -perm -111 -print -quit)"
[[ -x "${managed_api:-}" ]] || fail "managed app host not found"
start_api "$managed_api" managed
run_scenario managed
stop_api

echo "[5/8] Publish with Native AOT for $runtime_id"
dotnet publish "$project" --configuration Release --runtime "$runtime_id" --self-contained true --artifacts-path "$scratch_dir/native" --disable-build-servers -p:UseSharedCompilation=false
native_api="$(find "$scratch_dir/native" -type f -path '*/publish/*' -name 'qyl.sample' -perm -111 -print -quit)"
[[ -x "${native_api:-}" ]] || fail "native executable not found"
description="$(file "$native_api")"
printf '  %s\n' "$description"
case "$runtime_id" in
    osx-*) [[ "$description" == *"Mach-O"* ]] || fail "not a Mach-O executable" ;;
    linux-*) [[ "$description" == *"ELF"* ]] || fail "not an ELF executable" ;;
esac
if find "$(dirname -- "$native_api")" -maxdepth 1 -type f \( -name '*.dll' -o -name '*.deps.json' -o -name '*.runtimeconfig.json' \) -print -quit | grep -q .; then
    fail "managed deployment files were found beside the native executable"
fi

echo "[6/8] Run the same scenario against the native executable"
start_api "$native_api" native
run_scenario native
stop_api

echo "[7/8] Build the container image (Native AOT inside the SDK image) and run the scenario against it"
if [[ "${SKIP_DOCKER:-0}" == "1" ]]; then
    echo "  skipped: SKIP_DOCKER=1"
elif ! command -v docker >/dev/null || ! docker version >/dev/null 2>&1; then
    echo "  skipped: Docker is not available (install/start Docker or set SKIP_DOCKER=1 to silence this)"
else
    image_tag="qyl.sample:verify-$$"
    docker build --quiet --tag "$image_tag" --file qyl.sample/Dockerfile . >/dev/null
    container_id="$(docker run --detach --publish 127.0.0.1:0:8080 --env ASPNETCORE_ENVIRONMENT=Development "$image_tag")"
    stop_container() { docker rm --force "$container_id" >/dev/null 2>&1 || true; docker rmi --force "$image_tag" >/dev/null 2>&1 || true; }
    container_port="$(docker port "$container_id" 8080/tcp | head -1 | sed 's/.*://')"
    base_url="http://127.0.0.1:$container_port"
    for _ in $(seq 1 100); do
        curl -s -o /dev/null "$base_url/todos/" && break
        sleep 0.1
    done
    if ! curl -s -o /dev/null "$base_url/todos/"; then
        docker logs "$container_id" >&2 || true
        stop_container
        fail "the container did not start serving"
    fi
    if run_scenario container; then
        stop_container
    else
        stop_container
        exit 1
    fi
fi

echo "[8/8] Pack qyl.sdk as the Qyl.Api.Sdk MSBuild SDK and build a consumer from the package"
sdk_version="$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' "$root_dir/qyl.sdk/Qyl.Api.Sdk.csproj")"
[[ -n "$sdk_version" ]] || fail "could not read the SDK version from qyl.sdk/Qyl.Api.Sdk.csproj"
feed="$scratch_dir/feed"
dotnet pack "$root_dir/qyl.sdk/Qyl.Api.Sdk.csproj" --configuration Release --output "$feed" --disable-build-servers -p:UseSharedCompilation=false
[[ -f "$feed/Qyl.Api.Sdk.$sdk_version.nupkg" ]] || fail "Qyl.Api.Sdk.$sdk_version.nupkg was not produced"
# The same sources as the sample, but the project reads <Project Sdk="Qyl.Api.Sdk/version">: no imports, no generator reference, no CPM.
consumer="$scratch_dir/consumer/qyl.sample"
mkdir -p "$consumer"
cp -R "$root_dir/qyl.sample/Program.cs" "$root_dir/qyl.sample/AppJsonSerializerContext.cs" "$root_dir/qyl.sample/Todos" "$root_dir/qyl.sample/appsettings.json" "$consumer/"
cat >"$consumer/qyl.sample.csproj" <<PROJECT
<Project Sdk="Qyl.Api.Sdk/$sdk_version">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <RootNamespace>Qyl.Sample</RootNamespace>
    </PropertyGroup>
</Project>
PROJECT
cat >"$consumer/nuget.config" <<NUGET
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="qyl-local" value="$feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
NUGET
# The SDK resolver caches by id and version in the global packages folder; drop this version so the fresh package is used.
rm -rf "${NUGET_PACKAGES:-$HOME/.nuget/packages}/qyl.api.sdk/$sdk_version"
dotnet build "$consumer/qyl.sample.csproj" --configuration Release --disable-build-servers -p:UseSharedCompilation=false
[[ -f "$consumer/obj/generated/Qyl.Sdk.Xml.Generator/Qyl.Sdk.Xml.Generator.XmlWriterGenerator/Qyl_Sample_Todo.GenerateXml.g.cs" ]] || fail "packaged generator did not run in the consumer"
find "$consumer/bin" -name 'Qyl.Xml.dll' -print -quit | grep -q . || fail "the packaged Qyl.Xml assembly was not referenced by the consumer"
find "$consumer/bin" -name 'Qyl.Api.dll' -print -quit | grep -q . || fail "the packaged Qyl.Api assembly was not referenced by the consumer"
jq -S 'del(.servers)' "$consumer/openapi/qyl.sample.json" >"$scratch_dir/consumer-openapi.json"
diff -q "$scratch_dir/contract.json" "$scratch_dir/consumer-openapi.json" >/dev/null || {
    diff "$scratch_dir/contract.json" "$scratch_dir/consumer-openapi.json" >&2 || true
    fail "the consumer built from the Qyl.Api.Sdk package produced a different contract"
}
pass "Qyl.Api.Sdk $sdk_version package" "<Project Sdk=\"Qyl.Api.Sdk/$sdk_version\"> builds the identical contract"

completed=1
echo "All generator, validation, XML, OpenAPI, Native AOT, container, and package checks passed."
