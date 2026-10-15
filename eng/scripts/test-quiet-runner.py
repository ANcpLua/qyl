#!/usr/bin/env python3
"""Exercise gate progress and native failures through the real quiet-runner process."""

import pathlib
import select
import shutil
import subprocess
import sys
import tempfile
import unittest


SOURCE = pathlib.Path(__file__).with_name("quiet.sh")


class QuietRunnerTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="qyl quiet runner ")
        self.addCleanup(temporary.cleanup)
        self.root = pathlib.Path(temporary.name)
        self.script = self.root / "eng/scripts/quiet.sh"
        self.script.parent.mkdir(parents=True)
        shutil.copyfile(SOURCE, self.script)

    def test_live_log_is_reported_before_a_silent_command_finishes(self):
        barrier = self.root / "finish"
        command = self.root / "silent.py"
        command.write_text(
            "import pathlib, sys, time\n"
            "barrier = pathlib.Path(sys.argv[1])\n"
            "deadline = time.monotonic() + 15\n"
            "while not barrier.exists() and time.monotonic() < deadline: time.sleep(0.02)\n"
            "if not barrier.exists(): sys.exit(99)\n"
            "print('child finished')\n"
        )
        process = subprocess.Popen(
            ["bash", str(self.script), "active", "--", sys.executable, str(command), str(barrier)],
            cwd=self.root, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
            stderr=subprocess.PIPE, text=True,
        )
        try:
            ready, _, _ = select.select([process.stdout], [], [], 5)
            self.assertTrue(ready, "a silent gate must report its active log immediately")
            first_line = process.stdout.readline()
            self.assertTrue(first_line.startswith("RUNNING active "), first_line)
            self.assertIsNone(process.poll(), "progress must arrive before completion")
            live_log = pathlib.Path(first_line.rstrip().split(", live log ", 1)[1])
            self.assertTrue(live_log.is_file())
        finally:
            barrier.touch()
            output, errors = process.communicate(timeout=15)
        self.assertEqual(0, process.returncode, output + errors)
        self.assertIn("PASS active", output)
        self.assertIn("child finished", (self.root / "artifacts/logs/active.log").read_text())
        self.assertFalse(live_log.exists(), "the live log must move to the final artifact path")

    def test_native_failure_and_signal_exit_codes_are_preserved(self):
        command = self.root / "failure.py"
        command.write_text(
            "import os, signal, sys\n"
            "if sys.argv[1] == 'signal': os.kill(os.getpid(), signal.SIGKILL)\n"
            "sys.exit(42)\n"
        )
        for mode, expected in (("exit", 42), ("signal", 137)):
            with self.subTest(mode=mode):
                result = subprocess.run(
                    ["bash", str(self.script), mode, "--", sys.executable, str(command), mode],
                    cwd=self.root, text=True, capture_output=True, timeout=15,
                )
                self.assertEqual(expected, result.returncode, result.stdout + result.stderr)
                self.assertIn(f"FAIL {mode}", result.stdout)
                self.assertIn(f"exit {expected}", result.stdout)

    def test_failure_stops_later_steps(self):
        result = subprocess.run(
            ["bash", str(self.script), "steps", "exit 42", "touch should-not-run"],
            cwd=self.root, text=True, capture_output=True, timeout=15,
        )
        self.assertEqual(42, result.returncode, result.stdout + result.stderr)
        self.assertFalse((self.root / "should-not-run").exists())


if __name__ == "__main__":
    unittest.main()
