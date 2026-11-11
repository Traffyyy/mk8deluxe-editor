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
