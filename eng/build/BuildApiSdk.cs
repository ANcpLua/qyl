using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Xml.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Serilog;

namespace Qyl.Build;

/// <summary>
/// The proof that Qyl.Api.Sdk builds the API it claims to: the sample is the API, and every
/// check the fused repository ran as verify.sh runs here instead. Eight stages, same order —
/// build and generator tests, committed contract unchanged, every compile-time generator ran,
/// the HTTP scenario on the managed host, a Native AOT publish with no managed files beside the
/// binary, the same scenario on the native host, the container image, and a consumer built from
/// the packed SDK producing a byte-identical contract.
///
/// The scenario is written against the .NET HTTP and XML stacks rather than curl/jq/xmllint:
/// the gate must run wherever the build runs, and three shell tools that have to be installed
/// first are three ways for it to be skipped instead of failed.
/// </summary>
[ParameterPrefix(nameof(IApiSdk))]
interface IApiSdk : IHazSourcePaths, IHazConfiguration
{
    [Parameter("Skip the Qyl.Api.Sdk container stage (it needs a running Docker or OrbStack engine)")]
    bool? SkipContainer => TryGetValue<bool?>(() => SkipContainer);

    [Parameter("Runtime identifier for the Qyl.Api.Sdk Native AOT stage (default: the host)")]
    string? NativeRuntime => TryGetValue(() => NativeRuntime);

    AbsolutePath ApiSdkDirectory => PackagesDirectory / "Qyl.Api.Sdk";

    AbsolutePath ApiSdkProject => ApiSdkDirectory / "Qyl.Api.Sdk.csproj";

    AbsolutePath SampleDirectory => RootDirectory / "samples" / "qyl.sample";

    AbsolutePath SampleProject => SampleDirectory / "qyl.sample.csproj";

    AbsolutePath SampleDockerfile => SampleDirectory / "Dockerfile";

    AbsolutePath SampleContract => SampleDirectory / "openapi" / "qyl.sample.json";

    AbsolutePath GeneratorTestsProject =>
        RootDirectory / "tests" / "Qyl.Sdk.Xml.Generator.Tests" / "Qyl.Sdk.Xml.Generator.Tests.csproj";

    AbsolutePath ApiSdkArtifactsDirectory => ArtifactsDirectory / "api-sdk";

    AbsolutePath ApiSdkNativeDirectory => ApiSdkArtifactsDirectory / "native";

    AbsolutePath ApiSdkFeedDirectory => ApiSdkArtifactsDirectory / "feed";

    AbsolutePath ApiSdkConsumerDirectory => ApiSdkArtifactsDirectory / "consumer";

    AbsolutePath SampleIntermediateDirectory => RootDirectory / "artifacts" / "obj" / "qyl.sample";

    AbsolutePath ApiIntermediateDirectory => RootDirectory / "artifacts" / "obj" / "Qyl.Api";

    Target ApiSdk => d => d
        .Description("Run every Qyl.Api.Sdk gate: build, contract, generators, managed, native, container, package")
        .DependsOn(ApiSdkBuildAndTest)
        .DependsOn(ApiSdkContractIsCommitted)
        .DependsOn(ApiSdkGeneratorsRan)
        .DependsOn(ApiSdkManagedScenario)
        .DependsOn(ApiSdkNativePublish)
        .DependsOn(ApiSdkNativeScenario)
        .DependsOn(ApiSdkContainerScenario)
        .DependsOn(ApiSdkPackagedConsumer)
        .Executes(() => Log.Information("Qyl.Api.Sdk: eight stages green"));

    /// <summary>Stage 1: the SDK, the sample and the generator's own proof build and pass.</summary>
    Target ApiSdkBuildAndTest => d => d
        .Unlisted()
        .Executes(() =>
        {
            ApiSdkArtifactsDirectory.CreateOrCleanDirectory();

            DotNetTasks.DotNetBuild(s => s
                .SetProjectFile(SampleProject)
                .SetConfiguration(Configuration));

            DotNetTasks.DotNetTest(s => s
                .SetProjectFile(GeneratorTestsProject)
                .SetConfiguration(Configuration));

            Log.Information("Qyl.Api.Sdk: sample builds, generator tests pass");
        });

