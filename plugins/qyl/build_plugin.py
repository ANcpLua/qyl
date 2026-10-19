#!/usr/bin/env python3
"""Package qyl's skills and MCP configuration without running project code."""
import argparse
import hashlib
import json
from pathlib import Path
import stat
import xml.etree.ElementTree as ET
from zipfile import ZipFile, ZipInfo, ZIP_DEFLATED


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent
    output = args.output.resolve()
    if output.is_relative_to(root):
        raise ValueError("Write the archive outside the plugin source")
    manifest = json.loads((root / "plugin.json").read_text())
    versions = root.parents[1] / "Version.props"
    if versions.exists():
        manifest["version"] = ET.parse(versions).findtext(".//QylVersion")
    interface = manifest["extensions"]["com.openai"]["interface"]
    prompts = interface["defaultPrompt"]
    if isinstance(prompts, str):
        prompts = [prompts]
    if len(prompts) > 3 or any(not p.strip() or len(p) > 128 or "\n" in p for p in prompts):
        raise ValueError("Starter prompts must meet directory limits")
    skills = sorted((root / "skills").glob("*/SKILL.md"))
    if not skills:
        raise ValueError("The plugin has no skills")
    paths = [root / "mcp.json", root / "LICENSE", root / "skill-sources.json"]
    for folder in ["skills", "assets", "licenses"]:
        for path in sorted((root / folder).rglob("*")):
            if path.is_symlink():
                raise ValueError(f"Refusing linked package entry: {path}")
            if path.is_file() and path.name != ".DS_Store" and "__pycache__" not in path.parts:
                paths.append(path)
    output.parent.mkdir(parents=True, exist_ok=True)
    with ZipFile(output, "w", ZIP_DEFLATED) as archive:
        entries = [("plugin.json", (json.dumps(manifest, indent=2) + "\n").encode(), 0o644)]
        entries += [(str(p.relative_to(root)), p.read_bytes(), stat.S_IMODE(p.stat().st_mode)) for p in paths]
        for name, data, mode in entries:
            info = ZipInfo(name, date_time=(2026, 10, 19, 0, 0, 0))
            info.compress_type = ZIP_DEFLATED
            info.external_attr = (stat.S_IFREG | mode) << 16
            archive.writestr(info, data)
    with ZipFile(output) as archive:
        if archive.testzip() is not None:
            raise ValueError("Archive integrity check failed")
    print(json.dumps({"path": str(output), "version": manifest["version"],
                      "skills": len(skills), "files": len(entries),
                      "sha256": hashlib.sha256(output.read_bytes()).hexdigest()}, indent=2))


if __name__ == "__main__":
    main()
