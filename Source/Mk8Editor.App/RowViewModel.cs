using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mk8Editor.Core;

namespace Mk8Editor.App
{
    public sealed class RowViewModel
    {
        public RecordData Record { get; private set; }
        public int Number { get { return Record.Entry.Index + 1; } }
        public string Name { get { return Record.Entry.Name; } }
        public string Detail { get { return Record.Entry.Note; } }
        public BitmapImage Image { get; private set; }
        public StatCell[] Cells { get; private set; }
        public RowViewModel(RecordData record, EditSession session, Action changed)
        {
            Record = record;
            using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Mk8Editor.Assets." + record.Entry.Image))
            {
                if (stream == null) throw new InvalidDataException("Missing image: " + record.Entry.Name);
                Image = new BitmapImage(); Image.BeginInit(); Image.CacheOption = BitmapCacheOption.OnLoad;
                Image.StreamSource = stream; Image.EndInit(); Image.Freeze();
            }
            Cells = Enumerable.Range(0, 14).Select(index => new StatCell(session, record.Offset + index * 4, index, changed)).ToArray();
        }
        public void Refresh() { foreach (var cell in Cells) cell.Refresh(); }
    }

    public sealed class StatCell : INotifyPropertyChanged, IDataErrorInfo
    {
        private readonly EditSession session;
        private readonly int offset;
        private readonly int statistic;
        private readonly Action changed;
        private string text;
        public event PropertyChangedEventHandler PropertyChanged;
        public StatCell(EditSession session, int offset, int statistic, Action changed)
        { this.session = session; this.offset = offset; this.statistic = statistic; this.changed = changed; Refresh(); }
        public string Text
        {
            get { return text; }
            set
            {
                text = value;
                uint number;
                if (TryParse(value, out number)) session.SetValue(offset, number);
                Notify(); changed();
            }
        }
        public static bool TryParse(string input, out uint number)
        { return UInt32.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out number); }
        public bool IsValid { get { uint ignored; return TryParse(text, out ignored); } }
        public string Error { get { return IsValid ? "" : "Enter a whole number from 0 to 4294967295."; } }
        public string this[string name] { get { return name == "Text" ? Error : ""; } }
        public string Hint
        {
            get { return Statistics.Names[statistic] + " · value: " + Text + " · original: " + session.GetOriginalValue(offset).ToString(CultureInfo.InvariantCulture); }
        }
        public Brush Background
        {
            get
            {
                if (!IsValid) return ThemePalette.GetBrush("Invalid");
                uint value = session.GetValue(offset), baseline = session.GetOriginalValue(offset);
                if (value > baseline) return ThemePalette.GetBrush("Increase");
                if (value < baseline) return ThemePalette.GetBrush("Decrease");
                return ThemePalette.GetBrush("Statistic");
            }
        }
        public void RefreshTheme() { Notify(); }
        public void Refresh() { text = session.GetValue(offset).ToString(CultureInfo.InvariantCulture); Notify(); }
        private void Notify()
        {
            var handler = PropertyChanged;
            if (handler == null) return;
            foreach (string property in new[] { "Text", "Background", "Error", "IsValid", "Hint" }) handler(this, new PropertyChangedEventArgs(property));
        }
    }
}