    /// <summary>
    /// Stage 2: the committed OpenAPI document is what the build produces. Compared against a copy
    /// taken before the build rather than against git, so it also holds for an uncommitted file.
    /// </summary>
    Target ApiSdkContractIsCommitted => d => d
        .Unlisted()
        .Executes(() =>
        {
            if (!SampleContract.FileExists())
                throw new FileNotFoundException("The committed OpenAPI contract is missing", SampleContract);

            var before = SampleContract.ReadAllText();

            DotNetTasks.DotNetBuild(s => s
                .SetProjectFile(SampleProject)
                .SetConfiguration(Configuration));

            var after = SampleContract.ReadAllText();
            if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{RootDirectory.GetRelativePathTo(SampleContract)} changed during the build. " +
                    "The document is a committed build artifact: commit the result.");
            }

            Log.Information("Qyl.Api.Sdk: committed contract equals the built contract");
        });

    /// <summary>
    /// Stage 3: every compile-time generator the SDK turns on actually ran. Scoped per project,
    /// because Qyl.Api runs the OpenAPI generators too and has nothing of its own to record.
    /// </summary>
    Target ApiSdkGeneratorsRan => d => d
        .Unlisted()
        .DependsOn(ApiSdkBuildAndTest)
        .Executes(() =>
        {
            (AbsolutePath Root, string Description, string FileName, string Marker)[] expectations =
            [
                (SampleIntermediateDirectory, "validation resolver", "ValidatableInfoResolver.g.cs", "CreateTodoRequest"),
                (SampleIntermediateDirectory, "request delegates", "GeneratedRouteBuilderExtensions.g.cs", "MapPost"),
                (SampleIntermediateDirectory, "OpenAPI comment cache", "OpenApiXmlCommentSupport.generated.cs", "Creates a todo."),
                (SampleIntermediateDirectory, "XML writer", "Qyl_Sample_Todo.GenerateXml.g.cs", "WriteXml(writer, \"todo\", null)"),
                (SampleIntermediateDirectory, "JSON context", "AppJsonSerializerContext.CreateTodoRequest.g.cs", "CreateTodoRequest"),
                (SampleIntermediateDirectory, "public Program", "PublicTopLevelProgram.Generated.g.cs", "public partial class Program"),
                (ApiIntermediateDirectory, "problem JSON context", "QylProblemJsonContext.HttpValidationProblemDetails.g.cs", "HttpValidationProblemDetails"),
            ];

            var offenders = new List<string>();
            foreach (var (root, description, fileName, marker) in expectations)
            {
                var file = root.GlobFiles($"**/{fileName}").FirstOrDefault();
                if (file is null)
                {
                    offenders.Add($"generated {description} is missing ({fileName} under {RootDirectory.GetRelativePathTo(root)})");
                    continue;
                }

                if (!file.ReadAllText().Contains(marker, StringComparison.Ordinal))
                    offenders.Add($"generated {description} does not contain '{marker}': {file}");
            }

            if (offenders.Count > 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, offenders));

            Log.Information("Qyl.Api.Sdk: all {Count} compile-time generators produced their output", expectations.Length);
        });

    /// <summary>Stage 4: the HTTP scenario against the managed host.</summary>
    Target ApiSdkManagedScenario => d => d
        .Unlisted()
        .DependsOn(ApiSdkBuildAndTest)
        .Executes(() =>
        {
            var host = (RootDirectory / "artifacts" / "bin" / "qyl.sample")
                .GlobFiles($"**/{Configuration}/**/qyl.sample", $"**/{Configuration}/**/qyl.sample.exe")
                .FirstOrDefault(static file => !file.ToString().Contains("/publish/", StringComparison.Ordinal));

            if (host is null)
                throw new FileNotFoundException("The managed sample host was not found under artifacts/bin/qyl.sample");

            ApiSdkScenario.Run(host, "managed", SampleContract);
        });

    /// <summary>
    /// Stage 5: a Native AOT publish that really is native — the executable is a Mach-O or ELF
    /// image and no managed deployment file sits beside it.
    /// </summary>
    Target ApiSdkNativePublish => d => d
        .Unlisted()
        .Executes(() =>
        {
            var runtime = NativeRuntime ?? RuntimeInformation.RuntimeIdentifier;
            ApiSdkNativeDirectory.CreateOrCleanDirectory();

            DotNetTasks.DotNetPublish(s => s
                .SetProject(SampleProject)
                .SetConfiguration(Configuration)
                .SetRuntime(runtime)
                .EnableSelfContained()
                .SetOutput(ApiSdkNativeDirectory));

            var executable = ApiSdkNativeDirectory / "qyl.sample";
            if (!executable.FileExists())
                executable = ApiSdkNativeDirectory / "qyl.sample.exe";
            if (!executable.FileExists())
                throw new FileNotFoundException("The Native AOT publish produced no qyl.sample executable", executable);

            ApiSdkScenario.AssertNativeImage(executable);

            var managed = ApiSdkNativeDirectory
                .GlobFiles("*.dll", "*.deps.json", "*.runtimeconfig.json")
                .Select(static file => file.Name)
                .ToList();
            if (managed.Count > 0)
            {
                throw new InvalidOperationException(
                    "Managed deployment files sit beside the native executable: " + string.Join(", ", managed));
            }

            Log.Information("Qyl.Api.Sdk: {Runtime} native publish carries only the executable", runtime);
        });

    /// <summary>Stage 6: the same scenario against the native host.</summary>
    Target ApiSdkNativeScenario => d => d
        .Unlisted()
        .DependsOn(ApiSdkNativePublish)
        .Executes(() =>
        {
            var executable = ApiSdkNativeDirectory / "qyl.sample";
            if (!executable.FileExists())
                executable = ApiSdkNativeDirectory / "qyl.sample.exe";

            ApiSdkScenario.Run(executable, "native", SampleContract);
        });

    /// <summary>
    /// Stage 7: the container image, built Native AOT inside the SDK image and served from a
    /// runtime-deps image that carries no .NET runtime at all.
    /// </summary>
    Target ApiSdkContainerScenario => d => d
        .Unlisted()
        .Executes(() =>
        {
            if (SkipContainer == true)
            {
                Log.Warning("Qyl.Api.Sdk: container stage skipped by --apisdk-skip-container");
                return;
            }

            if (!ApiSdkScenario.DockerEngineIsRunning())
            {
                throw new InvalidOperationException(
                    "The Qyl.Api.Sdk container stage needs a running Docker or OrbStack engine. " +
                    "Start it, or pass --apisdk-skip-container to record the stage as skipped.");
            }

            var tag = $"qyl.sample:verify-{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}";
            var container = string.Empty;
            try
            {
                ApiSdkScenario.Docker($"build --quiet --tag {tag} --file \"{SampleDockerfile}\" \"{RootDirectory}\"", RootDirectory);
                var port = ApiSdkScenario.FreeLoopbackPort();
                container = ApiSdkScenario
                    .Docker(
                        $"run --detach --publish 127.0.0.1:{port}:8080 --env ASPNETCORE_ENVIRONMENT=Development {tag}",
                        RootDirectory)
                    .Trim();

                ApiSdkScenario.RunAgainst(new Uri($"http://127.0.0.1:{port}"), "container", SampleContract, () => { });
            }
            finally
            {
                if (container.Length > 0)
                    ApiSdkScenario.DockerQuiet($"rm --force {container}");
                ApiSdkScenario.DockerQuiet($"rmi --force {tag}");
            }
        });

    /// <summary>
    /// Stage 8: a consumer that reaches the SDK only through the package — no imports, no generator
    /// reference, no central package management — builds the identical contract from the same sources.
    /// </summary>
    Target ApiSdkPackagedConsumer => d => d
        .Unlisted()
        .Executes(() =>
        {
            var version = SampleVersion();
            ApiSdkFeedDirectory.CreateOrCleanDirectory();
            ApiSdkConsumerDirectory.CreateOrCleanDirectory();

            DotNetTasks.DotNetPack(s => s
                .SetProject(ApiSdkProject)
                .SetConfiguration(Configuration)
                .SetOutputDirectory(ApiSdkFeedDirectory));

            var package = ApiSdkFeedDirectory / $"Qyl.Api.Sdk.{version}.nupkg";
            if (!package.FileExists())
                throw new FileNotFoundException($"Qyl.Api.Sdk.{version}.nupkg was not produced", package);

            var consumer = ApiSdkConsumerDirectory / "qyl.sample";
            consumer.CreateDirectory();
            foreach (var name in new[] { "Program.cs", "AppJsonSerializerContext.cs", "appsettings.json" })
                FileSystemTasks.CopyFile(SampleDirectory / name, consumer / name);
            FileSystemTasks.CopyDirectoryRecursively(SampleDirectory / "Todos", consumer / "Todos");

            (consumer / "qyl.sample.csproj").WriteAllText(
                $"""
                 <Project Sdk="Qyl.Api.Sdk/{version}">
                     <PropertyGroup>
                         <TargetFramework>net10.0</TargetFramework>
                         <ImplicitUsings>enable</ImplicitUsings>
                         <RootNamespace>Qyl.Sample</RootNamespace>
                     </PropertyGroup>
                 </Project>

                 """);

            (consumer / "nuget.config").WriteAllText(
                $"""
                 <?xml version="1.0" encoding="utf-8"?>
                 <configuration>
                   <packageSources>
                     <clear />
                     <add key="qyl-local" value="{ApiSdkFeedDirectory}" />
                     <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                   </packageSources>
                 </configuration>

                 """);

            // The SDK resolver caches by id and version in the global packages folder; drop this
            // version so the package that was just built is the one the consumer resolves.
            var packages = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
                           ?? Path.Combine(
                               Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                               ".nuget",
                               "packages");
            AbsolutePath cached = Path.Combine(packages, "qyl.api.sdk", version);
            if (cached.DirectoryExists())
                cached.DeleteDirectory();

            DotNetTasks.DotNetBuild(s => s
                .SetProjectFile(consumer / "qyl.sample.csproj")
                .SetConfiguration(Configuration)
                .DisableProcessOutputLogging()
                .SetProcessArgumentConfigurator(static a => a.Add("--disable-build-servers")));

            var generated = consumer.GlobFiles("**/Qyl_Sample_Todo.GenerateXml.g.cs").FirstOrDefault();
            if (generated is null)
                throw new InvalidOperationException("The packaged generator did not run in the consumer");

            foreach (var assembly in new[] { "Qyl.Xml.dll", "Qyl.Api.dll" })
            {
                if (consumer.GlobFiles($"bin/**/{assembly}").Count == 0)
                    throw new InvalidOperationException($"The packaged {assembly} was not referenced by the consumer");
            }

            ApiSdkScenario.AssertSameContract(
                SampleContract.ReadAllText(),
                (consumer / "openapi" / "qyl.sample.json").ReadAllText(),
                "the consumer built from the Qyl.Api.Sdk package");

            Log.Information(
                "Qyl.Api.Sdk: <Project Sdk=\"Qyl.Api.Sdk/{Version}\"> builds the identical contract", version);
        });

    private string SampleVersion()
    {
        var versionProps = XDocument.Load(RootDirectory / "Version.props");
        return versionProps.Descendants("QylVersion").Single().Value.Trim();
    }
}

