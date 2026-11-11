using System;
using System.IO;
using System.Linq;
using Mk8Editor.Core;

// Dependency-free regression runner. All writes stay in a new temporary directory.
internal static class Tests
{
    private static int passed;
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        passed++; Console.WriteLine("PASS: " + description);
    }
    private static void Reject<T>(Action action, string description) where T : Exception
    {
        try { action(); } catch (T) { Check(true, description); return; }
        throw new Exception("Expected rejection: " + description);
    }
    private static byte[] Changed(byte[] source, int offset, uint value)
    {
        byte[] result = (byte[])source.Clone();
        PerformanceFile.WriteUInt32(result, offset, value); return result;
    }
    private static int Main(string[] args)
    {
        string temporary = Path.Combine(Path.GetTempPath(), "mk8-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            if (args.Length != 1) throw new Exception("Usage: Mk8Editor.Tests.exe <Performance.bin>");
            byte[] original = File.ReadAllBytes(args[0]);
            var parsed = PerformanceFile.Parse(original);
            Check(parsed.Records.Count == 131, "131 records loaded");
            string[] tags = { "PTBD", "PTDV", "PTTR", "PTWG" };
            int[] counts = { 41, 53, 22, 15 };
            int[] starts = { 4948, 7260, 10244, 11492 };
            for (int group = 0; group < tags.Length; group++)
            {
                var records = parsed.Records.Where(record => record.Entry.Section == tags[group]).ToArray();
                Check(records.Length == counts[group] && records[0].Offset == starts[group] &&
                    records.Select((record, index) => record.Offset == starts[group] + index * 56).All(value => value),
                    "Verified count, header exclusion and stride: " + tags[group]);
            }
            Check(parsed.Records.Last().Offset + 56 == original.Length, "Final record ends at file boundary");
            Check(parsed.Records.Single(record => record.Entry.Section == "PTDV" && record.Entry.Index == 51).Entry.Name == "Pauline", "DLC Pauline mapping");
            Check(parsed.Records.Single(record => record.Entry.Section == "PTDV" && record.Entry.Index == 52).Entry.Name.Contains("Mii"), "Second Mii record retained");
            Directory.CreateDirectory(temporary);
            string sourcePath = Path.Combine(temporary, "Performance.bin");
            File.WriteAllBytes(sourcePath, original);
            var session = EditSession.Open(sourcePath);
            Check(session.Serialize().SequenceEqual(original), "Unedited roundtrip is byte-identical");
            byte[] detached = parsed.CopyOriginalBytes(); detached[0] = 0;
            Check(parsed.CopyOriginalBytes().SequenceEqual(original), "Source bytes are immutable through copy API");
            int offset = parsed.Records[0].Offset;
            uint initial = session.GetValue(offset);
            session.SetValue(offset, UInt32.MaxValue);
            byte[] edited = session.Serialize();
            Check(edited.SequenceEqual(Changed(original, offset, UInt32.MaxValue)), "One edit changes only its four bytes; headers and other data preserved");
            Check(PerformanceFile.ReadUInt32(edited, offset) == UInt32.MaxValue, "Full uint32 range roundtrips");
            Check(session.ChangedCount == 1 && session.HasUnexportedChanges && session.CanUndo, "Edit state tracked");
            session.Undo();
            Check(session.Serialize().SequenceEqual(original) && !session.HasUnexportedChanges && !session.CanUndo, "Undo restores exact source");
            session.SetValue(offset, initial);
            Check(!session.CanUndo, "No-op edit does not create undo entry");
            Reject<ArgumentException>(() => session.SetValue(offset - 4, 9), "Section header cannot be edited");
            Reject<ArgumentException>(() => session.SetValue(offset + 1, 9), "Unaligned field cannot be edited");
            session.SetValue(offset, 9); session.SetValue(offset, 10); session.Undo();
            Check(session.GetValue(offset) == 9, "Sequential undo restores previous edited value");
            string export = Path.Combine(temporary, "export.bin");
            session.SaveCopy(export);
            Check(File.ReadAllBytes(export).SequenceEqual(session.Serialize()) && !session.HasUnexportedChanges, "Export verified and checkpoint recorded");
            Check(File.ReadAllBytes(sourcePath).SequenceEqual(original), "Opened source remains unchanged after export");
            session.SetValue(offset, 11); session.Undo();
            Check(!session.HasUnexportedChanges, "Undo back to exported checkpoint is clean");
            Reject<IOException>(() => session.SaveCopy(sourcePath), "Overwrite of opened file refused");
            Reject<IOException>(() => session.SaveCopy(Path.Combine(temporary, ".", "Performance.bin")), "Equivalent source path refused");
            Reject<IOException>(() => session.SaveCopy(export), "Overwrite of existing destination refused");
            Reject<IOException>(() => session.SaveCopy(temporary), "Directory destination refused");
            session.Reset();
            Check(session.Serialize().SequenceEqual(original) && !session.CanUndo && session.HasUnexportedChanges, "Reset restores source and differs from exported edits");
            string untouchedCopy = Path.Combine(temporary, "untouched.bin"); session.SaveCopy(untouchedCopy);
            Check(File.ReadAllBytes(untouchedCopy).SequenceEqual(original), "Unedited export byte-identical");
            Check(!Directory.GetFiles(temporary, "*.tmp").Any(), "No temporary files remain");
            Reject<InvalidDataException>(() => PerformanceFile.Parse(new byte[5]), "Short header rejected");
            Reject<InvalidDataException>(() => PerformanceFile.Parse(Changed(original, 0, 0)), "Wrong signature rejected");
            Reject<InvalidDataException>(() => PerformanceFile.Parse(original.Take(original.Length - 1).ToArray()), "Truncation rejected");
            Reject<InvalidDataException>(() => PerformanceFile.Parse(Changed(original, 4, 1)), "Declared size mismatch rejected");
            Reject<InvalidDataException>(() => PerformanceFile.Parse(Changed(original, 16, UInt32.MaxValue)), "Overflowing section offset rejected");
            Reject<InvalidDataException>(() => PerformanceFile.Parse(Changed(original, 20, 0)), "Overlapping sections rejected");
            Reject<InvalidDataException>(() => PerformanceFile.Parse(Changed(original, 4936, 0x10028)), "Unknown record count rejected");
            Reject<InvalidDataException>(() => PerformanceFile.Parse(Changed(original, 4932, 0)), "Missing statistics section rejected");
            byte[] relocated = new byte[original.Length + 16];
            Array.Copy(original, 0, relocated, 0, 4932);
            Array.Copy(original, 4932, relocated, 4948, original.Length - 4932);
            PerformanceFile.WriteUInt32(relocated, 4, (uint)relocated.Length);
            for (int tableIndex = 14; tableIndex < 18; tableIndex++)
            {
                int tableOffset = 16 + tableIndex * 4;
                PerformanceFile.WriteUInt32(relocated, tableOffset, PerformanceFile.ReadUInt32(original, tableOffset) + 16);
            }
            Check(PerformanceFile.Parse(relocated).Records[0].Offset == offset + 16, "Addresses are read from section table, not hard-coded");
            var allFields = EditSession.Open(sourcePath);
            foreach (var record in allFields.File.Records)
                for (int stat = 0; stat < 14; stat++) allFields.SetValue(record.Offset + stat * 4, UInt32.MaxValue);
            var allBytes = allFields.Serialize();
            var fieldBytes = allFields.File.Records.SelectMany(record => Enumerable.Range(record.Offset, 56)).ToHashSetCompat();
            Check(Enumerable.Range(0, original.Length).Where(index => !fieldBytes.Contains(index)).All(index => allBytes[index] == original[index]), "Editing all 1834 fields preserves every non-statistical byte");
            var batchSession = EditSession.Open(sourcePath);
            var batch = new System.Collections.Generic.Dictionary<int, uint> { { offset, 123 }, { offset + 4, 456 }, { offset + 56, 789 } };
            batchSession.SetValues(batch);
            Check(batch.All(pair => batchSession.GetValue(pair.Key) == pair.Value), "Batch edits update every requested statistic");
            var expectedBatch = (byte[])original.Clone();
            foreach (var pair in batch) PerformanceFile.WriteUInt32(expectedBatch, pair.Key, pair.Value);
            Check(batchSession.Serialize().SequenceEqual(expectedBatch), "Batch edits preserve every byte outside selected fields");
            batchSession.Undo();
            Check(batchSession.Serialize().SequenceEqual(original) && !batchSession.CanUndo, "One Undo restores the complete batch");
            var invalidBatch = new System.Collections.Generic.Dictionary<int, uint> { { offset, 999 }, { offset - 4, 999 } };
            Reject<ArgumentException>(() => batchSession.SetValues(invalidBatch), "Invalid batch rejected before modifying valid fields");
            Check(batchSession.Serialize().SequenceEqual(original) && !batchSession.CanUndo, "Failed batch leaves bytes and history unchanged");
            batchSession.SetValues(new System.Collections.Generic.Dictionary<int, uint> { { offset, initial } });
            Check(!batchSession.CanUndo, "No-op batch does not create history");
            batchSession.SetValue(offset, 111);
            batchSession.SetValues(batch); batchSession.Undo();
            Check(batchSession.GetValue(offset) == 111 && batchSession.GetValue(offset + 4) == PerformanceFile.ReadUInt32(original, offset + 4), "Batch Undo preserves earlier independent edits");
            uint[] transfer = Enumerable.Range(0, 14).Select(index => (uint)index).ToArray(); transfer[13] = UInt32.MaxValue;
            Check(StatisticTransfer.Parse(StatisticTransfer.Format(transfer)).SequenceEqual(transfer), "Clipboard transfer preserves all 14 statistics including maximum uint");
            Reject<FormatException>(() => StatisticTransfer.Parse("1\t2"), "Incomplete clipboard data rejected");
            Reject<FormatException>(() => StatisticTransfer.Parse(StatisticTransfer.Format(transfer) + "\t3"), "Extra clipboard data rejected");
            Reject<FormatException>(() => StatisticTransfer.Parse("-1\t" + String.Join("\t", transfer.Skip(1))), "Negative clipboard value rejected");
            Reject<FormatException>(() => StatisticTransfer.Parse("4294967296\t" + String.Join("\t", transfer.Skip(1))), "Overflowing clipboard value rejected");
            Reject<FormatException>(() => StatisticTransfer.Parse("1.5\t" + String.Join("\t", transfer.Skip(1))), "Fractional clipboard value rejected");
            Check(File.ReadAllBytes(args[0]).SequenceEqual(original), "Supplied fixture never modified");
            Console.WriteLine("ALL " + passed + " CHECKS PASSED"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL: " + error); return 1; }
        finally
        {
            // Delete only the exact GUID directory created by this process.
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
    }
    private static System.Collections.Generic.HashSet<T> ToHashSetCompat<T>(this System.Collections.Generic.IEnumerable<T> values)
    { return new System.Collections.Generic.HashSet<T>(values); }
}
