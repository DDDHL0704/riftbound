#!/usr/bin/env python3
"""Fail-closed reuse of a passed pull-request L2 for an identical main tree."""

import argparse
import json
import os
from pathlib import Path
import re
import subprocess
from urllib.parse import quote
from urllib.request import Request, urlopen


SHA = re.compile(r"[0-9a-fA-F]{40}")
TREE = re.compile(r"[0-9a-fA-F]{40}")
ROOT = Path(__file__).resolve().parents[1]


class ReuseUnavailable(RuntimeError):
    """The API or fixed checkout could not prove safe reuse."""


def git_tree(root, commit):
    if not SHA.fullmatch(commit or ""):
        raise ReuseUnavailable("invalid fixed commit SHA")
    try:
        return subprocess.check_output(
            ["git", "rev-parse", commit + "^{tree}"],
            cwd=root,
            stderr=subprocess.PIPE,
            text=True,
            timeout=20,
        ).strip()
    except (OSError, subprocess.SubprocessError) as error:
        raise ReuseUnavailable("cannot read the fixed main tree") from error


def api_get(url, token):
    if not token:
        raise ReuseUnavailable("GITHUB_TOKEN is unavailable")
    request = Request(
        url,
        headers={
            "Accept": "application/vnd.github+json",
            "Authorization": "Bearer " + token,
            "X-GitHub-Api-Version": "2022-11-28",
            "User-Agent": "riftbound-main-l2-reuse",
        },
    )
    try:
        with urlopen(request, timeout=20) as response:
            value = json.load(response)
    except Exception as error:  # urllib errors vary between runner Python versions.
        raise ReuseUnavailable("GitHub API query failed: " + type(error).__name__) from error
    if not isinstance(value, (dict, list)):
        raise ReuseUnavailable("GitHub API returned an unexpected payload")
    return value


def not_used(head, reason, tree_sha=None):
    return {
        "schemaVersion": 1,
        "status": "MAIN_L2_REUSE_NOT_USED",
        "reused": False,
        "headSha": head,
        "treeSha": tree_sha,
        "reason": reason,
    }


def valid_main_push_event(event, head):
    before = event.get("before") if isinstance(event, dict) else None
    return (
        isinstance(event, dict)
        and isinstance(head, str)
        and isinstance(before, str)
        and event.get("ref") == "refs/heads/main"
        and event.get("after") == head
        and SHA.fullmatch(before)
        and before != "0" * 40
    )


def _run_matches_pr(run, number):
    if not isinstance(run, dict):
        return False
    return any(
        isinstance(item, dict) and item.get("number") == number
        for item in run.get("pull_requests", [])
    )


def decide(event_name, event, head, root=ROOT, repository=None, token=None,
           fetch=api_get, tree_reader=git_tree):
    """Return a receipt; inability to prove reuse is a successful full-suite fallback."""
    if event_name != "push":
        return not_used(head, "Only a main push may reuse a pull-request L2")
    if not isinstance(head, str) or not SHA.fullmatch(head) or not valid_main_push_event(event, head):
        return not_used(head, "Malformed fixed push input")
    repository = repository or os.environ.get("GITHUB_REPOSITORY", "")
    if not repository or "/" not in repository:
        return not_used(head, "Repository identity is unavailable")
    try:
        tree_sha = tree_reader(root, head)
        if not TREE.fullmatch(tree_sha or ""):
            raise ReuseUnavailable("main tree identity is unavailable")
        api = "https://api.github.com/repos/" + repository
        associated = fetch(api + "/commits/" + head + "/pulls", token)
        if not isinstance(associated, list) or len(associated) != 1:
            raise ReuseUnavailable("main commit is not associated with exactly one PR")
        if not isinstance(associated[0], dict):
            raise ReuseUnavailable("associated PR payload is malformed")
        number = associated[0].get("number")
        if type(number) is not int or number <= 0:
            raise ReuseUnavailable("associated PR number is missing")
        pr = fetch(api + "/pulls/" + str(number), token)
        if not isinstance(pr, dict):
            raise ReuseUnavailable("merged PR payload is malformed")
        if (pr.get("merged_at") is None
                or pr.get("merge_commit_sha") != head
                or not isinstance(pr.get("base"), dict)
                or pr.get("base", {}).get("ref") != "main"):
            raise ReuseUnavailable("associated PR is not the merged main PR")
        if not isinstance(pr.get("head"), dict):
            raise ReuseUnavailable("associated PR head payload is malformed")
        branch = pr.get("head", {}).get("ref")
        if not branch:
            raise ReuseUnavailable("associated PR branch is missing")
        runs_payload = fetch(
            api + "/actions/runs?event=pull_request&branch=" + quote(branch, safe="")
            + "&per_page=100",
            token,
        )
        runs = runs_payload.get("workflow_runs") if isinstance(runs_payload, dict) else None
        candidates = [
            run for run in (runs or [])
            if isinstance(run, dict)
            and run.get("name") == "Backend L2"
            and run.get("event") == "pull_request"
            and run.get("status") == "completed"
            and run.get("conclusion") == "success"
            and _run_matches_pr(run, number)
        ]
        candidates.sort(key=lambda run: run.get("updated_at", ""), reverse=True)
        if not candidates:
            raise ReuseUnavailable("no passed pull-request Backend L2 was found")
        for run in candidates:
            run_head = run.get("head_sha")
            if not SHA.fullmatch(run_head or ""):
                continue
            commit = fetch(api + "/commits/" + run_head, token)
            if not isinstance(commit, dict) or not isinstance(commit.get("commit"), dict):
                continue
            tree = commit["commit"].get("tree")
            run_tree = tree.get("sha") if isinstance(tree, dict) else None
            if not TREE.fullmatch(run_tree or ""):
                continue
            if run_tree != tree_sha:
                continue
            if not isinstance(run.get("id"), int) or run["id"] <= 0:
                continue
            if not isinstance(run.get("run_attempt"), int) or run["run_attempt"] <= 0:
                continue
            return {
                "schemaVersion": 1,
                "status": "MAIN_L2_REUSE_VERIFIED",
                "reused": True,
                "headSha": head,
                "treeSha": tree_sha,
                "sourceTreeSha": run_tree,
                "pullRequest": number,
                "sourceRunId": run.get("id"),
                "sourceRunAttempt": run.get("run_attempt"),
                "sourceRunHeadSha": run_head,
                "sourceWorkflow": run.get("name"),
                "sourceEvent": run.get("event"),
                "sourceConclusion": run.get("conclusion"),
                "sourceRunUrl": run.get("html_url"),
                "reason": "Merged PR passed Backend L2 and has the identical Git tree",
            }
        raise ReuseUnavailable("passed PR L2 tree differs from main")
    except ReuseUnavailable as error:
        return not_used(head, str(error), locals().get("tree_sha"))
    except Exception as error:
        return not_used(head, "Cannot safely verify L2 reuse: " + type(error).__name__, locals().get("tree_sha"))


