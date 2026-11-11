using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mk8Editor.Core
{
    public sealed class UserFileException : IOException
    {
        public UserFileException(string message) : base(message) { }
    }
    /// <summary>Keeps source bytes immutable. Only explicitly edited u32 fields are written.</summary>
    public sealed class EditSession
    {
        private readonly byte[] original;
        private readonly HashSet<int> editableOffsets;
        private readonly Dictionary<int, uint> changes = new Dictionary<int, uint>();
        private Dictionary<int, uint> exportedChanges = new Dictionary<int, uint>();
        private readonly Stack<EditAction[]> undo = new Stack<EditAction[]>();
        public PerformanceFile File { get; private set; }
        public string SourcePath { get; private set; }
        public string LastExportPath { get; private set; }
        public int ChangedCount { get { return changes.Count; } }
        public bool CanUndo { get { return undo.Count > 0; } }
        public bool HasUnexportedChanges
        {
            get { return changes.Count != exportedChanges.Count || changes.Any(pair => !exportedChanges.ContainsKey(pair.Key) || exportedChanges[pair.Key] != pair.Value); }
        }
        public EditSession(PerformanceFile file, string sourcePath)
        {
            File = file; SourcePath = Path.GetFullPath(sourcePath); original = file.CopyOriginalBytes();
            editableOffsets = new HashSet<int>(file.Records.SelectMany(record => Enumerable.Range(0, 14).Select(index => record.Offset + index * 4)));
        }
        public static EditSession Open(string path)
        {
            if (new FileInfo(path).Length > 4 * 1024 * 1024)
                throw new InvalidDataException("The file is too large for the supported Performance.bin format.");
            return new EditSession(PerformanceFile.Parse(System.IO.File.ReadAllBytes(path)), path);
        }
        public uint GetValue(int offset) { return changes.ContainsKey(offset) ? changes[offset] : PerformanceFile.ReadUInt32(original, offset); }
        public uint GetOriginalValue(int offset) { return PerformanceFile.ReadUInt32(original, offset); }
        public void SetValue(int offset, uint value)
        {
            if (!editableOffsets.Contains(offset)) throw new ArgumentException("This offset is not a statistic field.");
            uint previous = GetValue(offset);
            if (previous == value) return;
            undo.Push(new[] { new EditAction(offset, previous) }); Apply(offset, value);
        }
        /// <summary>Validates the entire batch before changing any bytes; one Undo restores it all.</summary>
        public void SetValues(IDictionary<int, uint> values)
        {
            if (values == null) throw new ArgumentNullException("values");
            var requested = values.ToArray();
            if (requested.Any(pair => !editableOffsets.Contains(pair.Key)))
                throw new ArgumentException("The selection contains a non-statistic field.");
            var modified = requested.Where(pair => GetValue(pair.Key) != pair.Value).ToArray();
            if (modified.Length == 0) return;
            var previous = modified.Select(pair => new EditAction(pair.Key, GetValue(pair.Key))).ToArray();
            undo.Push(previous);
            foreach (var pair in modified) Apply(pair.Key, pair.Value);
        }
        private void Apply(int offset, uint value)
        { if (value == GetOriginalValue(offset)) changes.Remove(offset); else changes[offset] = value; }
        public void Undo()
        { if (!CanUndo) return; foreach (var action in undo.Pop()) Apply(action.Offset, action.Previous); }
        public void Reset() { changes.Clear(); undo.Clear(); }
        public byte[] Serialize()
        {
            byte[] result = (byte[])original.Clone();
            foreach (var change in changes) PerformanceFile.WriteUInt32(result, change.Key, change.Value);
            return result;
        }
        public void SaveCopy(string destination)
        {
            string fullPath = Path.GetFullPath(destination);
            if (String.Equals(fullPath, SourcePath, StringComparison.OrdinalIgnoreCase))
                throw new UserFileException("Choose a different name. The opened file must remain unchanged.");
            // Refuse existing destinations, including links to the source.
            if (System.IO.File.Exists(fullPath) || Directory.Exists(fullPath))
                throw new UserFileException("This destination already exists. Choose a new name for the copy.");
            byte[] result = Serialize();
            string temporary = Path.Combine(Path.GetDirectoryName(fullPath), ".mk8-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(result, 0, result.Length); stream.Flush(true); }
                if (!System.IO.File.ReadAllBytes(temporary).SequenceEqual(result)) throw new UserFileException("Copy verification failed.");
                System.IO.File.Move(temporary, fullPath);
                exportedChanges = new Dictionary<int, uint>(changes); LastExportPath = fullPath;
            }
            finally { if (System.IO.File.Exists(temporary)) System.IO.File.Delete(temporary); }
        }
        private sealed class EditAction
        {
            public int Offset; public uint Previous;
            public EditAction(int offset, uint previous) { Offset = offset; Previous = previous; }
        }
    }
}
