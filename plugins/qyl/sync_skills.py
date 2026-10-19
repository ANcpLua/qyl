#!/usr/bin/env python3
"""Copy or check the exact skill revisions declared in skill-sources.json, and the
investigation skill the qyl MCP server serves from this repository.

Use --source-root with local clones; otherwise retrieve the pinned public archives.
Only the named component skill directories, their license notices and the in-repo
skills are managed. The companion routing is adapted to the combined qyl namespace
during copying; Markdown files keep one trailing newline.
"""
import argparse
from io import BytesIO
import json
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import subprocess
import tarfile
from urllib.request import Request, urlopen
import zipfile

MAX_ARCHIVE_BYTES = 20 * 1024 * 1024
# The skills the MCP server serves, copied from their source in this repository.
IN_REPO_SKILLS = {"qyl-investigate": "services/qyl.mcp/skills/qyl-investigate"}


def component_files(component, source_root):
    repository = component["repository"]
    commit = component["commit"]
    if not re.fullmatch(r"https://github\.com/[\w.-]+/[\w.-]+", repository):
        raise ValueError("Only explicit public GitHub sources are supported")
    if not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise ValueError("Each component must pin a full commit")
    if source_root:
        clone = source_root / repository.rsplit("/", 1)[1]
        result = subprocess.run(["git", "-C", str(clone), "archive", commit],
                                check=True, capture_output=True)
        if len(result.stdout) > MAX_ARCHIVE_BYTES:
            raise ValueError("Component archive exceeds size limit")
        with tarfile.open(fileobj=BytesIO(result.stdout)) as archive:
            members = [(entry.name, entry.size, entry.issym() or entry.islnk(), entry.mode,
                        lambda entry=entry: archive.extractfile(entry).read())
                       for entry in archive if entry.isfile() or entry.issym() or entry.islnk()]
            return select_files(component, members)
    owner_repo = repository.removeprefix("https://github.com/")
    request = Request(f"https://codeload.github.com/{owner_repo}/zip/{commit}",
                      headers={"User-Agent": "qyl-skill-bundler"})
    with urlopen(request, timeout=30) as response:
        data = response.read(MAX_ARCHIVE_BYTES + 1)
    if len(data) > MAX_ARCHIVE_BYTES:
        raise ValueError("Component archive exceeds size limit")
    with zipfile.ZipFile(BytesIO(data)) as archive:
        members = [(entry.filename.partition("/")[2], entry.file_size,
                    ((entry.external_attr >> 16) & 0o170000) == 0o120000, entry.external_attr >> 16,
                    lambda entry=entry: archive.read(entry))
                   for entry in archive.infolist() if not entry.is_dir()]
        return select_files(component, members)


def select_files(component, members):
    selected = {}
    manifest = None
    total = 0
    skills = component["skills"]
    if any(not re.fullmatch(r"[a-z0-9]+(?:-[a-z0-9]+)*", name) for name in skills):
        raise ValueError("Invalid component skill name")
    for name, size, symlink, mode, read in members:
        path = PurePosixPath(name)
        relevant = (len(path.parts) >= 3 and path.parts[0] == "skills"
                    and path.parts[1] in skills) or name in component["notices"]
        if name == "plugin.json":
            manifest = json.loads(read())
        if not relevant:
            continue
        if path.is_absolute() or ".." in path.parts or symlink:
            raise ValueError(f"Unsafe component entry: {name}")
        if any(part in {"bin", "obj", "artifacts", "__pycache__"} for part in path.parts):
            raise ValueError(f"Build output in component source: {name}")
        total += size
        if total > MAX_ARCHIVE_BYTES:
            raise ValueError("Component files exceed size limit")
        destination = (name if path.parts[0] == "skills" else
                       f"licenses/{component['name']}/{name}")
        if destination in selected:
            raise ValueError(f"Duplicate component entry: {name}")
        data = read()
        if path.suffix == ".md":
            data = data.rstrip(b"\r\n") + b"\n"
        if destination == "skills/audit/references/mstest-routing.md":
            data = bundle_routing(data)
        selected[destination] = (data, 0o755 if mode & 0o111 else 0o644)
    if manifest is None or manifest.get("version") != component["version"]:
        raise ValueError("Pinned component version does not match its manifest")
    for name in skills:
        if f"skills/{name}/SKILL.md" not in selected:
            raise ValueError(f"Missing skill: {name}")
    for notice in component["notices"]:
        if f"licenses/{component['name']}/{notice}" not in selected:
            raise ValueError(f"Missing license notice: {notice}")
    return selected


