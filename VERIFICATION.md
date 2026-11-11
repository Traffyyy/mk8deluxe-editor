# Verification — version 1.0.0

Status recorded on 2026-11-07 (Europe/Rome). A configured workflow is not evidence
that it has run successfully. Native CI results remain pending until their completed
run is available.

## Original Windows WPF application

The preparation host previously passed the staged Framework build, all 52 core
checks and native off-screen WPF verification. The tests covered all 1,834 fields,
unchanged non-statistic bytes, save-copy protections, Undo/clipboard validation,
131 images, cell editing, menu focus, batch edits, Hex scrolling/navigation, themes,
icon geometry and bundled WAV playback at zero output volume.

The reference fixture hash before/after was
`602240ce35150e1bd0eda2c117b69b9dd13aa96b30dbe5c5148035fc9b817e0b`.
The supplied development files and 131 artwork hashes were previously verified
against the preparation snapshot/provenance manifest. The original application
logic remains preserved; publication metadata identifies Traffy / version 1.0.0.0.

## Cross-platform release work

- Packaging contract: **9 tests passed** on the Windows preparation host using
  synthetic native headers and the actual original ICO. Covers both Mac bundles,
  Linux/Windows archives, execute permissions, original 256px icon, music retention,
  SHA-256, incomplete inputs, mismatched runtime/machine types and invalid versions.
- Cross-platform core/UI build and native-run status: pending final verification.
- macOS ARM64/Intel and Ubuntu native CI, ad-hoc signing and interactive startup:
  pending. Do not infer success from source or archive creation alone.
- Four native CI jobs and an original-Windows job are defined to produce repeatable
  logs and packages. UI automation uses synthetic data and a headless backend;
  SDL's dummy driver does not prove audible output on real hardware.

No clean-machine installation, gameplay test, Developer ID signing, notarization,
or broad game-update compatibility is claimed. Windows executables are unsigned.
See [BUILDING.md](BUILDING.md) for commands and [platform support](docs/PLATFORM-SUPPORT.md)
for target-specific limits.

## Public build verification (2026-11-11)

GitHub Actions run 43074026091 built and passed core and headless UI checks on Linux x64. The extracted Linux launcher also passed. This is automated native testing, not an interactive user acceptance test or an audible speaker test.

The original Windows WPF build passed all 52 core checks on the GitHub runner, but its UI verification failed at bundled WAV playback. Its previous local Windows verification remains recorded above; the hosted-runner audio failure is unresolved. The published original Windows ZIP is the unchanged locally verified artifact.

macOS core and headless UI checks passed on Apple Silicon; packaging initially failed signing nested assemblies. A packaging fix is being verified in run 43074561667. Do not infer completed macOS package verification from the source build alone.

## Beta status — 2026-11-11

This release is now classified as a public beta/prerelease. Existing v1.0.0 tag, download filenames and binaries are retained so existing links and hashes remain valid; this is a release-status change, not a rebuilt binary.

Traffy reports testing with Mario Kart 8 Deluxe 3.0.5. This is the stated game test version, not a claim of exhaustive gameplay coverage. The parser validates structure rather than detecting the installed game version.

Run 43074902750 completed successfully for the portable desktop on macOS Intel, Apple Silicon, Linux x64 and Windows x64, including the applicable extracted-package checks. The original WPF Windows job still fails at WAV playback on the hosted runner. Earlier pending statements above are historical; this entry records the latest observed status. macOS is ad-hoc signed, not notarized. Interactive clean-machine and audible-output testing remains incomplete.
