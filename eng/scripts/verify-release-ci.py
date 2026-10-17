#!/usr/bin/env python3
"""Require a completed full GitHub check for the exact commit before publication."""
import json
import subprocess
import sys

REPOSITORY = "ANcpLua/qyl"


def gh_json(*arguments):
    return json.loads(subprocess.check_output(
        ["gh", *arguments, "--repo", REPOSITORY], text=True))


def verify(commit):
    runs = gh_json("run", "list", "--workflow", "check.yml", "--branch", "main",
                   "--commit", commit, "--status", "success", "--limit", "100",
                   "--json", "databaseId")
    for candidate in runs:
        run = gh_json("run", "view", str(candidate["databaseId"]), "--json",
                      "headSha,headBranch,event,status,conclusion,jobs,url")
        if (run["headSha"] != commit or run["headBranch"] != "main"
                or run["event"] not in ("push", "workflow_dispatch")
                or run["status"] != "completed" or run["conclusion"] != "success"):
            continue
        for job in run["jobs"]:
            if (job["name"] != "check-full" or job["status"] != "completed"
                    or job["conclusion"] != "success"):
                continue
            passed = {step["name"] for step in job["steps"]
                      if step["status"] == "completed" and step["conclusion"] == "success"}
            if {"NativeAOT prerequisites", "Gate"} <= passed:
                print(f"Full Linux check passed for {commit}: {run['url']}")
                return
    raise SystemExit(
        f"release: no successful check-full run for {commit}. "
        "Wait for its main push check, or rerun check.yml with full=true; nothing may publish yet.")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("usage: verify-release-ci.py <commit>")
    try:
        verify(sys.argv[1])
    except (subprocess.CalledProcessError, OSError, ValueError, KeyError, TypeError) as error:
        raise SystemExit(f"release: unable to verify the full GitHub check: {error}") from error
