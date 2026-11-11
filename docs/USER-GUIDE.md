# User guide — public beta

Tested game version: **Mario Kart 8 Deluxe 3.0.5**, as reported by Traffy. This does not certify every edit, item mapping or operating system. Other game versions are unverified. The parser checks the binary layout; it does not independently detect the game's version.

## First edit

1. Download your platform's archive from [the release](https://github.com/Traffyyy/mk8deluxe-editor/releases/tag/v1.0.0), extract the whole folder, and keep the Audio folder beside the executable. Keep a backup of your own Performance.bin.
2. Start the application. Click the folder icon and choose your Performance.bin. No game files are bundled.
3. Select Karts, Characters, Gliders or Wheels. Search for an item by name. The numbers come from the opened file, including any previous modifications.
4. Double-click a statistic or select it and press F2. Enter a whole number. The portable application uses an edit dialog. Green/pink cells indicate increases/decreases relative to the file you opened.
5. Use Undo to reverse an operation. A batch operation is undone as one operation. Reset restores the opened values and clears Undo history.
6. Click the disk icon and choose a **new filename**. The editor refuses to overwrite the opened file or an existing destination. Reopen the exported copy to inspect it before using it in your mod setup. Exporting does not install a mod or modify the active game folder.

## Copy, paste and batch edits

Select a numeric cell in the source item. Open its right-click menu or the three-dot actions menu and choose **Copy all 14 statistics**. Select cells in the destination item(s), then choose **Paste to selected items**. This replaces all fourteen statistics of each destination item, not just the highlighted columns.

To set only selected statistics to a common value, select cells using Ctrl-click (Command-click on Mac) and Shift-click, then choose **Edit selected statistics**. Shift selection forms a rectangle in the portable application. Paste accepts exactly fourteen nonnegative integers separated by whitespace; malformed input is rejected before edits are applied.

| Action | Original Windows application | Portable Linux/macOS application |
| --- | --- | --- |
| Open | Ctrl+O | Ctrl/Command+O |
| Save a new copy | Ctrl+Shift+S | Ctrl/Command+S |
| Undo | Ctrl+Z | Ctrl/Command+Z |
| Copy all 14 stats | Ctrl+Shift+C | Ctrl/Command+C |
| Paste into selected items | Ctrl+Shift+V | Ctrl/Command+V |
| Set selected statistics | Ctrl+Enter | F2 or Enter |
| Show statistic in Hex | Ctrl+H | Ctrl/Command+H |

Use the menu commands if your OS reserves a shortcut. Text fields retain normal text-copy shortcuts.

## Statistics

| Code | Meaning |
| --- | --- |
| WG | Weight |
| AC | Acceleration |
| ON / OF | On-road / off-road traction |
| MT | Mini-turbo |
| SL / SW / SA / SG | Ground / water / anti-gravity / air speed |
| TL / TW / TA / TG | Ground / water / anti-gravity / air handling |
| IV | Invincibility |

These are raw unsigned 32-bit values, not the game's displayed bars. The accepted range is 0–4294967295; that is a storage limit, not a recommended gameplay range. Test small changes first. The editor does not rebalance the file automatically.

## Hex comparison

The left pane retains the original snapshot; the right pane shows current edits to that same file. Scrolling is linked. Alternating bookmark colors identify records; hover over statistics to see the item and field information. Use **Show statistic in Hex** from a selected table cell to reach its bytes. In the portable application, double-click a bookmarked statistic to return to the table.

Headers, padding and other non-statistic data are not editable through these controls. Saving a copy does not replace the original snapshot used by the comparison.

## Appearance and audio

The sun/moon button switches theme. The speaker button toggles music: green is enabled, red is disabled. Hover over it to adjust volume. The song title is shown only while enabled. Keep Audio/reference.wav with the application. The Windows hosted-runner audio test currently fails; audio on other machines needs further testing.

## Reporting a problem

Open a [GitHub issue](https://github.com/Traffyyy/mk8deluxe-editor/issues) with application/package version, OS and architecture, game version, steps, expected result, actual result and the exact error message. Include a screenshot if useful, removing personal paths. Do not upload game binaries, ROMs or keys. State whether the problem affects opening, editing, export, in-game behavior or audio.

This is a beta. Automated checks do not establish complete gameplay compatibility. See [verification](../VERIFICATION.md) for evidence and known limits.
