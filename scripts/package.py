#!/usr/bin/env python3
"""Package a self-contained desktop publish directory; Python 3.11+, stdlib only."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import plistlib
import re
import shutil
import stat
import struct
import subprocess
import sys
import tarfile
import tempfile
import zipfile


ROOT = Path(__file__).resolve().parents[1]
APP = "Mk8Editor.Desktop"
NAME = "mk8deluxe editor"
RIDS = ("osx-arm64", "osx-x64", "linux-x64", "win-x64")


def png_icons(icon_path):
    """Extract the original PNG payloads from this project's multi-size ICO."""
    data = icon_path.read_bytes()
    if len(data) < 6 or struct.unpack_from("<HH", data) != (0, 1):
        raise ValueError("The icon is not a Windows ICO file")
    icons = {}
    for index in range(struct.unpack_from("<H", data, 4)[0]):
        width, height, _, _, _, _, size, offset = struct.unpack_from(
            "<BBBBHHII", data, 6 + 16 * index
        )
        width, height = width or 256, height or 256
        png = data[offset:offset + size]
        if width == height and len(png) == size and png.startswith(b"\x89PNG\r\n\x1a\n"):
            icons[width] = png
    if 256 not in icons:
        raise ValueError("The source ICO must contain its original 256px PNG image")
    return icons


def validate_architecture(path, rid):
    """Reject mismatched apphosts/runtimes; this is not a binary integrity test."""
    with path.open("rb") as binary:
        data = binary.read(4096)
    valid = False
    try:
        if rid.startswith("win-") and data[:2] == b"MZ":
            offset = struct.unpack_from("<I", data, 60)[0]
            valid = (data[offset:offset + 4] == b"PE\0\0" and
                     struct.unpack_from("<H", data, offset + 4)[0] == 0x8664)
        elif rid.startswith("linux-"):
            valid = (data[:6] == b"\x7fELF\x02\x01" and
                     struct.unpack_from("<H", data, 18)[0] == 62)
        elif rid.startswith("osx-"):
            expected = 0x0100000C if rid.endswith("arm64") else 0x01000007
            valid = struct.unpack_from("<II", data, 0) == (0xFEEDFACF, expected)
    except struct.error:
        pass
    if not valid:
        raise ValueError(f"Binary architecture does not match {rid}: {path.name}")


def write_icns(icon_path, destination):
    icons = png_icons(icon_path)
    chunks = []
    for size, kind in ((16, b"icp4"), (32, b"icp5"), (64, b"icp6"),
                       (128, b"ic07"), (256, b"ic08")):
        if size in icons:
            png = icons[size]
            chunks.append(kind + struct.pack(">I", len(png) + 8) + png)
    body = b"".join(chunks)
    destination.write_bytes(b"icns" + struct.pack(">I", len(body) + 8) + body)


def validate_publish(publish, rid):
    executable = APP + (".exe" if rid.startswith("win-") else "")
    coreclr = "coreclr.dll" if rid.startswith("win-") else (
        "libcoreclr.dylib" if rid.startswith("osx-") else "libcoreclr.so"
    )
    for relative in (executable, f"{APP}.dll", f"{APP}.deps.json",
                     f"{APP}.runtimeconfig.json", coreclr, "Audio/reference.wav"):
        if not (publish / relative).is_file():
            raise ValueError(f"Incomplete self-contained publish: missing {relative}")
    deps = json.loads((publish / f"{APP}.deps.json").read_text(encoding="utf-8-sig"))
    if not deps.get("runtimeTarget", {}).get("name", "").endswith("/" + rid):
        raise ValueError(f"The publish dependency manifest does not target {rid}")
    validate_architecture(publish / executable, rid)
    validate_architecture(publish / coreclr, rid)
    if any(path.is_symlink() for path in publish.rglob("*")):
        raise ValueError("Publish input contains symbolic links; supply regular published files")
    return executable


def archive_mode(path, executables):
    return 0o755 if path.is_dir() or path in executables else 0o644