/// <summary>The HTTP proof, identical for the managed host, the native host, and the container.</summary>
internal static class ApiSdkScenario
{
    private static readonly TimeSpan s_startupTimeout = TimeSpan.FromSeconds(60);

    public static void Run(AbsolutePath executable, string label, AbsolutePath contract)
    {
        var port = FreeLoopbackPort();
        var log = new StringBuilder();
        var startInfo = new ProcessStartInfo(executable)
        {
            WorkingDirectory = executable.Parent,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add($"http://127.0.0.1:{port}");
        // The template maps /openapi/v1.json in Development only, and the scenario compares that document.
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException($"Could not start the {label} host");
        process.OutputDataReceived += (_, e) => Append(log, e.Data);
        process.ErrorDataReceived += (_, e) => Append(log, e.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            RunAgainst(new Uri($"http://127.0.0.1:{port}"), label, contract, () =>
            {
                if (process.HasExited)
                    throw new InvalidOperationException($"The {label} host exited before it served:{Environment.NewLine}{log}");
            });
        }
        catch
        {
            Log.Error("{Label} host output:{NewLine}{Output}", label, Environment.NewLine, log.ToString());
            throw;
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
        }
    }

    public static void RunAgainst(Uri baseAddress, string label, AbsolutePath contract, Action assertAlive)
    {
        using var client = new HttpClient { BaseAddress = baseAddress, Timeout = TimeSpan.FromSeconds(30) };
        WaitUntilServing(client, label, assertAlive);

        AssertValidationProblem(client, label);
        var id = AssertCreate(client, label);
        AssertJsonReadBack(client, label, id);
        AssertXmlDocument(client, label, id);
        AssertUnknownIsNotFound(client, label);
        AssertServedContract(client, label, contract);
    }

    public static int FreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public static bool DockerEngineIsRunning()
    {
        try
        {
            var process = ProcessTasks.StartProcess("docker", "version", logOutput: false, logInvocation: false);
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return false;
        }
    }

    public static string Docker(string arguments, AbsolutePath workingDirectory)
    {
        var process = ProcessTasks.StartProcess("docker", arguments, workingDirectory, logOutput: false);
        process.AssertZeroExitCode();
        return string.Join(Environment.NewLine, process.Output.Select(static line => line.Text));
    }

    public static void DockerQuiet(string arguments)
    {
        try
        {
            ProcessTasks.StartProcess("docker", arguments, logOutput: false, logInvocation: false).WaitForExit();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Log.Debug(exception, "docker {Arguments} failed while cleaning up", arguments);
        }
    }

    /// <summary>A native image, not an apphost with a managed payload: the file starts with a Mach-O or ELF magic.</summary>
    public static void AssertNativeImage(AbsolutePath executable)
    {
        Span<byte> magic = stackalloc byte[4];
        using (var stream = File.OpenRead(executable))
        {
            if (stream.Read(magic) != magic.Length)
                throw new InvalidOperationException($"{executable} is too short to be an executable image");
        }

        var value = BitConverter.ToUInt32(magic);
        var isMachO = value is 0xFEEDFACF or 0xCFFAEDFE or 0xFEEDFACE or 0xCEFAEDFE;
        var isElf = magic[0] is 0x7F && magic[1] is (byte)'E' && magic[2] is (byte)'L' && magic[3] is (byte)'F';
        var isPortableExecutable = magic[0] is (byte)'M' && magic[1] is (byte)'Z';

        if (!isMachO && !isElf && !isPortableExecutable)
            throw new InvalidOperationException($"{executable} is not a native executable image");
    }

    public static void AssertSameContract(string committed, string produced, string what)
    {
        var expected = Canonicalize(committed);
        var actual = Canonicalize(produced);
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The OpenAPI document from {what} differs from the committed contract." +
                $"{Environment.NewLine}committed:{Environment.NewLine}{expected}" +
                $"{Environment.NewLine}produced:{Environment.NewLine}{actual}");
        }
    }

    private static void Append(StringBuilder log, string? line)
    {
        if (line is null) return;
        lock (log) log.AppendLine(line);
    }

    private static void WaitUntilServing(HttpClient client, string label, Action assertAlive)
    {
        var deadline = DateTime.UtcNow + s_startupTimeout;
        while (DateTime.UtcNow < deadline)
        {
            assertAlive();
            try
            {
                using var response = client.GetAsync("/todos/").GetAwaiter().GetResult();
                return;
            }
            catch (HttpRequestException)
            {
                Thread.Sleep(100);
            }
        }

        throw new InvalidOperationException($"The {label} host did not start serving within {s_startupTimeout}");
    }

    private static void AssertValidationProblem(HttpClient client, string label)
    {
        using var response = Post(client, "/todos/", """{"title":"no"}""");
        Expect(label, response.StatusCode is HttpStatusCode.BadRequest, "an invalid POST returns 400");
        Expect(label, response.Content.Headers.ContentType?.MediaType is "application/problem+json",
            "the validation problem is application/problem+json");

        var problem = ReadJson(response);
        var titleErrors = problem?["errors"]?["Title"]?.AsArray();
        Expect(label, titleErrors?.Count is 1, "the problem details carry exactly one errors.Title entry");
        Log.Information("  {Label}: generated DataAnnotations validation -> 400 application/problem+json", label);
    }

    private static string AssertCreate(HttpClient client, string label)
    {
        using var response = Post(client, "/todos/", """{"title":"Read obj/generated","dueBy":"2026-09-05"}""");
        Expect(label, response.StatusCode is HttpStatusCode.Created, "a valid POST returns 201");

        var id = ReadJson(response)?["id"]?.GetValue<int>().ToString(CultureInfo.InvariantCulture);
        Expect(label, id is not null, "the created todo carries an id");
        Expect(label, response.Headers.Location?.ToString().EndsWith($"/todos/{id}", StringComparison.Ordinal) is true,
            $"the 201 carries a Location pointing at /todos/{id}");
        Log.Information("  {Label}: validated create -> 201 + Location /todos/{Id}", label, id);
        return id!;
    }

    private static void AssertJsonReadBack(HttpClient client, string label, string id)
    {
        using var response = client.GetAsync($"/todos/{id}").GetAwaiter().GetResult();
        Expect(label, response.StatusCode is HttpStatusCode.OK, "the created todo reads back");
        Expect(label, ReadJson(response)?["title"]?.GetValue<string>() is "Read obj/generated",
            "the JSON read-back returns the stored title");
        Log.Information("  {Label}: JSON round-trip (source-generated context) -> 200 Todo", label);
    }

    private static void AssertXmlDocument(HttpClient client, string label, string id)
    {
        using var response = client.GetAsync($"/todos/{id}/xml").GetAwaiter().GetResult();
        Expect(label, response.Content.Headers.ContentType?.MediaType is "application/xml",
            "the XML endpoint answers application/xml");

        var document = XDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        Expect(label, document.Root?.Attribute("id")?.Value == id, "the XML document carries the id as an attribute");
        Expect(label, document.Root?.Element("due-by")?.Value is "2026-09-05", "the XML document carries due-by as a date");
        Log.Information("  {Label}: generated XmlWriter document -> 200 application/xml", label);
    }

    private static void AssertUnknownIsNotFound(HttpClient client, string label)
    {
        using var response = client.GetAsync("/todos/999").GetAwaiter().GetResult();
        Expect(label, response.StatusCode is HttpStatusCode.NotFound, "an unknown todo returns 404");
        Log.Information("  {Label}: unknown todo -> 404", label);
    }

    private static void AssertServedContract(HttpClient client, string label, AbsolutePath contract)
    {
        using var response = client.GetAsync("/openapi/v1.json").GetAwaiter().GetResult();
        Expect(label, response.StatusCode is HttpStatusCode.OK, "the OpenAPI document is served");
        var served = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

        AssertSameContract(contract.ReadAllText(), served, $"the {label} host");

        var document = JsonNode.Parse(served)!;
        var post = document["paths"]!["/todos"]!["post"]!;
        Expect(label, post["summary"]?.GetValue<string>() is "Creates a todo.", "the XML-comment summary reaches the document");
        Expect(label, post["responses"]!["400"]!["content"]!.AsObject().First().Key is "application/problem+json",
            "the 400 response is described as application/problem+json");

        var xmlResponse = document["paths"]!["/todos/{id}/xml"]!["get"]!["responses"]!["200"]!["content"]!;
        Expect(label, xmlResponse.AsObject().First().Key is "application/xml",
            "the XML response is described as application/xml");
        Expect(label, xmlResponse["application/xml"]!["schema"]!["$ref"]?.GetValue<string>() is "#/components/schemas/TodoXml",
            "the application/xml response is described by the generated TodoXml schema");

        var todoXml = document["components"]!["schemas"]!["TodoXml"]!;
        Expect(label, todoXml["xml"]!["name"]?.GetValue<string>() is "todo", "TodoXml carries the document element name");
        Expect(label, todoXml["properties"]!["id"]!["xml"]!["attribute"]?.GetValue<bool>() is true,
            "TodoXml marks id as an attribute");
        Expect(label, todoXml["properties"]!["due-by"]!["format"]?.GetValue<string>() is "date",
            "TodoXml describes due-by as a date");
        Log.Information("  {Label}: OpenAPI equals the committed contract", label);
    }

    private static HttpResponseMessage Post(HttpClient client, string path, string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return client.PostAsync(path, content).GetAwaiter().GetResult();
    }

    private static JsonNode? ReadJson(HttpResponseMessage response) =>
        JsonNode.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());

    private static void Expect(string label, bool condition, string what)
    {
        if (!condition)
            throw new InvalidOperationException($"{label}: {what} — it did not");
    }

    /// <summary>
    /// Both documents with their <c>servers</c> array removed and their members ordered, so the
    /// comparison sees the contract and not the host's address or the writer's member order.
    /// </summary>
    private static string Canonicalize(string json)
    {
        var node = JsonNode.Parse(json) ?? throw new InvalidOperationException("The OpenAPI document is not JSON");
        node.AsObject().Remove("servers");
        return Order(node).ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static JsonNode Order(JsonNode node)
    {
        switch (node)
        {
            case JsonObject o:
            {
                var ordered = new JsonObject();
                foreach (var (key, value) in o.ToList().OrderBy(static pair => pair.Key, StringComparer.Ordinal))
                {
                    o.Remove(key);
                    ordered[key] = value is null ? null : Order(value);
                }

                return ordered;
            }
            case JsonArray a:
            {
                var ordered = new JsonArray();
                foreach (var value in a.ToList())
                {
                    a.Remove(value);
                    ordered.Add(value is null ? null : Order(value));
                }

                return ordered;
            }
            default:
                return node.DeepClone();
        }
    }
}
