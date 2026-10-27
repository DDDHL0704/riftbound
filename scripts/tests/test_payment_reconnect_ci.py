import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("payment_reconnect_ci", ROOT / "scripts/run-payment-reconnect-ci.py")
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
        self.assertEqual([("sql", None), ("prepare", None), ("start", "before-restart", 1), ("play", None),
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

    def test_finish_resumes_only_after_readonly_recovery_verification(self):
        def driver(phase, folder, stage=None):
            self.calls.append((phase, stage))
        result = RUNNER.run_case("instruction-power-payment", self.output, driver, self.launch, self.stop, self.loaded)
        self.assertEqual([("sql", None), ("prepare", None), ("start", "before-restart", 1), ("play", None),
                          ("verify", "before-restart"), ("stop", 1), ("start", "after-restart", 2),
                          ("recover", None), ("verify", "after-restart"), ("finish", None),
                          ("verify", "final"), ("stop", 2), ("tamper", None)], self.calls)
        self.assertEqual("PASSED", result["status"])

    def test_failed_final_verification_stops_api_without_tamper_or_pass(self):
        def driver(phase, folder, stage=None):
            self.calls.append((phase, stage))
            if stage == "final":
                raise ValueError("final payment state differs")
        with self.assertRaisesRegex(ValueError, "final payment state differs"):
            RUNNER.run_case("restricted-mana-lux", self.output, driver, self.launch, self.stop, self.loaded)
        self.assertIn(("verify", "after-restart"), self.calls)
        self.assertEqual([("finish", None), ("verify", "final"), ("stop", 2)], self.calls[-3:])
        self.assertNotIn(("tamper", None), self.calls)

    def test_imposed_payment_finishes_only_after_verified_real_restart(self):
        def driver(phase, folder, stage=None):
            self.calls.append((phase, stage))
        result = RUNNER.run_case("imposed-movement", self.output, driver, self.launch, self.stop, self.loaded)
        self.assertEqual([("sql", None), ("prepare", None), ("start", "before-restart", 1), ("play", None),
                          ("verify", "before-restart"), ("stop", 1), ("start", "after-restart", 2),
                          ("recover", None), ("verify", "after-restart"), ("finish", None),
                          ("verify", "final"), ("stop", 2), ("tamper", None)], self.calls)
        self.assertEqual("PASSED", result["status"])

    def test_failed_imposed_recovery_cannot_finish_or_tamper(self):
        def driver(phase, folder, stage=None):
            self.calls.append((phase, stage))
            if stage == "after-restart":
                raise ValueError("original recovered payment differs")
        with self.assertRaisesRegex(ValueError, "original recovered payment differs"):
            RUNNER.run_case("imposed-movement", self.output, driver, self.launch, self.stop, self.loaded)
        self.assertEqual([("recover", None), ("verify", "after-restart"), ("stop", 2)], self.calls[-3:])
        self.assertNotIn(("finish", None), self.calls)
        self.assertNotIn(("tamper", None), self.calls)

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


class RuntimeInputTests(unittest.TestCase):
    def test_missing_default_imposed_project_fails_before_starting_any_api(self):
        # Injected build failure only; these callbacks are not backend evidence.
        def build(command, root, env, log):
            if command[2] == 'tests/Riftbound.ImposedMovementAcceptance':
                raise FileNotFoundError('required imposed project missing')
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder) / 'missing-imposed'
            argv = ['run-payment-reconnect-ci.py', '--expected', 'a' * 40, '--output', str(output)]
            environment = {'GITHUB_ACTIONS': 'true', 'ConnectionStrings__Riftbound':
                           'Host=127.0.0.1;Port=5432;Database=riftbound_acceptance;Username=unit-only'}
            with mock.patch.object(RUNNER.sys, 'argv', argv), \
                    mock.patch.dict(RUNNER.os.environ, environment), \
                    mock.patch.object(RUNNER.CANDIDATE, 'verify', return_value={'syntheticUnitFixtureOnly': True}), \
                    mock.patch.object(RUNNER.shutil, 'which', return_value='/synthetic/dotnet'), \
                    mock.patch.object(RUNNER, 'checked', side_effect=build), \
                    mock.patch.object(RUNNER, 'start_api') as api:
                with self.assertRaisesRegex(FileNotFoundError, 'required imposed project missing'):
                    RUNNER.main()
            api.assert_not_called()
            proof = json.loads((output / 'payment-reconnect-receipt.json').read_text())
            self.assertIn('imposed-movement', proof['orderedCases'])
            self.assertEqual('PAYMENT_RECONNECT_FAILURE_OR_INCOMPLETE', proof['status'])
            self.assertEqual([], proof['cases'])

    def test_network_and_sql_inherit_the_same_dedicated_database(self):
        connection = 'Host=127.0.0.1;Port=5432;Database=riftbound_acceptance;Username=postgres;Password=unit-only'
        env = RUNNER.ci_environment('a' * 40, {'ConnectionStrings__Riftbound': connection,
            'RIFTBOUND_SERVER_URL': 'https://outside.invalid', 'DOTNET_STARTUP_HOOKS': 'unrelated-hook'})
        self.assertEqual(connection, env['ConnectionStrings__Riftbound'])
        self.assertEqual('http://127.0.0.1:15090', env['RIFTBOUND_SERVER_URL'])
        self.assertEqual(env['RIFTBOUND_SERVER_URL'], env['ASPNETCORE_URLS'])
        self.assertNotIn('DOTNET_STARTUP_HOOKS', env)
        for connection in ('', 'Host=prod;Port=5432;Database=riftbound_acceptance',
                           'Host=127.0.0.1;Port=5432;Database=original_matches'):
            with self.subTest(connection=connection), self.assertRaises(ValueError):
                RUNNER.ci_environment('a' * 40, {'ConnectionStrings__Riftbound': connection})

    def test_wrong_runtime_path_sha_revision_or_api_mvid_cannot_pass(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            driver = root / 'driver.json'
            api = root / 'api.json'
            engine = root / 'Riftbound.Engine.dll'
            expected = 'a' * 40
            value = dict(assemblyPath=str(engine), sha256='b' * 64, mvid='unit-mvid',
                         informationalVersion='1.0.0+' + expected)
            driver.write_text(json.dumps(value))
            self.assertEqual(value, RUNNER.verify_driver_loaded(driver, engine, 'b' * 64, expected))
            for key, wrong in [('assemblyPath', str(root / 'other.dll')), ('sha256', 'c' * 64),
                               ('mvid', ''), ('informationalVersion', '1.0.0+' + 'd' * 40)]:
                driver.write_text(json.dumps(dict(value, **{key: wrong})))
                with self.subTest(field=key), self.assertRaises(ValueError):
                    RUNNER.verify_driver_loaded(driver, engine, 'b' * 64, expected)
            driver.write_text(json.dumps(value))
            receipt = dict(processId=42, sha256='b' * 64, mvid='unit-mvid', loadedAssemblyPath=str(engine))
            api.write_text(json.dumps(receipt))
            self.assertEqual(receipt, RUNNER.verify_loaded(api, 42, 'b' * 64, driver, engine))
            for key, wrong in [('processId', 41), ('sha256', 'c' * 64), ('mvid', 'other-mvid'),
                               ('loadedAssemblyPath', str(root / 'stale.dll'))]:
                api.write_text(json.dumps(dict(receipt, **{key: wrong})))
                with self.subTest(field=key), self.assertRaises(ValueError):
                    RUNNER.verify_loaded(api, 42, 'b' * 64, driver, engine)

    def test_import_cannot_dirty_the_fixed_checkout(self):
        import subprocess, sys
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / 'scripts').mkdir()
            for name in ('ci-candidate.py', 'run-payment-reconnect-ci.py'):
                (root / 'scripts' / name).write_bytes((ROOT / 'scripts' / name).read_bytes())
            command = "import sys,runpy;sys.pycache_prefix=None;sys.dont_write_bytecode=False;sys.argv=['scripts/run-payment-reconnect-ci.py','--help'];runpy.run_path(sys.argv[0],run_name='__main__')"
            subprocess.run([sys.executable, '-c', command], cwd=root, check=True, capture_output=True)
            self.assertFalse((root / 'scripts/__pycache__').exists())


class NetworkAuthenticationEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.output = Path(self.temp.name)

    def data(self):
        return {"room": "synthetic-room", "snapshots": {"P1": {}, "P2": {}}, "traces": [
            {"recipient": player, "method": "Joined", "message": {
                "type": "RECONNECT", "roomId": "synthetic-room", "playerId": player,
                "payload": {"playerId": player, "seat": player, "reconnectToken": "synthetic-" + player}}}
            for player in ("P1", "P2")]}

    def write(self, value):
        (self.output / "network-after-restart.json").write_text(json.dumps(value))

    def test_successful_joined_is_bound_to_both_players_and_room(self):
        self.write(self.data())
        proof = RUNNER.verify_network_auth(self.output)
        self.assertEqual(2, proof["traces"][0]["successfulJoinedPlayers"])
        self.assertFalse(proof["sqlCredentialRotationAndWholeBackendCertified"])

    def test_original_false_green_pattern_cannot_pass_on_snapshots(self):
        value = self.data()
        value["traces"] = [{"method": "Error", "message": {"payload": {
            "code": "INVALID_RECONNECT_TOKEN"}}} for _ in range(2)]
        value["failure"] = None
        self.write(value)
        with self.assertRaisesRegex(ValueError, "successful RECONNECT Joined"):
            RUNNER.verify_network_auth(self.output)

    def test_each_trace_needs_success_even_when_an_earlier_trace_passed(self):
        self.write(self.data())
        value = self.data()
        value["traces"] = []
        (self.output / "network-before-restart.json").write_text(json.dumps(value))
        with self.assertRaises(ValueError):
            RUNNER.verify_network_auth(self.output)

    def test_missing_evidence_and_wrong_room_player_type_or_token_fail(self):
        with self.assertRaises(ValueError):
            RUNNER.verify_network_auth(self.output)
        for field, wrong in (("roomId", "other-room"), ("playerId", "P2"), ("type", "JOIN")):
            value = self.data()
            value["traces"][0]["message"][field] = wrong
            self.write(value)
            with self.subTest(field=field), self.assertRaises(ValueError):
                RUNNER.verify_network_auth(self.output)
        value = self.data()
        value["traces"][0]["message"]["payload"]["reconnectToken"] = ""
        self.write(value)
        with self.assertRaises(ValueError):
            RUNNER.verify_network_auth(self.output)

    def test_duplicate_joined_and_blank_token_fail(self):
        value = self.data()
        value["traces"].append(value["traces"][0])
        self.write(value)
        with self.assertRaisesRegex(ValueError, "exactly one"):
            RUNNER.verify_network_auth(self.output)
        value = self.data()
        value["traces"][0]["message"]["payload"]["reconnectToken"] = "   "
        self.write(value)
        with self.assertRaises(ValueError):
            RUNNER.verify_network_auth(self.output)

    def test_previous_phase_evidence_cannot_replace_fresh_joined_for_each_room(self):
        self.write(self.data())
        proof = RUNNER.verify_network_auth(self.output)
        before = {row["path"]: row["sha256"] for row in proof["traces"]}
        with self.assertRaisesRegex(ValueError, "fresh successful Joined"):
            RUNNER.verify_network_auth(self.output, before)
        second = self.data()
        second["room"] = "second-room"
        for row in second["traces"]:
            row["message"]["roomId"] = "second-room"
        (self.output / "second-before.json").write_text(json.dumps(second))
        proof = RUNNER.verify_network_auth(self.output)
        before = {row["path"]: row["sha256"] for row in proof["traces"]}
        (self.output / "network-new-phase.json").write_text(json.dumps(self.data()))
        with self.assertRaisesRegex(ValueError, "fresh successful Joined"):
            RUNNER.verify_network_auth(self.output, before)
        (self.output / "second-new-phase.json").write_text(json.dumps(second))
        self.assertEqual("ACTUAL_RECONNECT_JOINED_OBSERVED", RUNNER.verify_network_auth(self.output, before)["status"])
