using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace Mk8Editor.Core
{
    /// <summary>Reads section boundaries from PERF, never inferring identity from statistics.</summary>
    public sealed class PerformanceFile
    {
        public const int StatisticCount = 14;
        public const int RecordSize = StatisticCount * 4;
        private readonly byte[] originalBytes;
        public ReadOnlyCollection<RecordData> Records { get; private set; }
        public int Length { get { return originalBytes.Length; } }
        private PerformanceFile(byte[] bytes, List<RecordData> records)
        { originalBytes = (byte[])bytes.Clone(); Records = records.AsReadOnly(); }
        public byte[] CopyOriginalBytes() { return (byte[])originalBytes.Clone(); }

        public static PerformanceFile Parse(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 16)
                throw new InvalidDataException("Incomplete file: the Performance.bin header is missing.");
            if (Encoding.ASCII.GetString(bytes, 0, 4) != "FREP")
                throw new InvalidDataException("This is not a little-endian Mario Kart 8 Deluxe Performance.bin file.");
            if (ReadUInt32(bytes, 4) != bytes.Length)
                throw new InvalidDataException("The declared size does not match the file. It may be truncated.");
            int sectionCount = ReadUInt16(bytes, 8);
            int dataStart = ReadUInt16(bytes, 10);
            if (sectionCount < 4 || sectionCount > 256 || dataStart != 16 + sectionCount * 4 || dataStart > bytes.Length)
                throw new InvalidDataException("The section table is invalid.");
            var starts = new List<int>();
            for (int index = 0; index < sectionCount; index++)
            {
                long address = (long)dataStart + ReadUInt32(bytes, 16 + index * 4);
                if (address > bytes.Length - 16 || (index == 0 && address != dataStart) ||
                    (index > 0 && address <= starts[index - 1]))
                    throw new InvalidDataException("Invalid or overlapping section offset.");
                starts.Add((int)address);
            }
            starts.Add(bytes.Length);
            var expected = new Dictionary<string, int> { { "PTBD", 41 }, { "PTDV", 53 }, { "PTTR", 22 }, { "PTWG", 15 } };
            var found = new List<string>();
            var records = new List<RecordData>();
            for (int sectionIndex = 0; sectionIndex < sectionCount; sectionIndex++)
            {
                int header = starts[sectionIndex], end = starts[sectionIndex + 1];
                if (end - header < 16) throw new InvalidDataException("Incomplete section header.");
                string tag = new string(Encoding.ASCII.GetString(bytes, header, 4).Reverse().ToArray());
                if (!expected.ContainsKey(tag)) continue;
                if (found.Contains(tag)) throw new InvalidDataException("Duplicate statistics section: " + tag);
                int count = ReadUInt16(bytes, header + 4);
                int payloadLength = end - header - 16;
                if (count != expected[tag] || payloadLength != count * RecordSize)
                    throw new InvalidDataException("Unsupported version: " + tag + " contains " + count +
                        " records. This editor requires 14 fields per record and the full Deluxe/DLC catalog (41/53/22/15). No data was changed.");
                found.Add(tag);
                for (int index = 0; index < count; index++)
                    records.Add(new RecordData(Catalog.Find(tag, index), header + 16 + index * RecordSize));
            }
            if (!found.SequenceEqual(new[] { "PTBD", "PTDV", "PTTR", "PTWG" }))
                throw new InvalidDataException("The four statistics sections are missing or their order is not recognized.");
            return new PerformanceFile(bytes, records);
        }
        public static uint ReadUInt32(byte[] bytes, int offset)
        { return (uint)(bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16 | bytes[offset + 3] << 24); }
        private static int ReadUInt16(byte[] bytes, int offset) { return bytes[offset] | bytes[offset + 1] << 8; }
        public static void WriteUInt32(byte[] bytes, int offset, uint value)
        { for (int index = 0; index < 4; index++) bytes[offset + index] = (byte)(value >> (index * 8)); }
    }
    public sealed class RecordData
    {
        public CatalogEntry Entry { get; private set; }
        public int Offset { get; private set; }
        public RecordData(CatalogEntry entry, int offset) { Entry = entry; Offset = offset; }
    }
}
