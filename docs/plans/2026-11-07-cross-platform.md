# First public release: Windows, macOS and Linux

User-authorized scope: publish Traffyyy/mk8deluxe-editor publicly; MIT for project
code; retain existing artwork and music per latest explicit instruction. Asset
notices remain separate and do not claim redistribution permissions.

Architecture: keep the existing WPF Windows app. Add an Avalonia desktop app for
macOS arm64/x64 and Linux x64, sharing the tested Core source and catalog. Retain
the table, 14 fields, clipboard/batch edits, Undo, save-copy protections, search,
themes, music, item pictures and paired bookmarked Hex with synchronized scroll.
Use .NET 10, pinned NuGet packages and self-contained per-RID publication.

- [ ] Add cross-platform Core project and synthetic fixture regression coverage.
- [ ] Port desktop UI with native pickers/clipboard and keyboard shortcuts.
- [ ] Add portable audio backend with pause/resume/volume/loop.
- [ ] Verify core operations and headless UI without touching the desktop.
- [ ] Build macOS bundles and Linux archive; verify executable architecture/modes.
- [ ] Preserve the tested Windows package, add CI and English build/install docs.
- [ ] Publish source and tagged release using verified GitHub credentials.
- [ ] Check remote commit/assets and report native test coverage honestly.

Validation priorities: never change original game data; byte range validation;
atomic batch/Undo; focus-independent copying; exact statistic-to-Hex mapping;
linked scrolling; missing audio device handled without editor failure; correct
case-sensitive paths on Linux; executable modes in Unix packages. macOS signing
and notarization are not claimed without the user's actual certificate.
