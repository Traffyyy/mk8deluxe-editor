# Contributing

Original project code is MIT licensed. Keep the third-party media and dependency
notices with redistributed copies; the MIT grant does not relicense those assets.

For bug reports include the version, operating system and processor architecture,
steps to reproduce, expected/actual behavior and a screenshot if useful. Specify
whether you ran the original WPF app or the Avalonia app. For layout issues include
window size and display scaling. For parsing failures include the error, file size
and SHA-256 if available; do not upload game binaries, ROMs, keys or private mod folders.

Preserve the opened-file baseline and save-copy-only behavior. Keep format logic in
the shared Core sources and desktop-specific behavior in its application. The original
Windows implementation remains in `Source/`; changes to the separate desktop port
belong in `CrossPlatform/`. Keep readable names and English UI strings.

Add focused regression checks for functional bugs and test real focus/selection
transitions for UI bugs. Run the relevant core/UI checks and packaging tests from
[BUILDING.md](BUILDING.md). State which operating systems were actually tested;
cross-compilation and headless checks do not replace native interactive testing.

Include what changed, why and the checks performed in a pull request. Avoid unrelated
generated files, local absolute paths and third-party media additions without provenance.
