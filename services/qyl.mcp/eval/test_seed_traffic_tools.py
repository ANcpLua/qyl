"""The answer-key seed's traffic mode samples only tools the MCP server has.

The seed is read as source, not run: its traffic lists are module-level constants,
and the manifest snapshot is the published tool surface.
"""
import ast
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[3]
SEED = ROOT / "tests/Qyl.Collector.Tests/Fixtures/seed-answer-key.py"
MANIFEST = ROOT / "services/qyl.mcp/tool-manifest.snapshot.json"
TRAFFIC_LISTS = ("TRAFFIC_MCP_TOOLS", "TRAFFIC_AGENT_TOOLS")


def seed_constant(name):
    """The literal a module-level assignment of the seed binds to `name`."""
    module = ast.parse(SEED.read_text(), str(SEED))
    for node in module.body:
        if isinstance(node, ast.Assign) and any(isinstance(target, ast.Name) and target.id == name for target in node.targets):
            return ast.literal_eval(node.value)
    raise AssertionError(f"{SEED.name} declares no module-level {name}")


class SeedTrafficToolsTests(unittest.TestCase):
    def test_traffic_samples_only_tools_the_manifest_publishes(self):
        published = {tool["name"] for tool in json.loads(MANIFEST.read_text())["tools"]}
        self.assertTrue(published)
        for name in TRAFFIC_LISTS:
            with self.subTest(name):
                sampled = seed_constant(name)
                self.assertTrue(sampled, f"{name} is empty")
                self.assertEqual(sorted(set(sampled) - published), [], f"{name} samples tools the server does not have")


if __name__ == "__main__":
    unittest.main()
