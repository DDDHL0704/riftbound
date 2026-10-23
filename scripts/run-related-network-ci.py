#!/usr/bin/env python3
"""Run current-candidate PostgreSQL/SignalR restart cases in Actions."""
import argparse
import datetime
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import signal
import subprocess
import sys
import time
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
CASES = ("payment-resource-surplus", "damage-doubling-combat-keywords", "ven-flow-combat-keywords",
         "optional-additional-cost-reductions", "instruction-power-payment", "restricted-mana-lux",
         "empowered-target-action-replacement")
FINISH_CASES = ("instruction-power-payment", "restricted-mana-lux")
SPEC = importlib.util.spec_from_file_location("candidate", ROOT / "scripts/ci-candidate.py")
CANDIDATE = importlib.util.module_from_spec(SPEC)
previous_bytecode_setting = sys.dont_write_bytecode
try:
    # Importing the verifier must not create an untracked input in its checkout.
    sys.dont_write_bytecode = True
    SPEC.loader.exec_module(CANDIDATE)
finally:
    sys.dont_write_bytecode = previous_bytecode_setting


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def save(path, value):
    with path.open("x") as stream:
        json.dump(value, stream, indent=2)
        stream.write("\n")


def checked(command, root, env, log):
    with log.open("xb") as stream:
        subprocess.run(command, cwd=root, env=env, stdout=stream, stderr=subprocess.STDOUT, check=True)


def stop_api(process):
    if process.poll() is not None:
        raise RuntimeError("Dedicated API exited before its owner requested shutdown.")
    process.send_signal(signal.SIGINT)
    try:
        code = process.wait(timeout=20)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=10)
        raise RuntimeError("Dedicated API required forced cleanup; restart acceptance is incomplete.")
    if code not in (0, -signal.SIGINT, 128 + signal.SIGINT):
        raise RuntimeError("Dedicated API did not shut down normally: " + str(code))
    return code


def start_api(dotnet, assembly, url, env, log):
    stream = log.open("xb")
    process = subprocess.Popen([dotnet, str(assembly)], cwd=assembly.parent, env=env,
                               stdout=stream, stderr=subprocess.STDOUT)
    stream.close()
    deadline = time.monotonic() + 60
    try:
        while time.monotonic() < deadline:
            if process.poll() is not None:
                raise RuntimeError("Dedicated API exited during startup: " + str(process.returncode))
            try:
                with urllib.request.urlopen(url + "/health", timeout=1) as response:
                    if response.status == 200:
                        return process
            except (urllib.error.URLError, TimeoutError):
                pass
            time.sleep(0.2)
        raise RuntimeError("Dedicated API did not become healthy.")
    except BaseException:
        if process.poll() is None:
            stop_api(process)
        raise


def verify_loaded(path, pid, engine_sha, driver_runtime):
    value = json.loads(path.read_text())
    driver = json.loads(driver_runtime.read_text())
    engine = next(row for row in driver["assemblies"] if row["file"] == "Riftbound.Engine.dll")
    if value["processId"] != pid or value["sha256"] != engine_sha or engine["sha256"] != engine_sha:
        raise ValueError("Actual API/driver Engine path or SHA differs from the fixed runtime.")
    if value["mvid"].lower() != engine["mvid"].lower():
        raise ValueError("Actual API and driver loaded different Engine MVIDs.")
    return value


