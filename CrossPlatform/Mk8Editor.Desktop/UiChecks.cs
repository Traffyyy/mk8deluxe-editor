using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mk8Editor.Core;

namespace Mk8Editor.Desktop;

/// <summary>Runs inside Avalonia's headless platform; never opens a desktop window or audio device.</summary>
internal static class UiChecks
{
    public static int Run(string[] args)
    {
        if (args.Length < 1) throw new ArgumentException("Usage: --verify <synthetic-Performance.bin> [report-directory]");
        string folder = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.Combine(Path.GetTempPath(), "mk8-ui-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var results = new List<string>();
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + label);
            results.Add("PASS: " + label); Console.WriteLine(results[^1]);
        }
        var window = new MainWindow(verification: true);
        window.Show(); Dispatcher.UIThread.RunJobs();
        Check(window.State.Session == null && window.Table.CellCount == 0, "Empty editor opens without game data");
        window.LoadForVerification(args[0]); Dispatcher.UIThread.RunJobs();
        Check(window.State.Records.Count == 131, "All 131 catalog items loaded");
        int[] expectedCounts = [41, 53, 15, 22];
        for (int category = 0; category < 4; category++)
        {
            window.ShowCategory(category); Dispatcher.UIThread.RunJobs();
            Check(window.Table.VisibleRowCount == expectedCounts[category] && window.Table.CellCount == expectedCounts[category] * 14,
                EditorState.Categories[category] + ": fourteen statistics for every item");
            Check(window.Table.GetVisualDescendants().OfType<Image>().Count() == expectedCounts[category],
                EditorState.Categories[category] + ": item images present");
        }
        window.ShowCategory(0);
        var state = window.State;
        var rows = state.VisibleRecords;
        int first = rows[0].Offset, last = rows[2].Offset + 8;
        uint original = state.Session!.GetValue(first);
        state.Select(first, false, false); state.Select(last, false, true);
        Check(state.SelectedOffsets.Count == 9, "Shift selection forms a three-by-three rectangle");
        state.Select(rows[3].Offset, true, false);
        Check(state.SelectedOffsets.Count == 10, "Control/Command adds a separate cell");
        state.SetSelected(uint.MaxValue); window.RefreshAfterEdit();
        Check(state.SelectedOffsets.All(offset => state.Session.GetValue(offset) == uint.MaxValue), "Batch editing accepts the complete unsigned 32-bit range");
        window.Undo();
        Check(state.SelectedOffsets.All(offset => state.Session.GetValue(offset) == state.Session.GetOriginalValue(offset)), "One Undo restores the entire batch");
        state.Select(first, false, false);
        string clipboard = state.CopyAll();
        Check(StatisticTransfer.Parse(clipboard).Length == 14, "Copy always contains all fourteen statistics");
        state.Select(rows[1].Offset, false, false); state.Select(rows[2].Offset, true, false);
        state.PasteAll(clipboard); window.RefreshAfterEdit();
        var expected = StatisticTransfer.Parse(clipboard);
        Check(new[] { rows[1], rows[2] }.All(record => Enumerable.Range(0, 14).All(i => state.Session.GetValue(record.Offset + i * 4) == expected[i])),
            "Paste applies all fourteen values to every selected item");
        byte[] beforeInvalid = state.Session.Serialize();
        bool rejected = false;
        try { state.PasteAll("1 2 invalid"); } catch (FormatException) { rejected = true; }
        Check(rejected && beforeInvalid.SequenceEqual(state.Session.Serialize()), "Invalid clipboard text cannot partially edit data");
        state.Session.Reset();
        state.Select(first + 12, false, false); window.JumpToHex(); Dispatcher.UIThread.RunJobs();
        Check(state.Category == 4 && window.Hex.IsVisible && window.Hex.SelectedByteOffset == first + 12, "Statistic-to-Hex jump selects the exact byte offset");
        window.Hex.SetScrollOffset(new Vector(0, 180)); Dispatcher.UIThread.RunJobs();
        Check(window.Hex.OriginalScrollOffset == window.Hex.EditedScrollOffset, "Original and edited Hex panes remain synchronized");
        Check(window.Hex.NavigateToByte(first + 5) && state.Category == 0 && state.ActiveOffset == first + 4, "Hex navigation returns to the matching statistic");
        Check(!window.Hex.NavigateToByte(0), "Header bytes cannot be edited as statistics");
        window.ToggleTheme(); Dispatcher.UIThread.RunJobs();
        Check(state.Dark && window.Table.CellCount == 41 * 14, "Dark theme preserves table content");
        window.ToggleTheme();
        state.Query = rows[0].Entry.Name; window.RefreshTable();
        Check(window.Table.VisibleRowCount > 0 && state.VisibleRecords.All(r => r.Entry.Name.Contains(state.Query, StringComparison.OrdinalIgnoreCase)), "Search filters names without changing the catalog");
        state.Query = "no-item-should-have-this-name"; window.RefreshTable();
        Check(window.Table.CellCount == 0, "Unmatched search safely shows no rows");
        state.Query = ""; window.RefreshTable();
        state.Select(first, false, false); state.SetSelected(original + 1); window.RefreshAfterEdit();
        string copy = Path.Combine(folder, "verified-copy-" + Guid.NewGuid().ToString("N") + ".bin");
        state.Session.SaveCopy(copy);
        Check(File.ReadAllBytes(copy).SequenceEqual(state.Session.Serialize()), "Saved copy matches the edited bytes");
        Check(File.ReadAllBytes(args[0]).SequenceEqual(state.Session.File.CopyOriginalBytes()), "Source file remains byte-for-byte unchanged");
        bool overwriteRejected = false;
        try { state.Session.SaveCopy(args[0]); } catch (UserFileException) { overwriteRejected = true; }
        Check(overwriteRejected, "Save refuses the opened source path");
        state.Session.Reset(); window.RefreshAfterEdit(); Dispatcher.UIThread.RunJobs();
        Check(window.Bounds.Width >= 960 && window.Table.Bounds.Height > 0, "Headless window and table complete layout");
        // Rendering is optional under Avalonia's mock drawing backend; layout and commands above are mandatory.
        using var frame = window.CaptureRenderedFrame();
        if (frame != null) { frame.Save(Path.Combine(folder, "portable-editor.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default); results.Add("Headless rendered image saved."); }
        results.Add("No native desktop window or audible audio was used. Native OS pickers/audio require platform smoke testing.");
        File.WriteAllLines(Path.Combine(folder, "ui-checks.txt"), results);
        window.Close();
        Console.WriteLine("PORTABLE UI CHECKS PASSED (" + results.Count(line => line.StartsWith("PASS:")) + ")");
        return 0;
    }
}

