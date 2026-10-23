import importlib.util
from pathlib import Path
import re
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("ci_candidate", ROOT / "scripts/ci-candidate.py")
CANDIDATE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CANDIDATE)


class CandidateCheckoutTests(unittest.TestCase):
    def setUp(self):
        folder = tempfile.TemporaryDirectory()
        self.addCleanup(folder.cleanup)
        self.root = Path(folder.name)
        self.git("init", "-q")
        (self.root / "input.txt").write_text("original fixed input\n")
        self.git("add", "input.txt")
        self.git("-c", "user.name=CI fixture", "-c", "user.email=ci-fixture@example.invalid",
                 "-c", "commit.gpgsign=false", "commit", "-qm", "fixed input")
        self.sha = self.git("rev-parse", "HEAD")

    def git(self, *args):
        return subprocess.check_output(["git", *args], cwd=self.root, stderr=subprocess.STDOUT).decode().strip()

    def test_exact_checkout_retains_commit_tree_and_content_identity(self):
        result = CANDIDATE.verify(self.root, self.sha, self.sha)
        self.assertEqual(self.sha, result["actualSha"])
        self.assertEqual(self.git("rev-parse", "HEAD^{tree}"), result["treeSha"])
        self.assertEqual(1, result["trackedEntries"])
        self.assertEqual(64, len(result["sourceIndexSha256"]))
        self.assertFalse(result["semanticOrBackendAcceptance"])

    def test_moved_checkout_cannot_certify_the_old_candidate(self):
        self.git("-c", "user.name=CI fixture", "-c", "user.email=ci-fixture@example.invalid",
                 "-c", "commit.gpgsign=false", "commit", "--allow-empty", "-qm", "next input")
        with self.assertRaisesRegex(ValueError, "differs from expected candidate"):
            CANDIDATE.verify(self.root, self.sha)

    def test_workflow_sha_and_checkout_cannot_disagree(self):
        with self.assertRaisesRegex(ValueError, "Workflow event SHA differs"):
            CANDIDATE.verify(self.root, self.sha, "0" * 40)

    def test_changed_tracked_input_fails_before_execution(self):
        (self.root / "input.txt").write_text("changed input\n")
        with self.assertRaisesRegex(ValueError, "changes or unexpected"):
            CANDIDATE.verify(self.root, self.sha)

    def test_untracked_source_cannot_join_a_fixed_candidate(self):
        (self.root / "extra.cs").write_text("// unexpected compile input\n")
        with self.assertRaisesRegex(ValueError, "unexpected untracked"):
            CANDIDATE.verify(self.root, self.sha)

    def test_abbreviations_refs_and_shell_text_are_not_commit_identities(self):
        for invalid in [self.sha[:8], "main", "refs/tags/candidate", "$(false)", "x" * 40, ""]:
            with self.subTest(invalid=invalid), self.assertRaises(ValueError):
                CANDIDATE.verify(self.root, invalid)


class WorkflowCandidateTests(unittest.TestCase):
    def test_every_checkout_is_sha_bound_and_verified_without_automatic_cancellation(self):
        text = (ROOT / ".github/workflows/native-desktop.yml").read_text()
        jobs = dict(re.findall(r"^  ([a-z_]+):\n(.*?)(?=^  [a-z_]+:\n|\Z)", text, re.M | re.S))
        self.assertIn("related_network", jobs)
        for name, block in jobs.items():
            if "- uses: actions/checkout@" in block:
                with self.subTest(job=name):
                    self.assertIn("ref: ${{ github.sha }}", block)
                    self.assertIn("scripts/ci-candidate.py", block)
                    self.assertIn('--event-sha "$GITHUB_SHA"', block)
                    self.assertRegex(block, r'Verify the fixed candidate checkout\n(?:        if: [^\n]+\n)?        shell: bash\n')
        self.assertNotIn("cancel-in-progress: true", text)
        self.assertIn("candidate_sha:", text)