def in_repo_files(root):
    repository = root.parent.parent
    selected = {}
    for name, source in IN_REPO_SKILLS.items():
        directory = repository / source
        if not (directory / "SKILL.md").is_file():
            raise ValueError(f"Missing in-repo skill: {source}")
        for path in sorted(directory.rglob("*")):
            if path.is_symlink():
                raise ValueError(f"Unsafe in-repo skill entry: {path}")
            if path.is_file():
                data = path.read_bytes()
                if path.suffix == ".md":
                    data = data.rstrip(b"\r\n") + b"\n"
                selected[f"skills/{name}/{path.relative_to(directory).as_posix()}"] = (data, 0o644)
    return selected


def bundle_routing(data):
    """Keep the upstream task table while selecting skills in this same bundle."""
    text = data.decode()
    marker = "| Task | Companion skill |"
    end = "\nIf the companion is unavailable,"
    if marker not in text or end not in text:
        raise ValueError("Upstream MSTest routing changed; update the bundle adapter")
    table = text[text.index(marker):text.index("\nKeep general MSBuild")]
    table = table.replace("Companion skill", "Bundled skill").replace("mstest-extensions:", "qyl:")
    return ("# Bundled MSTest routing\n\n"
            "All skills below are included in qyl. Select the smallest matching skill; "
            "preserve the caller's scope, including read-only audits.\n\n" + table +
            "\n\nUse the bundled .NET skills for general MSBuild, package versions and analyzers. "
            "Use these MSTest skills for test semantics. Do not preload every skill.\n\n"
            "If an entry is missing, report an incomplete qyl installation; "
            "do not claim it ran or install a duplicate companion plugin.\n").encode()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", type=Path)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent
    components = json.loads((root / "skill-sources.json").read_text())["components"]
    expected = {}
    owned = []
    for component in components:
        files = component_files(component, args.source_root)
        if set(expected) & set(files):
            raise ValueError("Components have conflicting paths")
        expected.update(files)
        owned.extend(root / "skills" / name for name in component["skills"])
        owned.append(root / "licenses" / component["name"])
    local = in_repo_files(root)
    if set(expected) & set(local):
        raise ValueError("In-repo skills conflict with a component")
    expected.update(local)
    owned.extend(root / "skills" / name for name in IN_REPO_SKILLS)
    if args.check:
        actual = {str(p.relative_to(root)): (p.read_bytes(), stat.S_IMODE(p.stat().st_mode))
                  for directory in owned for p in directory.rglob("*") if p.is_file()}
        if actual != expected:
            missing = sorted(set(expected) - set(actual))
            extra = sorted(set(actual) - set(expected))
            changed = sorted(k for k in expected.keys() & actual.keys() if expected[k] != actual[k])
            raise ValueError(f"Skill source mismatch: missing={missing}, extra={extra}, changed={changed}")
    else:
        for directory in owned:
            if directory.is_symlink():
                raise ValueError(f"Refusing linked output: {directory}")
        for directory in owned:
            if directory.exists():
                shutil.rmtree(directory)
        for name, (data, mode) in expected.items():
            path = root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
            path.chmod(mode)
    print(f"{'Checked' if args.check else 'Bundled'} {sum(len(c['skills']) for c in components)} skills from {len(components)} pinned sources"
          f" and {len(IN_REPO_SKILLS)} from this repository ({len(expected)} files).")


if __name__ == "__main__":
    main()
