# Building and verification

## Original Windows application

`Source/` is the preserved C#/WPF implementation for Windows x64 and .NET Framework 4.8.
The installed 64-bit Framework compiler and WPF assemblies are sufficient; its
build script performs no NuGet restore or network download.

```powershell
& .\Source\build.ps1
```

Alternatively run `Source/build.cmd`. Output goes to `App/`. Keep the executable,
`.exe.config` and `Audio` directory together. The script embeds the catalog, XAML,
131 PNGs and original icon and copies the original WAV. Compiler response files
avoid Windows command-line length limits. The script builds the test runner but
does **not** run it.

## Cross-platform application

Install .NET SDK **10.0.403** and Python **3.11+**. The Avalonia app uses the shared
original core sources; no second copy of the format logic is maintained. NuGet
restore requires a network connection the first time. Packages are pinned in the
project and generated lock files where available.

```sh
dotnet run --project CrossPlatform/Mk8Editor.Tests --configuration Release
dotnet publish CrossPlatform/Mk8Editor.Desktop --configuration Release --runtime linux-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false --output artifacts/publish/linux-x64
python scripts/package.py --publish artifacts/publish/linux-x64 --output artifacts/packages --rid linux-x64 --version 1.0.0
python -m unittest discover -s scripts -p test_package.py -v
```

Replace `linux-x64` with `win-x64`, `osx-arm64` or `osx-x64` as appropriate. Cross-publishing
is possible but is not native execution verification. Build native macOS packages
on the matching Mac runner and add `--codesign-adhoc` to the package command. This
seals the `.app` and verifies its local ad-hoc signature; it does not use a signing
identity or notarize the application.

The packaging script verifies the runtime identifier, apphost/core-runtime machine
types, required files and original icon PNG. Archives preserve Unix executable
modes. macOS bundles contain `Contents/Info.plist`, `MacOS/` and the derived original
`Resources/App.icns`; Linux includes a launcher and optional desktop entry. Each
archive has a SHA-256 sidecar. Neither checksums nor header checks establish trust
in a publisher or prove that the program runs correctly.

## Synthetic fixtures and UI checks

The portable test runner generates a synthetic 12,332-byte file from the documented
layout; no game file is committed. It runs the existing 52-check core suite and
platform-specific checks for Unicode paths and, on Unix, symbolic-link protection.
To create a separate fixture, choose a path that does not yet exist:

```sh
dotnet run --project CrossPlatform/Mk8Editor.Tests --configuration Release -- --write-fixture fixture.bin
```

Run the published executable with `--verify fixture.bin verification-output`.
For macOS the executable is inside the bundle at
`mk8deluxe editor.app/Contents/MacOS/Mk8Editor.Desktop`; Linux uses
`Mk8Editor.Desktop`, and Windows uses `Mk8Editor.Desktop.exe`. Set the environment
variable `SDL_AUDIODRIVER=dummy` for unattended checks. This path verifies actual
Avalonia controls in a headless backend and audio initialization with a silent
driver; it is not an interactive desktop or audible playback test.

Original WPF checks use the same fixture:

```powershell
& .\App\Mk8Editor.Tests.exe .\fixture.bin
$process = Start-Process -FilePath '.\App\mk8deluxe editor.exe' -ArgumentList '--verify fixture.bin Verification' -Wait -PassThru
if ($process.ExitCode -ne 0) { throw 'WPF verification failed' }
Get-Content .\Verification\ui-verification.txt
```

The original suite assumes fields at 0x1354, 0x1C5C, 0x2804 and 0x2CE4. It is not a
compatibility test for arbitrary layouts. UI outputs must be checked only after
the process exits. Tests preserve their supplied fixture and use temporary copies.

## Continuous integration

`.github/workflows/build.yml` uses macOS 15 ARM64, macOS 15 Intel, Ubuntu 24.04 x64
and Windows Server 2022 x64. It runs core/packaging tests, publishes on each target,
runs headless UI checks and uploads archives/checksums and verification logs. A
separate Windows job builds and checks the original WPF app. CI results must be
observed before calling a native target verified; see [VERIFICATION.md](VERIFICATION.md).
