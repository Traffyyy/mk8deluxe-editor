using Mk8Editor.Core;

namespace Mk8Editor.Desktop;

/// <summary>Selection and commands shared by the visible table and headless checks.</summary>
public sealed class EditorState
{
    public EditSession? Session { get; private set; }
    public HashSet<int> SelectedOffsets { get; } = [];
    public int? ActiveOffset { get; private set; }
    public int Category { get; set; }
    public string Query { get; set; } = "";
    public bool Dark { get; set; }
    public static readonly string[] Sections = ["PTBD", "PTDV", "PTWG", "PTTR"];
    public static readonly string[] Categories = ["Karts", "Characters", "Gliders", "Wheels"];
    public IReadOnlyList<RecordData> Records => Session?.File.Records.ToArray() ?? [];
    public RecordData[] VisibleRecords => Category >= 4 ? [] : Records.Where(record =>
        record.Entry.Section == Sections[Category] &&
        record.Entry.Name.Contains(Query, StringComparison.OrdinalIgnoreCase)).ToArray();

    public void Load(string path)
    {
        // Parse before replacing the active session, so an invalid file loses no edits.
        var candidate = EditSession.Open(path);
        Session = candidate;
        SelectedOffsets.Clear(); ActiveOffset = null; Query = "";
    }

    public RecordData? RecordAt(int offset) => Records.FirstOrDefault(record =>
        offset >= record.Offset && offset < record.Offset + PerformanceFile.RecordSize);

    public void Select(int offset, bool additive, bool range)
    {
        var target = RecordAt(offset) ?? throw new ArgumentException("Select a statistic.");
        if ((offset - target.Offset) % 4 != 0) throw new ArgumentException("Unaligned statistic.");
        var visible = VisibleRecords;
        if (range && ActiveOffset is int anchor && RecordAt(anchor) is RecordData previous)
        {
            int first = Array.IndexOf(visible, previous), last = Array.IndexOf(visible, target);
            if (first >= 0 && last >= 0)
            {
                int a = (anchor - previous.Offset) / 4, b = (offset - target.Offset) / 4;
                if (!additive) SelectedOffsets.Clear();
                for (int row = Math.Min(first, last); row <= Math.Max(first, last); row++)
                    for (int column = Math.Min(a, b); column <= Math.Max(a, b); column++)
                        SelectedOffsets.Add(visible[row].Offset + column * 4);
                return;
            }
        }
        if (!additive) SelectedOffsets.Clear();
        if (additive && SelectedOffsets.Contains(offset)) SelectedOffsets.Remove(offset);
        else SelectedOffsets.Add(offset);
        ActiveOffset = offset;
    }

    public void ClearSelection() { SelectedOffsets.Clear(); ActiveOffset = null; }
    public string CopyAll()
    {
        var record = ActiveOffset is int offset ? RecordAt(offset) : null;
        if (Session == null || record == null) throw new InvalidOperationException("Select a source item first.");
        return StatisticTransfer.Format(Enumerable.Range(0, 14).Select(i => Session.GetValue(record.Offset + i * 4)).ToArray());
    }
    public void PasteAll(string text)
    {
        if (Session == null) throw new InvalidOperationException("Open Performance.bin first.");
        uint[] values = StatisticTransfer.Parse(text);
        var targets = SelectedOffsets.Select(RecordAt).OfType<RecordData>().Distinct().ToArray();
        if (targets.Length == 0) throw new InvalidOperationException("Select destination items first.");
        var batch = new Dictionary<int, uint>();
        foreach (var item in targets)
            for (int i = 0; i < 14; i++) batch.Add(item.Offset + i * 4, values[i]);
        Session.SetValues(batch);
    }
    public void SetSelected(uint value)
    {
        if (Session == null || SelectedOffsets.Count == 0) throw new InvalidOperationException("Select statistic cells first.");
        Session.SetValues(SelectedOffsets.ToDictionary(offset => offset, _ => value));
    }
}