def verify_receipt(receipt_path, checkout_path, head, event_name, event,
                   run_id=None, run_attempt=None):
    receipt = json.loads(Path(receipt_path).read_text())
    checkout = json.loads(Path(checkout_path).read_text())
    if event_name != "push" or not valid_main_push_event(event, head):
        raise ValueError("L2 reuse is only valid for a main push")
    if (not SHA.fullmatch(head or "") or not str(run_id).isdigit()
            or not str(run_attempt).isdigit()):
        raise ValueError("Missing fixed event or workflow identity")
    if not isinstance(checkout, dict) or checkout.get("status") != "FIXED_CANDIDATE_CHECKOUT_VERIFIED":
        raise ValueError("L2 reuse checkout receipt is not verified")
    if (any(checkout.get(key) != head for key in ["actualSha", "expectedSha", "eventSha"])
            or checkout.get("runId") != run_id
            or checkout.get("runAttempt") != run_attempt
            or not TREE.fullmatch(checkout.get("treeSha", ""))):
        raise ValueError("L2 reuse checkout does not match the fixed workflow input")
    required = {
        "status": "MAIN_L2_REUSE_VERIFIED",
        "reused": True,
        "headSha": head,
        "treeSha": checkout.get("treeSha"),
        "sourceTreeSha": checkout.get("treeSha"),
        "sourceWorkflow": "Backend L2",
        "sourceEvent": "pull_request",
        "sourceConclusion": "success",
    }
    if any(receipt.get(key) != value for key, value in required.items()):
        raise ValueError("L2 reuse receipt does not match the fixed checkout")
    if receipt.get("runId") != run_id or receipt.get("runAttempt") != run_attempt:
        raise ValueError("L2 reuse receipt belongs to another workflow attempt")
    if (type(receipt.get("pullRequest")) is not int or receipt["pullRequest"] <= 0
            or type(receipt.get("sourceRunId")) is not int or receipt["sourceRunId"] <= 0):
        raise ValueError("L2 reuse receipt has no source identity")
    if (type(receipt.get("sourceRunAttempt")) is not int
            or receipt["sourceRunAttempt"] <= 0):
        raise ValueError("L2 reuse receipt has no source attempt")
    if not SHA.fullmatch(receipt.get("sourceRunHeadSha", "")):
        raise ValueError("L2 reuse source head is invalid")
    return {
        "status": "MAIN_L2_REUSE_RECEIPT_VERIFIED",
        "headSha": head,
        "treeSha": checkout.get("treeSha"),
        "pullRequest": receipt["pullRequest"],
        "sourceRunId": receipt["sourceRunId"],
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--event", type=Path, required=True)
    parser.add_argument("--event-name", default=os.environ.get("GITHUB_EVENT_NAME", ""))
    parser.add_argument("--head", required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--github-output", type=Path)
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--token", default=os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN", ""))
    parser.add_argument("--verify-receipt", type=Path)
    parser.add_argument("--checkout", type=Path)
    args = parser.parse_args()
    try:
        event = json.loads(args.event.read_text())
    except (OSError, ValueError, TypeError):
        if args.verify_receipt:
            raise
        event = {}
    if args.verify_receipt:
        result = verify_receipt(
            args.verify_receipt, args.checkout, args.head, args.event_name, event,
            os.environ.get("GITHUB_RUN_ID"), os.environ.get("GITHUB_RUN_ATTEMPT"),
        )
    else:
        result = decide(args.event_name, event, args.head, args.root.resolve(), token=args.token)
        result.update(runId=os.environ.get("GITHUB_RUN_ID"), runAttempt=os.environ.get("GITHUB_RUN_ATTEMPT"))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n")
    if args.github_output:
        with args.github_output.open("a") as stream:
            stream.write("reused=" + str(bool(result.get("reused"))).lower() + "\n")
    print(json.dumps(result))


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, TypeError, KeyError) as error:
        raise SystemExit("L2 reuse receipt rejected: " + str(error))
