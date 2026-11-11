# Changelog

## 1.0.0 — first release

Prepared on 2026-11-07. Public version/tag: **1.0.0 / v1.0.0**; assembly/file version:
**1.0.0.0**. Earlier numbered folders were internal iterations, not public releases.

- Original Windows WPF application preserved with an English UI.
- Avalonia desktop implementation for Windows x64, macOS Apple Silicon/Intel and Linux x64.
- Four categories, 131 item records and 14 raw statistics per record, with original artwork.
- Name search, light/dark themes and the original recording with audio controls.
- Copy/paste full rows, multi-cell batch edits, grouped Undo and Reset.
- Linked Original/Edited Hex panes with bookmarks, tooltips and two-way navigation.
- Save to a new copy while preserving the opened file and non-statistic bytes.
- Synthetic core/UI verification and four-target native CI configuration.
- Self-contained archives, macOS app bundles, Linux launcher and SHA-256 checksums.
- MIT license for original project code; third-party media retains separate rights.

Supported format: PTBD/41, PTDV/53, PTTR/22, PTWG/15, with 14 little-endian u32 fields
per record. Unknown layouts are rejected. No blanket game-update compatibility or
in-game validation is claimed. Native macOS/Linux results are pending unless listed
as completed in [VERIFICATION.md](VERIFICATION.md).
