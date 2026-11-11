using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mk8Editor.Core;

namespace Mk8Editor.App
{
    /// <summary>Displays the current session bytes and resolves bookmarks by verified byte ranges.</summary>
    public sealed class HexView
    {
        public DataGrid Grid { get; private set; }
        public List<HexRow> Rows { get; private set; }
        private EditSession session;
        private readonly bool showOriginal;
        private byte[] originalBytes;
        private string currentFilter;
        public HexView(DataGrid grid, Action<HexCell> editStatistic, bool showOriginal = false)
        {
            this.showOriginal = showOriginal;
            Grid = grid; Rows = new List<HexRow>();
            Grid.Columns.Add(new DataGridTextColumn { Header = "Offset", Binding = new System.Windows.Data.Binding("Address"), Width = 82, MinWidth = 82 });
            for (int index = 0; index < 16; index++)
            {
                string template = "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>" +
                    "<Border DataContext='{Binding Cells[" + index + "]}' Background='{Binding Background}' BorderBrush='{Binding ChangeBorder}' BorderThickness='1' Padding='1,3' ToolTipService.InitialShowDelay='100' ToolTipService.ShowDuration='60000'>" +
                    "<Border.ToolTip><ToolTip DataContext='{Binding PlacementTarget.DataContext, RelativeSource={RelativeSource Self}}'><StackPanel Width='340'>" +
                    "<StackPanel Orientation='Horizontal' Margin='0,0,0,10'><Image Source='{Binding Image}' Visibility='{Binding BookmarkVisibility}' Width='64' Height='52' Margin='0,0,12,0'/>" +
                    "<TextBlock Text='{Binding Name}' FontFamily='Segoe UI' FontSize='16' FontWeight='SemiBold' TextWrapping='Wrap' Width='255' VerticalAlignment='Center'/></StackPanel>" +
                    "<TextBlock Text='{Binding Details}' FontFamily='Segoe UI' FontSize='13' TextWrapping='Wrap'/>" +
                    "</StackPanel></ToolTip></Border.ToolTip><TextBlock Text='{Binding Hex}' HorizontalAlignment='Center'/></Border></DataTemplate>";
                Grid.Columns.Add(new DataGridTemplateColumn { Header = index.ToString("X2"), MinWidth = 25,
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star), CellTemplate = (DataTemplate)XamlReader.Parse(template) });
            }
            Grid.Columns.Add(new DataGridTextColumn { Header = "ASCII", Binding = new System.Windows.Data.Binding("Ascii"), Width = 132, MinWidth = 132 });
            Grid.MouseDoubleClick += delegate(object sender, System.Windows.Input.MouseButtonEventArgs args) {
                // Resolve the actual hit, never the last selected cell: headers and empty space are not bookmarks.
                var hit = args.OriginalSource as DependencyObject;
                while (hit != null && hit != Grid)
                {
                    var element = hit as FrameworkElement;
                    var cell = element == null ? null : element.DataContext as HexCell;
                    if (cell != null)
                    {
                        if (cell.Bookmark != null) { editStatistic(cell); args.Handled = true; }
                        return;
                    }
                    hit = hit is Visual ? VisualTreeHelper.GetParent(hit) : LogicalTreeHelper.GetParent(hit);
                }
            };
        }
        public void Load(EditSession nextSession, IList<RowViewModel> itemRows)
        {
            session = nextSession;
            originalBytes = session.File.CopyOriginalBytes();
            // Assign colors once in file order, so filtering never changes an item's color.
            var colorIndices = itemRows.OrderBy(item => item.Record.Offset)
                .Select((item, index) => new { Item = item, Index = index })
                .ToDictionary(entry => entry.Item, entry => entry.Index);
            var bookmarks = new RowViewModel[session.File.Length];
            foreach (var item in itemRows)
                for (int byteIndex = 0; byteIndex < PerformanceFile.RecordSize; byteIndex++) bookmarks[item.Record.Offset + byteIndex] = item;
            Rows = new List<HexRow>();
            for (int address = 0; address < session.File.Length; address += 16)
            {
                var cells = new HexCell[16];
                for (int index = 0; index < cells.Length; index++)
                {
                    int offset = address + index;
                    var bookmark = offset < bookmarks.Length ? bookmarks[offset] : null;
                    cells[index] = new HexCell(session, offset, bookmark, bookmark == null ? -1 : colorIndices[bookmark], showOriginal);
                }
                Rows.Add(new HexRow(address, cells));
            }
            currentFilter = null; Refresh(); Filter("");
        }
        public void Refresh()
        {
            if (session == null) return;
            byte[] bytes = showOriginal ? originalBytes : session.Serialize();
            foreach (var row in Rows) row.Refresh(bytes);
        }
        public void Filter(string query)
        {
            query = query.Trim();
            if (query == currentFilter) return;
            currentFilter = query;
            int address;
            if (query.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (Int32.TryParse(query.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out address) && address >= 0 && session != null && address < session.File.Length)
                { Grid.ItemsSource = Rows; Grid.ScrollIntoView(Rows[address / 16]); }
                else Grid.ItemsSource = new List<HexRow>();
            }
            else Grid.ItemsSource = query.Length == 0 ? Rows : Rows.Where(row => row.Cells.Any(cell => cell.Bookmark != null && cell.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
        }
    }
    public sealed class HexRow : INotifyPropertyChanged
    {
        public int Offset { get; private set; }
        public string Address { get { return "0x" + Offset.ToString("X8"); } }
        public string Ascii { get; private set; }
        public HexCell[] Cells { get; private set; }
        public event PropertyChangedEventHandler PropertyChanged;
        public HexRow(int offset, HexCell[] cells) { Offset = offset; Cells = cells; }
        public void Refresh(byte[] bytes)
        {
            foreach (var cell in Cells) cell.Refresh(bytes);
            Ascii = new string(Cells.Where(cell => cell.Offset < bytes.Length).Select(cell => bytes[cell.Offset] >= 32 && bytes[cell.Offset] <= 126 ? (char)bytes[cell.Offset] : '.').ToArray());
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Ascii"));
        }
    }
    public sealed class HexCell : INotifyPropertyChanged
    {
        private readonly EditSession session;
        private readonly int bookmarkColorIndex;
        private readonly bool showOriginal;
        public int Offset { get; private set; }
        public RowViewModel Bookmark { get; private set; }
        public int StatisticIndex { get { return Bookmark == null ? -1 : (Offset - Bookmark.Record.Offset) / 4; } }
        public string Hex { get; private set; }
        public string Name { get { return Bookmark == null ? "No editable bookmark" : Bookmark.Name; } }
        public BitmapImage Image { get { return Bookmark == null ? null : Bookmark.Image; } }
        public Visibility BookmarkVisibility { get { return Bookmark == null ? Visibility.Collapsed : Visibility.Visible; } }
        public Brush Background
        {
            get { return ThemePalette.GetBrush(Bookmark == null ? "Surface" : bookmarkColorIndex % 2 == 0 ? "HexBookmarkEven" : "HexBookmarkOdd"); }
        }
        public bool IsChanged
        {
            get {
                if (Bookmark == null) return false;
                int field = Bookmark.Record.Offset + StatisticIndex * 4;
                int shift = (Offset - field) * 8;
                return (byte)(session.GetOriginalValue(field) >> shift) != (byte)(session.GetValue(field) >> shift);
            }
        }
        public Brush ChangeBorder { get { return IsChanged ? ThemePalette.GetBrush("DiffOutline") : Brushes.Transparent; } }
        public string Details
        {
            get
            {
                if (Offset >= session.File.Length) return "Outside the file.";
                if (Bookmark == null) return "Byte 0x" + Offset.ToString("X8") + "\nHeader or non-statistical data. This byte is preserved unchanged.";
                int start = Bookmark.Record.Offset, field = start + StatisticIndex * 4;
                uint value = showOriginal ? session.GetOriginalValue(field) : session.GetValue(field);
                return Statistics.Codes[StatisticIndex] + " — " + Statistics.Names[StatisticIndex] +
                    "\n" + (showOriginal ? "Original value: " : "Current value: ") + value + " (0x" + value.ToString("X8") + ")" +
                    (IsChanged ? "\nChanged from " + session.GetOriginalValue(field) + " to " + session.GetValue(field) + "." : "") +
                    "\nBookmark: 0x" + start.ToString("X8") + "–0x" + (start + 55).ToString("X8") +
                    "\nField: 0x" + field.ToString("X8") + " · 4 bytes, little-endian" +
                    "\nHovered byte: 0x" + Offset.ToString("X8") + "\nDouble-click to edit this statistic in its item tab.";
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public HexCell(EditSession session, int offset, RowViewModel bookmark, int colorIndex, bool showOriginal)
        {
            this.session = session; Offset = offset; Bookmark = bookmark; bookmarkColorIndex = colorIndex; this.showOriginal = showOriginal;
        }
        public void Refresh(byte[] bytes)
        {
            Hex = Offset < bytes.Length ? bytes[Offset].ToString("X2") : "";
            if (PropertyChanged != null)
                foreach (string name in new[] { "Hex", "Details", "Background", "IsChanged", "ChangeBorder" }) PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
    }
}
