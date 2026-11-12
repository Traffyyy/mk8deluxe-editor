# mk8deluxe editor



I created **mk8deluxe editor** to edit statistics in Mario Kart 8 Deluxe `Performance.bin` files. Current download: **Beta 2**. This is a prerelease, not a fully validated stable release.

The original Windows WPF application is preserved in `Source/`. A separate Avalonia
application in `CrossPlatform/` provides Windows x64, macOS Apple Silicon/Intel and
Linux x64 build targets. Automated checks have passed on Linux and macOS; interactive use and audible playback still need broader testing. See
[platform support](docs/PLATFORM-SUPPORT.md) and [verification](VERIFICATION.md).

I tested the editor with **Mario Kart 8 Deluxe 3.0.5**. Other game versions are unverified. File-layout validation is not game-version detection.

**Start here: [step-by-step user guide](docs/USER-GUIDE.md).** Includes editing, copying, batch changes, Hex, saving and platform-specific shortcuts.

## Features

- Four item categories: 41 vehicle bodies, 53 characters, 22 wheels and 15 gliders.
- Fourteen raw integer statistics per item, with artwork and name search.
- Copy all statistics between items and apply a value to selected numeric cells.
- Undo for single edits and complete batch operations; Reset to the opened values.
- Linked Original/Edited Hex views, bookmarks, tooltips and changed-byte outlines.
- Two-way navigation between statistics and their bytes.
- Light/dark themes and the original bundled music with playback/volume controls.
- Save to the opened file, or use Save As; overwrites retain a recoverable backup.

## Required game file

You need your own **`Performance.bin`, extracted from the RomFS of Mario Kart 8 Deluxe**. Use the RomFS for the game version you intend to modify; my tests used **3.0.5**. In the extracted RomFS, look in **`Common/Mush/Performance.bin`**.

The editor opens the extracted `Performance.bin` directly. It does not open a ROM/container or extract RomFS for you. The file is not included with the editor. Keep an untouched backup before editing.

## Download and start

Download available packages from [Releases](https://github.com/Traffyyy/mk8deluxe-editor/releases).
Choose the archive matching your operating system and processor, then extract it fully.
Keep the entire extracted folder together, including `Audio/reference.wav`.

| Package | Start |
| --- | --- |
| Cross-platform Windows x64 | `Mk8Editor.Desktop.exe`; .NET runtime included |
| macOS Apple Silicon (`osx-arm64`) | `mk8deluxe editor.app` |
| macOS Intel (`osx-x64`) | `mk8deluxe editor.app` |
| Linux x64 (`linux-x64`) | Run `./mk8deluxe-editor` from the extracted folder |

The macOS packages target macOS 15 or newer; Linux validation targets Ubuntu 24.04.
See [platform support](docs/PLATFORM-SUPPORT.md) for dependencies and verification limits.
The Windows executables have no publisher certificate. Native macOS packages use an
ad-hoc signature, with no Developer ID certificate or Apple notarization. SHA-256
checksums detect changed downloads; they do not authenticate the publisher.

No game binary, ROM, encryption key, emulator or mod is included. Supply your own
supported `Performance.bin`. The editor does not need a network connection.

## Use

Open a file with the folder button or Ctrl+O, select a category, then edit a numeric
cell. Ctrl+Z undoes an edit. Reset restores the opened values and clears Undo history.
Save to the opened file with the disk button or Ctrl/Command+S. Use Ctrl/Command+Shift+S for Save As.

Values are raw unsigned 32-bit integers (0–4294967295), not the bars in the game menu.
An accepted value is not a promise of meaningful or stable gameplay behavior.

Ctrl/Command+C copies a row's 14 statistics. Select destination cells and use
Ctrl/Command+V to paste into their rows. Ctrl+click or Shift+click selects numeric
cells; F2 opens a dialog to set selected fields to one value. The actions menu provides
these commands too. The shortcuts are expressed as Ctrl throughout this README;
use the commands shown by the application on your platform.

Clipboard order: WG, AC, ON, OF, MT, SL, SW, SA, SG, TL, TW, TA, TG, IV.
Full-item paste accepts exactly 14 nonnegative whole numbers separated by whitespace.

Original Hex retains the snapshot captured at open; Edited includes unsaved changes.
Saving a copy does not move that baseline. Hex is an inspection/navigation view:
headers and non-statistic bytes remain outside the editable fields. The comparison
is implemented in this application and does not embed ImHex.

The interface is English; native file dialogs follow the operating system. Music
starts at 35% and loops. Preferences are not persisted between runs.

## Supported layout

The parser requires PTBD/41, PTDV/53, PTTR/22 and PTWG/15 in that order, with
14 little-endian u32 fields per record. Section addresses are read from the file;
unknown layouts are rejected. Compatibility with every game update is not claimed.
The final character entry is Mii (DLC costumes), using a representative image.

## Source and licensing

I release the original project code under the [MIT license](LICENSE).
The MIT grant excludes Nintendo artwork, the icon derived from that artwork, the
bundled recording and other third-party components. I have included the existing media as-is; this does not establish permission from their
rights holders. See [license scope](LICENSE-STATUS.md) and
[third-party notices](THIRD-PARTY-NOTICES.md).

See [building](BUILDING.md), [changelog](CHANGELOG.md) and
[contributing](CONTRIBUTING.md). This project is not affiliated with or endorsed by Nintendo.

## Earlier Windows interface previews

These show the earlier WPF interface. Current downloads use the portable interface; these are historical previews.

![Statistics in light mode](docs/images/statistics-light.png)

![Statistics in dark mode](docs/images/statistics-dark.png)

![Linked Hex comparison](docs/images/hex-comparison-dark.png)
