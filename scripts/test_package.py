"""Archive-contract tests with synthetic publish files; these do not launch the app."""

import hashlib
import json
from pathlib import Path
import plistlib
import struct
import tarfile
import tempfile
import unittest
import zipfile

from package import APP, ROOT, package, png_icons


class PackagingTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.publish = self.root / "publish"
        self.publish.mkdir()

    def fixture(self, rid):
        executable = APP + (".exe" if rid.startswith("win-") else "")
        coreclr = "coreclr.dll" if rid.startswith("win-") else (
            "libcoreclr.dylib" if rid.startswith("osx-") else "libcoreclr.so"
        )
        header = bytearray(256)
        if rid.startswith("win-"):
            header[:2] = b"MZ"
            struct.pack_into("<I", header, 60, 128)
            header[128:132] = b"PE\0\0"
            struct.pack_into("<H", header, 132, 0x8664)
        elif rid.startswith("linux-"):
            header[:6] = b"\x7fELF\x02\x01"
            struct.pack_into("<H", header, 18, 62)
        else:
            struct.pack_into("<II", header, 0, 0xFEEDFACF,
                             0x0100000C if rid.endswith("arm64") else 0x01000007)
        for name in (executable, coreclr):
            (self.publish / name).write_bytes(header)
        (self.publish / (APP + ".dll")).write_bytes(b"synthetic managed assembly")
        (self.publish / (APP + ".deps.json")).write_text(json.dumps({
            "runtimeTarget": {"name": ".NETCoreApp,Version=v10.0/" + rid}
        }))
        (self.publish / (APP + ".runtimeconfig.json")).write_text("{}")
        (self.publish / "Audio").mkdir()
        (self.publish / "Audio/reference.wav").write_bytes(b"RIFF synthetic fixture")

    def test_mac_bundles_have_plist_original_icon_music_and_unix_execute_mode(self):
        for rid in ("osx-arm64", "osx-x64"):
            with self.subTest(rid=rid):
                if not (self.publish / "Audio").exists():
                    self.fixture(rid)
                else:
                    (self.publish / (APP + ".deps.json")).write_text(json.dumps({
                        "runtimeTarget": {"name": ".NETCoreApp,Version=v10.0/" + rid}
                    }))
                    header = bytearray((self.publish / APP).read_bytes())
                    struct.pack_into("<I", header, 4, 0x01000007)
                    for name in (APP, "libcoreclr.dylib"):
                        (self.publish / name).write_bytes(header)
                result = package(self.publish, self.root / "out", rid)
                base = f"mk8deluxe-editor-1.0.0-{rid}/mk8deluxe editor.app/Contents/"
                with zipfile.ZipFile(result) as archive:
                    info = plistlib.loads(archive.read(base + "Info.plist"))
                    self.assertEqual(info["CFBundleExecutable"], APP)
                    self.assertEqual(info["CFBundleShortVersionString"], "1.0.0")
                    executable = archive.getinfo(base + "MacOS/" + APP)
                    self.assertEqual(executable.create_system, 3)
                    self.assertEqual((executable.external_attr >> 16) & 0o777, 0o755)
                    self.assertEqual(archive.read(base + "MacOS/Audio/reference.wav"),
                                     b"RIFF synthetic fixture")
                    icns = archive.read(base + "Resources/App.icns")
                    self.assertEqual(icns[:4], b"icns")
                    self.assertEqual(struct.unpack_from(">I", icns, 4)[0], len(icns))
                    self.assertIn(png_icons(ROOT / "Source/Mk8Editor.App/App.ico")[256], icns)

    def test_linux_archive_has_executable_launcher_and_desktop_entry(self):
        self.fixture("linux-x64")
        result = package(self.publish, self.root / "out", "linux-x64")
        base = "mk8deluxe-editor-1.0.0-linux-x64/"
        with tarfile.open(result) as archive:
            for name in (APP, "mk8deluxe-editor"):
                self.assertEqual(archive.getmember(base + name).mode, 0o755)
            self.assertEqual(archive.getmember(base + "Audio/reference.wav").mode, 0o644)
            self.assertIn(b"Exec=mk8deluxe-editor\n", archive.extractfile(
                base + "mk8deluxe-editor.desktop").read())
            self.assertIn(b'"$@"', archive.extractfile(base + "mk8deluxe-editor").read())

    def test_windows_archive_and_sha256(self):
        self.fixture("win-x64")
        result = package(self.publish, self.root / "out", "win-x64")
        with zipfile.ZipFile(result) as archive:
            self.assertIn("mk8deluxe-editor-1.0.0-win-x64/" + APP + ".exe", archive.namelist())
            self.assertIsNone(archive.testzip())
        checksum = result.with_name(result.name + ".sha256").read_text().split()[0]
        self.assertEqual(checksum, hashlib.sha256(result.read_bytes()).hexdigest())

    def test_missing_music_is_rejected(self):
        self.fixture("linux-x64")
        (self.publish / "Audio/reference.wav").unlink()
        with self.assertRaisesRegex(ValueError, "Audio/reference.wav"):
            package(self.publish, self.root / "out", "linux-x64")
        self.assertFalse((self.root / "out").exists())

    def test_wrong_runtime_is_rejected(self):
        self.fixture("osx-x64")
        with self.assertRaisesRegex(ValueError, "does not target osx-arm64"):
            package(self.publish, self.root / "out", "osx-arm64")

    def test_invalid_version_is_rejected(self):
        self.fixture("linux-x64")
        with self.assertRaisesRegex(ValueError, "Version"):
            package(self.publish, self.root / "out", "linux-x64", "../wrong")

    def test_binary_architecture_mismatch_is_rejected(self):
        self.fixture("osx-arm64")
        header = bytearray((self.publish / APP).read_bytes())
        struct.pack_into("<I", header, 4, 0x01000007)
        (self.publish / APP).write_bytes(header)
        with self.assertRaisesRegex(ValueError, "architecture"):
            package(self.publish, self.root / "out", "osx-arm64")
        self.assertFalse((self.root / "out").exists())

    def test_corrupt_runtime_is_rejected(self):
        self.fixture("linux-x64")
        (self.publish / "libcoreclr.so").write_bytes(b"not a binary")
        with self.assertRaisesRegex(ValueError, "architecture"):
            package(self.publish, self.root / "out", "linux-x64")

    def test_original_icon_has_valid_png_dimensions(self):
        icons = png_icons(ROOT / "Source/Mk8Editor.App/App.ico")
        self.assertIn(256, icons)
        for size, png in icons.items():
            self.assertEqual(png[12:16], b"IHDR")
            self.assertEqual(struct.unpack_from(">II", png, 16), (size, size))


if __name__ == "__main__":
    unittest.main(verbosity=2)
