#!/usr/bin/env python3
"""Verify the exported desktop runtime on its actual OS; no publishing or signing secrets."""
import hashlib
from contextlib import contextmanager
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import urllib.request
import zipfile

PROJECT = Path(__file__).resolve().parents[1]
ROOT = PROJECT.parents[1]
VERSION = "4.7.2"
RELEASE = f"https://github.com/godotengine/godot-builds/releases/download/{VERSION}-stable/"
EDITOR = {
    "darwin": ("macos.universal", "8af3977b60d2c59802f7c8ff1914b3ca02a5e294f7381fc1104ee777e33cbbd8"),
    "win32": ("win64", "a2a48473a7414c5f19fab690518caebb738c09ef9601f6bd2388676a7f53b3c0"),
}
TEMPLATE_SHA = "92f8681e349ef1f90891b792da95e3b2b0bd1ed610b78018c58feb2d87e15a9d"


def download(name, digest, directory):
    destination = directory / name
    if destination.is_file():
        with destination.open("rb") as source:
            if hashlib.file_digest(source, "sha256").hexdigest() == digest:
                return destination
    with urllib.request.urlopen(RELEASE + name, timeout=120) as response, destination.open("wb") as output:
        shutil.copyfileobj(response, output)
    with destination.open("rb") as source:
        actual = hashlib.file_digest(source, "sha256").hexdigest()
    if actual != digest:
        raise RuntimeError(f"SHA-256 mismatch for {name}")
    return destination


@contextmanager
def packaging_without_editor_plugins(project):
    config = project / "project.godot"
    original = config.read_bytes()
    text = original.decode("utf-8")
    # Only editor tooling is disabled. The game settings and exported runtime
    # are exercised normally; always restore the exact source bytes.
    packaging = re.sub(r'(\[editor_plugins\][^\[]*?^enabled=)[^\r\n]*',
                       r'\1PackedStringArray()', text, count=1, flags=re.M)
    try:
        config.write_bytes(packaging.encode("utf-8"))
        yield
    finally:
        config.write_bytes(original)


def run(args, log, timeout=600, cwd=ROOT):
    print("Running", " ".join(map(str, args)), flush=True)
    with log.open("w", encoding="utf-8") as output:
        result = subprocess.run(list(map(str, args)), cwd=cwd, stdout=output, stderr=subprocess.STDOUT, timeout=timeout)
    text = log.read_text(encoding="utf-8", errors="replace")
    if result.returncode or re.search(r"^ERROR:", text, re.M):
        print(text)
        raise RuntimeError(f"Command failed; see {log.name}")
    return text


def verify_macos_sigterm_shutdown(executable, log, cwd):
    import errno
    import pty
    import select
    import signal
    import time

    command = [str(executable), "--headless", "--max-fps", "60", "--quit-after", "3600", "--",
               "--riftbound-ephemeral-session", "--riftbound-server=http://127.0.0.1:1"]
    master, slave = pty.openpty()
    output = bytearray()
    process = None
    sent = False
    try:
        process = subprocess.Popen(command, cwd=cwd, stdin=subprocess.DEVNULL, stdout=slave, stderr=slave)
        os.close(slave)
        slave = -1
        deadline = time.monotonic() + 90
        while True:
            if time.monotonic() > deadline:
                raise TimeoutError("Exported Main did not boot and exit after SIGTERM within 90 seconds")
            if select.select([master], [], [], 0.1)[0]:
                try:
                    block = os.read(master, 65536)
                except OSError as error:
                    if error.errno != errno.EIO:
                        raise
                    break
                if not block:
                    break
                output.extend(block)
            if not sent and b"Client booted." in output and re.search(rb"Official catalog loaded: [1-9]\d* cards\.", output):
                process.send_signal(signal.SIGTERM)
                sent = True
        exit_code = process.wait(timeout=5)
    finally:
        if process is not None and process.poll() is None:
            process.kill()
            process.wait(timeout=5)
        if slave >= 0:
            os.close(slave)
        os.close(master)
        log.write_bytes(output)
    text = output.decode("utf-8", errors="replace")
    if (not sent or exit_code != 0 or "Termination requested; exiting through Godot." not in text
            or re.search(r"ERROR:|Unhandled exception|libc\+\+abi: terminating", text)):
        print(text)
        raise RuntimeError(f"Exported Main SIGTERM shutdown failed (exit {exit_code}); see {log.name}")
    print("macOS: exported Main SIGTERM exited through the Godot loop without errors.")


