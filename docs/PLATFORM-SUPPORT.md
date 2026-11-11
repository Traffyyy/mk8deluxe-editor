# Platform support

## Release targets and validation status

| Target | Build/package | Native validation as of 2026-11-07 |
| --- | --- | --- |
| Original Windows x64 WPF | .NET Framework 4.8; portable ZIP | Core and WPF off-screen checks passed on the preparation host |
| Windows x64 Avalonia | Self-contained .NET 10 ZIP | Pending final application checks / native CI |
| macOS 15+ Apple Silicon | `osx-arm64` `.app` in ZIP | Native CI and interactive checks pending |
| macOS 15+ Intel | `osx-x64` `.app` in ZIP | Native CI and interactive checks pending |
| Ubuntu 24.04 x64 | `linux-x64` tar.gz with launcher | Native CI and interactive checks pending |

These are the configured release targets, not a claim that every target has already
passed. Read [VERIFICATION.md](../VERIFICATION.md) and the specific CI run when
assessing a package. No Linux ARM64, Windows ARM64, universal Mac binary, mobile
build, installer or automatic updater is provided.

## Windows

Extract the whole archive. The original `mk8deluxe editor.exe` requires the installed
.NET Framework 4.8 and its adjacent configuration/audio files. The Avalonia
`Mk8Editor.Desktop.exe` includes its own .NET runtime. Neither has a publisher
code-signing certificate. CI uses Windows Server 2022; clean Windows 10/11 desktop
installation and interactive usability checks remain separate manual checks.

## macOS

Select Apple Silicon (`osx-arm64`) or Intel (`osx-x64`) to match your Mac. Extract
the ZIP and open `mk8deluxe editor.app`; keep its contents intact. The original icon
is converted directly from the existing ICO PNG payloads to ICNS. The app contains
its runtime and WAV. Bundle minimum OS is macOS 15.0.

Native CI applies a local ad-hoc signature and verifies the bundle. There is no
Developer ID certificate or Apple notarization; Gatekeeper may block a downloaded
application. An ad-hoc signature is not publisher authentication. Follow macOS's
per-application approval flow only after reviewing the source/download you trust.
No global security-setting change is required or recommended by this project.

## Linux

Extract the tar.gz and run `./mk8deluxe-editor` from its folder. Execute permissions
are stored in the archive. Keep all native libraries and `Audio/reference.wav`.
The .NET runtime is bundled, but desktop/system libraries are supplied by Linux.
Ubuntu 24.04 is the CI target. Avalonia uses X11; a Wayland desktop needs XWayland
for this configuration. A display session and an available system audio backend
are needed for normal interactive use.

Avalonia lists `libx11-6`, `libice6`, `libsm6` and `libfontconfig1` as Linux desktop
dependencies; .NET/native rendering libraries also need normal distribution runtime
libraries. See the [official Avalonia Linux deployment guide](https://docs.avaloniaui.net/docs/deployment/linux).
Audio failure is reported without preventing editing. CI's SDL dummy driver tests
initialization and controls but does not establish audible playback.

The optional `.desktop` entry expects `mk8deluxe-editor` on `PATH` and its PNG in the
desktop icon search path; the archive does not install those automatically.

## What automation does and does not prove

The native CI runner matrix matches [GitHub's runner labels](https://docs.github.com/en/actions/how-tos/write-workflows/choose-where-workflows-run/choose-the-runner-for-a-job).
Core tests cover format/edit/save behavior; headless checks exercise Avalonia controls;
packaging tests cover bundle layout, architecture metadata, icons, permissions and
checksums. Native interactive file dialogs, clipboard integration, display scaling,
real audio hardware, Gatekeeper approval and gameplay require separate checks.
