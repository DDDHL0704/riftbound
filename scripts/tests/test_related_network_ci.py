import importlib.util
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("related_network_ci", ROOT / "scripts/run-related-network-ci.py")
RUNNER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RUNNER)


class RestartSequenceTests(unittest.TestCase):
    def setUp(self):
        folder = tempfile.TemporaryDirectory()
        self.addCleanup(folder.cleanup)
        self.output = Path(folder.name) / "case"
        self.calls = []
        self.pid = 0

    def launch(self, stage, folder):
        self.pid += 1
        self.calls.append(("start", stage, self.pid))
        return type("Process", (), {"pid": self.pid})()

    def stop(self, process):
        self.calls.append(("stop", process.pid))
        return 0

    def loaded(self, stage, pid, folder):
        return {"processId": pid, "stage": stage, "syntheticUnitFixtureNotBackendEvidence": True}

    def test_recover_uses_new_process_after_before_stage_has_stopped(self):
        def driver(phase, folder, stage=None):
            self.calls.append((phase, stage))
        result = RUNNER.run_case("synthetic-unit-only", self.output, driver, self.launch, self.stop, self.loaded)
        self.assertEqual([("prepare", None), ("start", "before-restart", 1), ("play", None),
                          ("verify", "before-restart"), ("stop", 1), ("start", "after-restart", 2),
                          ("recover", None), ("verify", "after-restart"), ("stop", 2), ("tamper", None)], self.calls)
        self.assertEqual([1, 2], [p["processId"] for p in result["processes"]])

    def test_failed_driver_stops_its_api_and_cannot_reach_recover_or_pass(self):
        def driver(phase, folder, stage=None):
            if phase == "play":
                raise ValueError("original failure retained")
        with self.assertRaisesRegex(ValueError, "original failure retained"):
            RUNNER.run_case("synthetic-unit-only", self.output, driver, self.launch, self.stop, self.loaded)
        self.assertEqual([("start", "before-restart", 1), ("stop", 1)], self.calls)

    def test_pid_reuse_is_not_a_real_restart(self):
        def same_process(stage, folder):
            return type("Process", (), {"pid": 55})()
        with self.assertRaisesRegex(ValueError, "different actual API process"):
            RUNNER.run_case("synthetic-unit-only", self.output, lambda *args: None,
                            same_process, self.stop, self.loaded)
        self.assertEqual([("stop", 55), ("stop", 55)], self.calls)

    def test_cleanup_failure_cannot_hide_the_original_driver_failure(self):
        def driver(phase, folder, stage=None):
            if phase == "play":
                raise ValueError("original rejection")
        def broken_stop(process):
            raise RuntimeError("shutdown failed")
        with self.assertRaisesRegex(ValueError, "original rejection") as caught:
            RUNNER.run_case("synthetic-unit-only", self.output, driver, self.launch, broken_stop, self.loaded)
        self.assertIn("shutdown failed", caught.exception.riftbound_cleanup_errors[0])