def main():
    platform = sys.platform
    if platform not in EDITOR:
        raise SystemExit("Run this gate on macOS or Windows.")
    tools_dir = Path(os.environ.get("RUNNER_TEMP", ROOT / "var")) / "riftbound-godot-ci"
    tools_dir.mkdir(parents=True, exist_ok=True)
    logs = PROJECT / "exports" / "ci-logs"
    logs.mkdir(parents=True, exist_ok=True)
    suffix, digest = EDITOR[platform]
    editor_archive = download(f"Godot_v{VERSION}-stable_mono_{suffix}.zip", digest, tools_dir)
    if platform == "darwin":
        subprocess.run(["ditto", "-xk", str(editor_archive), str(tools_dir)], check=True)
        editor = next(tools_dir.glob("*.app/Contents/MacOS/Godot"))
        template_root = Path.home() / "Library/Application Support/Godot/export_templates"
    else:
        with zipfile.ZipFile(editor_archive) as archive:
            archive.extractall(tools_dir)
        editor = next(tools_dir.rglob("Godot*_console.exe"))
        template_root = Path(os.environ["APPDATA"]) / "Godot/export_templates"
    templates = download(f"Godot_v{VERSION}-stable_mono_export_templates.tpz", TEMPLATE_SHA, tools_dir)
    template_dir = template_root / f"{VERSION}.stable.mono"
    template_dir.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(templates) as archive:
        for member in archive.infolist():
            name = Path(member.filename).name
            if name in ("version.txt", "icudt_godot.dat", "macos.zip") or (name.startswith("windows_") and "x86_64" in name):
                with archive.open(member) as source, (template_dir / name).open("wb") as output:
                    shutil.copyfileobj(source, output)

    run(["dotnet", "build", PROJECT / "Riftbound.GodotClient.csproj"], logs / "build.log")
    with packaging_without_editor_plugins(PROJECT):
        run([editor, "--headless", "--editor", "--path", PROJECT, "--import"], logs / "import.log")
        lifecycle = run([editor, "--headless", "--path", PROJECT,
                         "res://scenes/debug/RuneTableLifecycleProof.tscn"], logs / "rune-table-lifecycle.log", timeout=90)
        if "RUNE_TABLE_LIFECYCLE_PASS: 2 freed-control transitions, 4 native signals" not in lifecycle:
            raise RuntimeError("Native table reuse after returning to the lobby was not verified")
        target = "macOS" if platform == "darwin" else "Windows Desktop"
        output_dir = PROJECT / "exports" / ("macos" if platform == "darwin" else "windows")
        output_dir.mkdir(parents=True, exist_ok=True)
        package = output_dir / ("Riftbound.zip" if platform == "darwin" else "Riftbound.exe")
        run([editor, "--headless", "--verbose", "--path", PROJECT, "--export-release", target, package], logs / "export.log")
    if platform == "darwin":
        extracted = tools_dir / "exported"
        subprocess.run(["ditto", "-xk", str(package), str(extracted)], check=True)
        app = next(extracted.glob("*.app"))
        subprocess.run(["codesign", "--verify", "--deep", "--strict", str(app)], check=True)
        executable = next((app / "Contents/MacOS").iterdir())
    else:
        executable = package
        runtime = output_dir / "data_Riftbound.GodotClient_windows_x86_64"
        if not (runtime / "Riftbound.GodotClient.dll").is_file():
            raise RuntimeError("Windows managed runtime was not exported")
        with zipfile.ZipFile(output_dir / "Riftbound-windows-x64.zip", "w", zipfile.ZIP_DEFLATED) as archive:
            archive.write(executable, executable.name)
            for path in sorted(runtime.rglob("*")):
                if path.is_file():
                    archive.write(path, path.relative_to(output_dir))
    # A working directory outside the checkout proves the packaged catalog path.
    boot = run([executable, "--headless", "--verbose", "--max-fps", "60", "--quit-after", "600", "--",
                "--riftbound-ephemeral-session"], logs / "exported-boot.log", timeout=90, cwd=tools_dir)
    if "Client booted." not in boot or not re.search(r"Official catalog loaded: [1-9]\d* cards\.", boot):
        print(boot)
        raise RuntimeError("Exported native client or bundled catalog did not initialize")
    if platform == "darwin":
        verify_macos_sigterm_shutdown(executable, logs / "exported-sigterm.log", tools_dir)
    print(f"{platform}: exported native client and bundled catalog initialized successfully.")


if __name__ == "__main__":
    main()