def verify_network_auth(output, previous_json_sha256=None):
    reports = []
    for path in sorted(output.rglob("*.json")):
        data = json.loads(path.read_text())
        if not isinstance(data, dict) or "traces" not in data or "snapshots" not in data:
            continue
        snapshots, traces, room = data["snapshots"], data["traces"], data.get("room")
        if not isinstance(snapshots, dict) or len(snapshots) != 2 or not isinstance(traces, list) or not room:
            raise ValueError("Network evidence requires its room and two actual private-view identities.")
        players, joined = set(snapshots), set()
        for trace in traces:
            if not isinstance(trace, dict) or trace.get("method") != "Joined":
                continue
            message = trace.get("message", {})
            payload = message.get("payload", {}) if isinstance(message, dict) else None
            if not isinstance(payload, dict):
                raise ValueError("Joined must contain an actual player session.")
            player, token = payload.get("playerId"), payload.get("reconnectToken")
            if (player not in players or message.get("playerId") != player or message.get("roomId") != room
                    or message.get("type") != "RECONNECT" or not isinstance(token, str) or not token.strip()
                    or trace.get("recipient", player) != player):
                raise ValueError("Joined session, player, room or rotated token differs from its network case.")
            if player in joined:
                raise ValueError("Each identity must receive exactly one actual reconnect Joined per trace.")
            joined.add(player)
        if joined != players:
            raise ValueError("Both players must receive successful RECONNECT Joined; snapshots alone cannot prove reconnect.")
        reports.append({"path": str(path.relative_to(output)), "room": room,
                        "players": sorted(players), "successfulJoinedPlayers": len(joined),
                        "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    if not reports:
        raise ValueError("Actual network traces with successful Joined are required.")
    if previous_json_sha256 is not None:
        rooms = {row["room"] for row in reports}
        fresh_rooms = {row["room"] for row in reports
                       if previous_json_sha256.get(row["path"]) != row["sha256"]}
        if fresh_rooms != rooms:
            raise ValueError("Each room needs fresh successful Joined evidence from this network phase.")
    return {"status": "ACTUAL_RECONNECT_JOINED_OBSERVED", "traces": reports,
            "sqlCredentialRotationAndWholeBackendCertified": False}


def run_case(case, output, driver, launch, shutdown, loaded):
    """The transport owner restarts; domain classes retain all rule assertions."""
    output.mkdir()
    driver("prepare", output)
    processes = []
    for recover in (False, True):
        stage = "after-restart" if recover else "before-restart"
        process = launch(stage, output)
        failure = None
        try:
            if processes and process.pid == processes[0]["processId"]:
                raise ValueError("Recover must use a different actual API process.")
            driver("recover" if recover else "play", output)
            driver("verify", output, stage)
            if recover and case in FINISH_CASES:
                # Verify the recovered transcript and read-only SQL parity
                # before resuming its original resource/payment instruction.
                driver("finish", output)
                driver("verify", output, "final")
            record = loaded(stage, process.pid, output)
        except BaseException as error:
            failure = error
            raise
        finally:
            try:
                exit_code = shutdown(process)
            except BaseException as cleanup:
                if failure is None:
                    raise
                failure.riftbound_cleanup_errors = [repr(cleanup)]
        processes.append({**record, "normalExitCode": exit_code})
    driver("tamper", output)
    return {"case": case, "status": "PASSED", "processes": processes,
            "actualApiRestart": True, "visibleOrWholeCardCertification": False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--expected", required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--case", choices=CASES, action="append")
    parser.add_argument("--allow-local-debug", action="store_true")
    args = parser.parse_args()
    cases = tuple(args.case or CASES)
    if os.environ.get("GITHUB_ACTIONS") != "true" and not (args.allow_local_debug and len(cases) == 1):
        parser.error("Heavy acceptance runs in Actions; local debug must explicitly select one failing case.")
    if len(set(cases)) != len(cases):
        parser.error("Duplicate cases are not allowed.")
    connection = os.environ.get("ConnectionStrings__Riftbound")
    if not connection:
        parser.error("An independent PostgreSQL ConnectionStrings__Riftbound is required.")
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    proof = {"status": "RUNNING", "fixedCommit": args.expected, "orderedCases": cases,
             "startedAtUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "cases": []}
    try:
        proof["checkout"] = CANDIDATE.verify(ROOT, args.expected, os.environ.get("GITHUB_SHA", ""))
        dotnet = shutil.which("dotnet")
        if not dotnet:
            raise RuntimeError("The Actions .NET runtime was not installed.")
        env = dict(os.environ, SourceRevisionId=args.expected)
        env.pop("DOTNET_STARTUP_HOOKS", None)
        env.update(ASPNETCORE_ENVIRONMENT="Production", ASPNETCORE_URLS="http://127.0.0.1:15090",
                   RIFTBOUND_SERVER_URL="http://127.0.0.1:15090")
        url = env["RIFTBOUND_SERVER_URL"]
        for project, name in [("src/Riftbound.Api", "api"), ("tests/Riftbound.BackendAcceptance", "driver")]:
            checked([dotnet, "build", project, "-m:1", "--nologo", "-v:q"], ROOT, env, output / ("build-" + name + ".log"))
        api = ROOT / "src/Riftbound.Api/bin/Debug/net10.0/Riftbound.Api.dll"
        driver_dll = ROOT / "tests/Riftbound.BackendAcceptance/bin/Debug/net10.0/Riftbound.BackendAcceptance.dll"
        runtime = {str(path): digest(path) for path in (api, driver_dll)}
        for name in ("Riftbound.Engine.dll", "Riftbound.Contracts.dll", "Riftbound.CardCatalog.dll", "Riftbound.Persistence.dll"):
            if digest(api.parent / name) != digest(driver_dll.parent / name):
                raise ValueError("API and driver do not contain identical " + name)
            for folder in (api.parent, driver_dll.parent):
                runtime[str(folder / name)] = digest(folder / name)
        for name in ("card-catalog.zh-CN.json", "rule-input-scope.json", "armaments.json"):
            source_sha = digest(ROOT / "data/official" / name)
            for folder in (api.parent, driver_dll.parent):
                path = folder / "data/official" / name
                if digest(path) != source_sha:
                    raise ValueError("Actual API/driver sidecar differs from candidate " + name)
                runtime[str(path)] = source_sha
        proof["fixedRuntimeSha256"] = runtime
        hook = output / "startup-hook"
        hook.mkdir()
        project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
        props = ET.SubElement(project, "PropertyGroup")
        for key, value in [("TargetFramework", "net10.0"), ("ImplicitUsings", "enable"), ("EnableDefaultCompileItems", "false")]:
            ET.SubElement(props, key).text = value
        ET.SubElement(ET.SubElement(project, "ItemGroup"), "Compile", Include=str(ROOT / "tests/Riftbound.NativeNetworkSmoke/PaymentAcceptanceStartupHook.cs"))
        ET.ElementTree(project).write(hook / "Hook.csproj")
        checked([dotnet, "build", str(hook / "Hook.csproj"), "-m:1", "--nologo", "-v:q"], ROOT, env, output / "build-hook.log")
        hook_dll = hook / "bin/Debug/net10.0/Hook.dll"
        runtime[str(hook_dll)] = digest(hook_dll)
        engine_sha = digest(api.parent / "Riftbound.Engine.dll")

        for case in cases:
            def driver(phase, folder, stage=None):
                command = [dotnet, str(driver_dll), case, phase, str(folder)]
                if stage:
                    command.append(stage)
                before_json = ({str(path.relative_to(folder)): digest(path) for path in folder.rglob("*.json")}
                               if phase in ("play", "recover", "finish") else None)
                checked(command, ROOT, env, folder / ("driver-" + (stage or phase) + ".log"))
                if before_json is not None:
                    save(folder / ("network-auth-" + phase + ".json"), verify_network_auth(folder, before_json))

            def launch(stage, folder):
                api_env = dict(env, DOTNET_STARTUP_HOOKS=str(hook_dll),
                               RIFTBOUND_PAYMENT_RUNTIME_RECEIPT=str(folder / ("api-runtime-" + stage + ".json")))
                return start_api(dotnet, api, url, api_env, folder / ("api-" + stage + ".log"))

            def loaded(stage, pid, folder):
                return verify_loaded(folder / ("api-runtime-" + stage + ".json"), pid, engine_sha,
                                     folder / "driver-runtime-prepare.json")

            proof["cases"].append(run_case(case, output / case, driver, launch, stop_api, loaded))
        if any(digest(Path(path)) != value for path, value in runtime.items()):
            raise ValueError("The fixed API/driver runtime changed during acceptance.")
        CANDIDATE.verify(ROOT, args.expected, os.environ.get("GITHUB_SHA", ""))
        proof["status"] = "RELATED_BACKEND_FIXED_CANDIDATE_COMPLETE" if cases == CASES else "BOUNDED_RELATED_BACKEND_CASE_COMPLETE"
    except BaseException as error:
        proof.update(status="RELATED_BACKEND_FAILURE_OR_INCOMPLETE", error=repr(error),
                     cleanupErrors=getattr(error, "riftbound_cleanup_errors", []))
        raise
    finally:
        proof["finishedAtUtc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        proof["wholeCardOrVisibleClientCertification"] = False
        save(output / "related-network-receipt.json", proof)
    print(json.dumps({"status": proof["status"], "fixedCommit": args.expected, "cases": cases}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