def write_zip(staging, destination, executables):
    # Explicit Unix mode metadata is needed when packaging on Windows for macOS.
    with zipfile.ZipFile(destination, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(staging.rglob("*")):
            relative = path.relative_to(staging).as_posix()
            directory = path.is_dir()
            info = zipfile.ZipInfo(relative + ("/" if directory else ""))
            info.create_system = 3
            mode = archive_mode(path, executables)
            info.external_attr = ((stat.S_IFDIR if directory else stat.S_IFREG) | mode) << 16
            if directory:
                info.external_attr |= 0x10
            info.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(info, b"" if directory else path.read_bytes())


def write_tar(staging, destination, executables):
    with tarfile.open(destination, "w:gz", format=tarfile.PAX_FORMAT) as archive:
        for path in sorted(staging.rglob("*")):
            info = archive.gettarinfo(str(path), path.relative_to(staging).as_posix())
            info.mode = archive_mode(path, executables)
            info.uid = info.gid = 0
            info.uname = info.gname = ""
            if path.is_file():
                with path.open("rb") as source:
                    archive.addfile(info, source)
            else:
                archive.addfile(info)


def package(publish, output, rid, version="1.0.0", icon=None, codesign_adhoc=False):
    publish, output = Path(publish).resolve(), Path(output).resolve()
    if rid not in RIDS:
        raise ValueError(f"Unsupported runtime identifier: {rid}")
    if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", version):
        raise ValueError("Version must have three numeric components, for example 1.0.0")
    if codesign_adhoc and (sys.platform != "darwin" or not rid.startswith("osx-")):
        raise ValueError("Ad-hoc signing requires macOS and an osx runtime identifier")
    executable = validate_publish(publish, rid)
    icon = Path(icon) if icon else ROOT / "Source/Mk8Editor.App/App.ico"
    png_icons(icon)  # Validate before creating any output.
    output.mkdir(parents=True, exist_ok=True)
    stem = f"mk8deluxe-editor-{version}-{rid}"
    extension = ".tar.gz" if rid.startswith("linux-") else ".zip"
    destination = output / (stem + extension)
    with tempfile.TemporaryDirectory(prefix="mk8-package-", dir=output) as temporary:
        temporary = Path(temporary)
        staging = temporary / "staging"
        staging.mkdir()
        container = staging / stem
        container.mkdir()
        if rid.startswith("osx-"):
            contents = container / f"{NAME}.app/Contents"
            payload = contents / "MacOS"
            resources = contents / "Resources"
            resources.mkdir(parents=True)
            plist = {
                "CFBundleName": NAME,
                "CFBundleDisplayName": NAME,
                "CFBundleIdentifier": "io.github.Traffyyy.mk8deluxe-editor",
                "CFBundleExecutable": executable,
                "CFBundlePackageType": "APPL",
                "CFBundleShortVersionString": version,
                "CFBundleVersion": version,
                "CFBundleIconFile": "App.icns",
                "NSHighResolutionCapable": True,
                "LSMinimumSystemVersion": "15.0",
            }
            (contents / "Info.plist").write_bytes(plistlib.dumps(plist))
            write_icns(icon, resources / "App.icns")
        else:
            payload = container
        shutil.copytree(publish, payload, dirs_exist_ok=True)
        executables = {payload / executable}
        if rid.startswith("linux-"):
            launcher = container / "mk8deluxe-editor"
            launcher.write_text(
                '#!/bin/sh\nset -eu\n'
                'APP_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)\n'
                'exec "$APP_DIR/Mk8Editor.Desktop" "$@"\n', encoding="utf-8", newline="\n"
            )
            executables.add(launcher)
            (container / "mk8deluxe-editor.png").write_bytes(png_icons(icon)[256])
            (container / "mk8deluxe-editor.desktop").write_text(
                "[Desktop Entry]\nType=Application\nName=mk8deluxe editor\n"
                "Comment=Edit supported Mario Kart 8 Deluxe Performance.bin files\n"
                "Exec=mk8deluxe-editor\nTryExec=mk8deluxe-editor\n"
                "Icon=mk8deluxe-editor\nTerminal=false\nCategories=Utility;\n",
                encoding="utf-8", newline="\n"
            )
            (container / "LINUX-START.txt").write_text(
                "Extract the entire archive and run ./mk8deluxe-editor from this folder.\n"
                "Keep all libraries and Audio/reference.wav with the executable.\n"
                "The optional .desktop entry expects this folder on PATH and the PNG\n"
                "installed as mk8deluxe-editor in your desktop's icon search path.\n"
                "See PLATFORM-SUPPORT.md for dependencies and native test status.\n",
                encoding="utf-8", newline="\n"
            )
        for name in ("LICENSE", "THIRD-PARTY-NOTICES.md", "README.md"):
            source = ROOT / name
            if source.is_file():
                shutil.copy2(source, container / name)
        support = ROOT / "docs/PLATFORM-SUPPORT.md"
        if support.is_file():
            shutil.copy2(support, container / support.name)
        for path in executables:
            path.chmod(0o755)
        if codesign_adhoc:
            bundle = payload.parent.parent
            # Sign nested native code before sealing the .app. This is an ad-hoc
            # signature, not Developer ID signing or Apple notarization.
            for path in sorted(payload.rglob("*")):
                if path.is_file() and path.suffix == ".dylib":
                    subprocess.run(["codesign", "--force", "--sign", "-", str(path)], check=True)
            subprocess.run(["codesign", "--force", "--deep", "--sign", "-", str(bundle)], check=True)
            subprocess.run(["codesign", "--verify", "--deep", "--strict", str(bundle)], check=True)
        archive = temporary / destination.name
        (write_tar if rid.startswith("linux-") else write_zip)(staging, archive, executables)
        os.replace(archive, destination)
    with destination.open("rb") as archive_file:
        digest = hashlib.file_digest(archive_file, "sha256").hexdigest()
    destination.with_name(destination.name + ".sha256").write_text(
        f"{digest}  {destination.name}\n", encoding="ascii"
    )
    return destination


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--publish", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--rid", required=True, choices=RIDS)
    parser.add_argument("--version", default="1.0.0")
    parser.add_argument("--icon", type=Path)
    parser.add_argument("--codesign-adhoc", action="store_true",
                        help="Apply and verify an ad-hoc signature on native macOS")
    args = parser.parse_args()
    try:
        print(package(args.publish, args.output, args.rid, args.version, args.icon, args.codesign_adhoc))
    except (OSError, ValueError, struct.error, subprocess.CalledProcessError) as error:
        parser.exit(1, f"Packaging failed: {error}\n")


if __name__ == "__main__":
    main()

