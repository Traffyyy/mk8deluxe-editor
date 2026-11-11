using System.Reflection;
using System.Text;
using Mk8Editor.Core;

internal static class PortableTests
{
    private static int Main(string[] args)
    {
        string temporary = Path.Combine(Path.GetTempPath(), "mk8-portable-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            if (args.Length == 2 && args[0] == "--write-fixture")
            {
                using var output = new FileStream(args[1], FileMode.CreateNew);
                output.Write(SyntheticFixture.Create());
                Console.WriteLine("Created synthetic fixture (no game data): " + args[1]);
                return 0;
            }
            Directory.CreateDirectory(temporary);
            string fixture = args.Length == 1 ? Path.GetFullPath(args[0]) : Path.Combine(temporary, "Performance.bin");
            if (args.Length == 0) File.WriteAllBytes(fixture, SyntheticFixture.Create());
            if (args.Length > 1) throw new ArgumentException("Usage: Mk8Editor.Tests [Performance.bin] | --write-fixture <new-path>");
            // Reuse the Windows suite verbatim; its entry point remains private in the original source.
            var originalRunner = typeof(Tests).GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic)!;
            int result = (int)originalRunner.Invoke(null, new object[] { new[] { fixture } })!;
            if (result != 0) return result;
            string unicodeSource = Path.Combine(temporary, "pérformance 日本語.bin");
            File.Copy(fixture, unicodeSource);
            var session = EditSession.Open(unicodeSource);
            string unicodeCopy = Path.Combine(temporary, "cópia 日本語.bin");
            session.SaveCopy(unicodeCopy);
            Check(File.ReadAllBytes(unicodeSource).SequenceEqual(File.ReadAllBytes(unicodeCopy)), "Unicode filenames export without changing data");
            if (!OperatingSystem.IsWindows())
            {
                string link = Path.Combine(temporary, "source-link.bin");
                File.CreateSymbolicLink(link, unicodeSource);
                bool refused = false;
                try { session.SaveCopy(link); } catch (IOException) { refused = true; }
                Check(refused && File.ReadAllBytes(unicodeSource).SequenceEqual(File.ReadAllBytes(fixture)), "Symbolic-link destinations cannot overwrite the source");
            }
            var editable = EditSession.Open(unicodeCopy);
            var before = File.ReadAllBytes(unicodeCopy);
            int offset = editable.File.Records[0].Offset;
            editable.SetValue(offset, 123);
            editable.Save();
            Check(File.ReadAllBytes(unicodeCopy).SequenceEqual(editable.Serialize()), "Save overwrites the opened file");
            Check(File.ReadAllBytes(editable.LastBackupPath).SequenceEqual(before), "Overwrite backup retains previous bytes");
            Check(!editable.HasUnexportedChanges, "Save records clean checkpoint");
            editable.Undo(); editable.Save();
            Check(File.ReadAllBytes(unicodeCopy).SequenceEqual(before), "Undo and save restore previous data");
            File.WriteAllBytes(unicodeCopy, new byte[] { 1, 2, 3 });
            bool conflict = false;
            try { editable.Save(); } catch (UserFileException) { conflict = true; }
            Check(conflict && File.ReadAllBytes(unicodeCopy).Length == 3, "External changes are not overwritten");
            string alternative = Path.Combine(temporary, "existing.bin");
            File.WriteAllBytes(alternative, before);
            editable.SaveAs(alternative);
            Check(editable.SourcePath == alternative && File.ReadAllBytes(editable.LastBackupPath).SequenceEqual(before), "Save As replaces destination with backup and updates path");
            Console.WriteLine("PORTABLE CHECKS PASSED");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL: " + error); return 1; }
        finally
        {
            // This process owns this exact GUID directory; never remove user-provided paths.
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
    }

    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }
}

internal static class SyntheticFixture
{
    // Constructed from the documented format, not copied from a game file.
    // Matching the original suite's addresses allows every existing assertion to run unchanged.
    internal static byte[] Create()
    {
        var bytes = new byte[12332];
        Encoding.ASCII.GetBytes("FREP").CopyTo(bytes, 0);
        Write32(bytes, 4, (uint)bytes.Length);
        bytes[8] = 18;
        bytes[10] = 88;
        for (int section = 0; section < 14; section++)
        {
            int address = 88 + section * 16;
            Write32(bytes, 16 + section * 4, (uint)(address - 88));
            Encoding.ASCII.GetBytes("TEST").CopyTo(bytes, address);
        }
        int[] starts = { 4932, 7244, 10228, 11476 };
        int[] counts = { 41, 53, 22, 15 };
        string[] reversedTags = { "DBTP", "VDTP", "RTTP", "GWTP" };
        for (int section = 0; section < starts.Length; section++)
        {
            int address = starts[section];
            Write32(bytes, 16 + (14 + section) * 4, (uint)(address - 88));
            Encoding.ASCII.GetBytes(reversedTags[section]).CopyTo(bytes, address);
            bytes[address + 4] = (byte)counts[section];
            for (int field = 0; field < counts[section] * 14; field++)
                Write32(bytes, address + 16 + field * 4, (uint)(1 + field % 29));
        }
        return bytes;
    }

    // Independent fixture writer: the parser and production writer do not construct expectations.
    private static void Write32(byte[] bytes, int offset, uint value)
    {
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
    }
}
