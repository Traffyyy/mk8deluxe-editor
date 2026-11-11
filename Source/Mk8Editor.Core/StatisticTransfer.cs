using System;
using System.Globalization;
using System.Linq;

namespace Mk8Editor.Core
{
    /// <summary>Plain-text interchange in the editor's fourteen-column order.</summary>
    public static class StatisticTransfer
    {
        public static string Format(uint[] values)
        {
            if (values == null || values.Length != 14) throw new ArgumentException("Exactly 14 statistics are required.");
            return String.Join("\t", values.Select(value => value.ToString(CultureInfo.InvariantCulture)));
        }

        public static uint[] Parse(string text)
        {
            string[] fields = (text ?? "").Split(new[] { '\t', '\r', '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 14) throw new FormatException("Copy all 14 statistics from an item first. The clipboard must contain exactly 14 whole numbers.");
            var values = new uint[14];
            for (int index = 0; index < fields.Length; index++)
                if (!UInt32.TryParse(fields[index], NumberStyles.None, CultureInfo.InvariantCulture, out values[index]))
                    throw new FormatException("Each statistic must be a whole number from 0 to 4294967295.");
            return values;
        }
    }
}
