#!/usr/bin/env python3
"""Verify the exported desktop runtime on its actual OS; no publishing or signing secrets."""
import hashlib
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
    with urllib.request.urlopen(RELEASE + name, timeout=120) as response, destination.open("wb") as output:
        shutil.copyfileobj(response, output)
    with destination.open("rb") as source:
        actual = hashlib.file_digest(source, "sha256").hexdigest()
    if actual != digest:
        raise RuntimeError(f"SHA-256 mismatch for {name}")
    return destination


def run(args, log, timeout=600, cwd=ROOT):
    print("Running", " ".join(map(str, args)), flush=True)
    with log.open("w", encoding="utf-8") as output:
        result = subprocess.run(list(map(str, args)), cwd=cwd, stdout=output, stderr=subprocess.STDOUT, timeout=timeout)
    text = log.read_text(encoding="utf-8", errors="replace")
    if result.returncode or re.search(r"^ERROR:", text, re.M):
        print(text)
        raise RuntimeError(f"Command failed; see {log.name}")
    return text


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
    run([editor, "--headless", "--editor", "--path", PROJECT, "--import"], logs / "import.log")
    target = "macOS" if platform == "darwin" else "Windows Desktop"
    output_dir = PROJECT / "exports" / ("macos" if platform == "darwin" else "windows")
    output_dir.mkdir(parents=True, exist_ok=True)
    package = output_dir / ("Riftbound.zip" if platform == "darwin" else "Riftbound.exe")
    run([editor, "--headless", "--path", PROJECT, "--export-release", target, package], logs / "export.log")
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
    print(f"{platform}: exported native client and bundled catalog initialized successfully.")


if __name__ == "__main__":
    main()
