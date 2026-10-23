#!/usr/bin/env python3
"""Verify the actual checkout before any fixed-candidate CI execution."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess


def verify(root, expected, event_sha=""):
    if not re.fullmatch(r"[0-9a-fA-F]{40}", expected):
        raise ValueError("Candidate identity must be a complete 40-character commit SHA.")
    expected = expected.lower()
    git = lambda *args: subprocess.check_output(["git", *args], cwd=root).decode().strip()
    actual = git("rev-parse", "HEAD")
    if actual != expected:
        raise ValueError("Actual checkout " + actual + " differs from expected candidate " + expected)
    if event_sha and event_sha.lower() != actual:
        raise ValueError("Workflow event SHA differs from candidate; dispatch its exact immutable ref.")
    if git("status", "--porcelain"):
        raise ValueError("Candidate checkout has tracked changes or unexpected untracked files.")
    index = subprocess.check_output(["git", "ls-tree", "-rz", "HEAD"], cwd=root)
    return {"status": "FIXED_CANDIDATE_CHECKOUT_VERIFIED", "expectedSha": expected,
            "actualSha": actual, "eventSha": event_sha or None,
            "treeSha": git("rev-parse", "HEAD^{tree}"),
            "sourceIndexSha256": hashlib.sha256(index).hexdigest(),
            "trackedEntries": len(index.rstrip(b"\0").split(b"\0")) if index else 0,
            "semanticOrBackendAcceptance": False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--expected", required=True)
    parser.add_argument("--event-sha", default="")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    try:
        result = verify(args.root.resolve(), args.expected, args.event_sha)
        code = 0
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        result = {"status": "FIXED_CANDIDATE_CHECKOUT_REJECTED", "error": str(error),
                  "semanticOrBackendAcceptance": False}
        code = 1
    result.update(checkedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  runId=os.environ.get("GITHUB_RUN_ID"), runAttempt=os.environ.get("GITHUB_RUN_ATTEMPT"),
                  job=os.environ.get("GITHUB_JOB"), ref=os.environ.get("GITHUB_REF"),
                  workflowRef=os.environ.get("GITHUB_WORKFLOW_REF"))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("x") as output:
        json.dump(result, output, indent=2)
        output.write("\n")
    print(json.dumps(result))
    return code


if __name__ == "__main__":
    raise SystemExit(main())
