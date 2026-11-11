using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Mk8Editor.Core;

namespace Mk8Editor.Desktop;

/// <summary>Two viewport-rendered views of one session, with a shared position on both axes.</summary>
public sealed class HexView : UserControl
{
    private readonly EditorState state;
    private readonly HexPane original, edited;
    private readonly ScrollBar[] horizontal = new ScrollBar[2], vertical = new ScrollBar[2];
    private readonly Border[] panels = new Border[2];
    private readonly TextBlock[] titles = new TextBlock[2];
    private EditSession? loaded;
    private byte[] source = [], current = [];
    private int[] recordsByByte = [], rows = [];
    private RecordData[] records = [];
    private bool synchronizing;
    private string filter = "";
    private Vector offset;
    public event Action<int, int>? NavigateRequested;
    public int? SelectedByteOffset => state.ActiveOffset;
    public Vector ScrollOffset => offset;
    public Vector OriginalScrollOffset => new(horizontal[0].Value, vertical[0].Value);
    public Vector EditedScrollOffset => new(horizontal[1].Value, vertical[1].Value);
    public int VisibleRowCount => rows.Length;
    private const double RowHeight = 28, HeaderHeight = 30, AddressWidth = 102, ByteWidth = 29;
    private const double AsciiStart = AddressWidth + 16 * ByteWidth + 14, ContentWidth = AsciiStart + 16 * 8 + 16;

    public HexView(EditorState state)
    {
        this.state = state;
        original = new HexPane(this, true); edited = new HexPane(this, false);
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        var panes = new[] { original, edited };
        for (int index = 0; index < 2; index++)
        {
            int paneIndex = index;
            titles[index] = new TextBlock { Text = index == 0 ? "Original file" : "Edited copy · live", FontSize = 15,
                FontWeight = FontWeight.SemiBold, Margin = new Thickness(12, 10) };
            horizontal[index] = new ScrollBar { Orientation = Orientation.Horizontal, Height = 22, SmallChange = ByteWidth, AllowAutoHide = false };
            vertical[index] = new ScrollBar { Orientation = Orientation.Vertical, Width = 22, SmallChange = RowHeight * 3, AllowAutoHide = false };
            horizontal[index].ValueChanged += (_, _) => SetScrollOffset(new Vector(horizontal[paneIndex].Value, offset.Y));
            vertical[index].ValueChanged += (_, _) => SetScrollOffset(new Vector(offset.X, vertical[paneIndex].Value));
            var area = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            area.Children.Add(titles[index]);
            Grid.SetRow(panes[index], 1); area.Children.Add(panes[index]);
            Grid.SetRow(vertical[index], 1); Grid.SetColumn(vertical[index], 1); area.Children.Add(vertical[index]);
            Grid.SetRow(horizontal[index], 2); area.Children.Add(horizontal[index]);
            panels[index] = new Border { Child = area, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7),
                Margin = new Thickness(index == 0 ? 0 : 5, 0, index == 0 ? 5 : 0, 0) };
            Grid.SetColumn(panels[index], index); layout.Children.Add(panels[index]);
            panes[index].SizeChanged += (_, _) => UpdateScrollRanges();
        }
        Styles.Add(new Style(s => s.OfType<ScrollBar>().Class(":vertical").Template().OfType<Thumb>())
            { Setters = { new Setter(MinHeightProperty, 42d) } });
        Styles.Add(new Style(s => s.OfType<ScrollBar>().Class(":horizontal").Template().OfType<Thumb>())
            { Setters = { new Setter(MinWidthProperty, 42d) } });
        Content = layout;
        Refresh();
    }

    public void Refresh()
    {
        if (loaded != state.Session)
        {
            loaded = state.Session;
            source = loaded?.File.CopyOriginalBytes() ?? [];
            records = loaded?.File.Records.ToArray() ?? [];
            recordsByByte = Enumerable.Repeat(-1, source.Length).ToArray();
            for (int recordIndex = 0; recordIndex < records.Length; recordIndex++)
                Array.Fill(recordsByByte, recordIndex, records[recordIndex].Offset, PerformanceFile.RecordSize);
            filter = "\0";
            SetScrollOffset(default);
        }
        current = loaded?.Serialize() ?? [];
        foreach (var panel in panels) { panel.Background = Appearance.Neutral(state.Dark); panel.BorderBrush = Appearance.Border(state.Dark); }
        foreach (var title in titles) title.Foreground = Appearance.Text(state.Dark);
        Filter(state.Query);
        original.InvalidateVisual(); edited.InvalidateVisual();
    }

    public void Filter(string query)
    {
        query = query.Trim();
        if (filter == query) return;
        filter = query;
        rows = Enumerable.Range(0, (source.Length + 15) / 16).Select(index => index * 16).ToArray();
        bool addressQuery = query.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        if (query.Length > 0 && !addressQuery)
            rows = rows.Where(row => Enumerable.Range(row, Math.Min(16, source.Length - row))
                .Any(position => recordsByByte[position] is int recordIndex && recordIndex >= 0 &&
                    records[recordIndex].Entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();
        UpdateScrollRanges();
        if (addressQuery && int.TryParse(query[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int address) && address >= 0 && address < source.Length)
            SetScrollOffset(new Vector(offset.X, address / 16 * RowHeight));
        else SetScrollOffset(default);
        original.InvalidateVisual(); edited.InvalidateVisual();
    }

    public void RevealSelection()
    {
        if (state.ActiveOffset is not int selected) return;
        Filter("");
        int row = Array.IndexOf(rows, selected / 16 * 16);
        if (row < 0) return;
        double viewportWidth = Math.Min(original.Bounds.Width, edited.Bounds.Width);
        double end = AddressWidth + (selected % 16 + 4) * ByteWidth;
        SetScrollOffset(new Vector(Math.Max(0, end - viewportWidth + 12), Math.Max(0, row * RowHeight - RowHeight * 3)));
        edited.Focus();
    }

    public void SetScrollOffset(Vector value)
    {
        if (synchronizing) return;
        synchronizing = true;
        try
        {
            offset = new Vector(Math.Clamp(value.X, 0, horizontal.Min(bar => bar.Maximum)), Math.Clamp(value.Y, 0, vertical.Min(bar => bar.Maximum)));
            for (int i = 0; i < 2; i++) { horizontal[i].Value = offset.X; vertical[i].Value = offset.Y; }
            original.InvalidateVisual(); edited.InvalidateVisual();
        }
        finally { synchronizing = false; }
    }

    private void UpdateScrollRanges()
    {
        synchronizing = true;
        try
        {
            double width = Math.Max(0, Math.Min(original.Bounds.Width, edited.Bounds.Width));
            double height = Math.Max(0, Math.Min(original.Bounds.Height, edited.Bounds.Height) - HeaderHeight);
            for (int i = 0; i < 2; i++)
            {
                horizontal[i].Maximum = Math.Max(0, ContentWidth - width); horizontal[i].ViewportSize = width; horizontal[i].LargeChange = Math.Max(ByteWidth, width * .8);
                vertical[i].Maximum = Math.Max(0, rows.Length * RowHeight - height); vertical[i].ViewportSize = height; vertical[i].LargeChange = Math.Max(RowHeight, height * .8);
            }
        }
        finally { synchronizing = false; }
        SetScrollOffset(offset);
    }

    public bool NavigateToByte(int address)
    {
        if (address < 0 || address >= recordsByByte.Length || recordsByByte[address] < 0) return false;
        int recordIndex = recordsByByte[address];
        NavigateRequested?.Invoke(recordIndex, (address - records[recordIndex].Offset) / 4);
        return true;
    }

    private sealed class HexPane : Control
    {
        private readonly HexView owner;
        private readonly bool showOriginal;
        private int hover = -1;
        private static readonly Typeface Mono = new(new FontFamily("Cascadia Mono,Consolas,Menlo,DejaVu Sans Mono,monospace"));
        public HexPane(HexView owner, bool showOriginal)
        {
            this.owner = owner; this.showOriginal = showOriginal;
            ClipToBounds = true; Focusable = true;
            ToolTip.SetShowDelay(this, 100);
            PointerExited += (_, _) => { hover = -1; ToolTip.SetIsOpen(this, false); };
        }
        private void Text(DrawingContext context, string text, double x, double y, IBrush brush)
            => context.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Mono, 13, brush), new Point(x, y));

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            var dark = owner.state.Dark;
            var background = Appearance.Neutral(dark);
            var foreground = Appearance.Text(dark);
            context.FillRectangle(background, new Rect(Bounds.Size));
            double xOffset = owner.offset.X;
            int first = Math.Max(0, (int)(owner.offset.Y / RowHeight));
            int last = Math.Min(owner.rows.Length, first + (int)Math.Ceiling(Bounds.Height / RowHeight) + 1);
            var bytes = showOriginal ? owner.source : owner.current;
            var bookmarks = new[] { Appearance.Bookmark(dark, 0), Appearance.Bookmark(dark, 1) };
            var changed = new Pen(Appearance.Brush(dark ? "#FFD166" : "#9A5100"), 2);
            var selectedFill = Appearance.Brush(dark ? "#214CA0" : "#164BA7");
            var selectedOutline = new Pen(Appearance.Brush(dark ? "#B9DDFF" : "#0A2B64"), 2);
            using (context.PushClip(new Rect(0, HeaderHeight, Bounds.Width, Math.Max(0, Bounds.Height - HeaderHeight))))
            for (int rowIndex = first; rowIndex < last; rowIndex++)
            {
                int address = owner.rows[rowIndex];
                double y = HeaderHeight + rowIndex * RowHeight - owner.offset.Y;
                Text(context, "0x" + address.ToString("X8"), 8 - xOffset, y + 5, foreground);
                for (int column = 0; column < 16 && address + column < bytes.Length; column++)
                {
                    int position = address + column, recordIndex = owner.recordsByByte[position];
                    double x = AddressWidth + column * ByteWidth - xOffset;
                    var rect = new Rect(x, y + 1, ByteWidth - 1, RowHeight - 2);
                    bool selected = owner.state.ActiveOffset is int active && position >= active && position < active + 4;
                    context.FillRectangle(selected ? selectedFill : recordIndex < 0 ? background : bookmarks[recordIndex % 2], rect);
                    if (owner.source[position] != owner.current[position]) context.DrawRectangle(changed, rect.Deflate(1));
                    if (selected) context.DrawRectangle(selectedOutline, rect.Deflate(1));
                    Text(context, bytes[position].ToString("X2"), x + 6, y + 5, selected ? Brushes.White : foreground);
                    char ascii = bytes[position] >= 32 && bytes[position] <= 126 ? (char)bytes[position] : '.';
                    Text(context, ascii.ToString(), AsciiStart + column * 8 - xOffset, y + 5, foreground);
                }
            }
            context.FillRectangle(Appearance.Surface(dark), new Rect(0, 0, Bounds.Width, HeaderHeight));
            Text(context, "Offset", 8 - xOffset, 6, foreground);
            for (int column = 0; column < 16; column++) Text(context, column.ToString("X2"), AddressWidth + column * ByteWidth + 6 - xOffset, 6, foreground);
            Text(context, "ASCII", AsciiStart - xOffset, 6, foreground);
            if (owner.rows.Length == 0) Text(context, owner.loaded == null ? "Open Performance.bin to view bytes." : "No matching bookmarks.", 12, 48, foreground);
        }

        private int Hit(Point point)
        {
            if (point.Y < HeaderHeight) return -1;
            int rowIndex = (int)((point.Y - HeaderHeight + owner.offset.Y) / RowHeight);
            if (rowIndex < 0 || rowIndex >= owner.rows.Length) return -1;
            double x = point.X + owner.offset.X;
            int column = x >= AsciiStart && x < AsciiStart + 128 ? (int)((x - AsciiStart) / 8) :
                x >= AddressWidth && x < AddressWidth + ByteWidth * 16 ? (int)((x - AddressWidth) / ByteWidth) : -1;
            int address = owner.rows[rowIndex] + column;
            return column >= 0 && address < owner.source.Length ? address : -1;
        }
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (e.ClickCount == 2 && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                e.Handled = owner.NavigateToByte(Hit(e.GetPosition(this)));
        }
        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            double horizontal = e.Delta.X * ByteWidth * 3, vertical = e.Delta.Y * RowHeight * 3;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) { horizontal += vertical; vertical = 0; }
            owner.SetScrollOffset(new Vector(owner.offset.X - horizontal, owner.offset.Y - vertical));
            e.Handled = true;
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            var offset = owner.offset;
            switch (e.Key)
            {
                case Key.Down: offset = new Vector(offset.X, offset.Y + RowHeight); break;
                case Key.Up: offset = new Vector(offset.X, offset.Y - RowHeight); break;
                case Key.Right: offset = new Vector(offset.X + ByteWidth, offset.Y); break;
                case Key.Left: offset = new Vector(offset.X - ByteWidth, offset.Y); break;
                case Key.PageDown: offset = new Vector(offset.X, offset.Y + Bounds.Height - HeaderHeight); break;
                case Key.PageUp: offset = new Vector(offset.X, offset.Y - Bounds.Height + HeaderHeight); break;
                case Key.Home: offset = default; break;
                case Key.End: offset = new Vector(offset.X, owner.vertical[0].Maximum); break;
                default: return;
            }
            owner.SetScrollOffset(offset); e.Handled = true;
        }
        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            int address = Hit(e.GetPosition(this));
            if (hover == address) return;
            hover = address; ToolTip.SetIsOpen(this, false);
            if (address < 0) { ToolTip.SetTip(this, null); return; }
            var content = new StackPanel { Spacing = 7, MaxWidth = 370 };
            int recordIndex = owner.recordsByByte[address];
            if (recordIndex < 0)
                content.Children.Add(new TextBlock { Text = $"Byte 0x{address:X8}\nHeader or non-statistical data.\nPreserved unchanged.", TextWrapping = TextWrapping.Wrap });
            else
            {
                var record = owner.records[recordIndex];
                int stat = (address - record.Offset) / 4, field = record.Offset + stat * 4;
                var session = owner.loaded!;
                var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
                heading.Children.Add(new Image { Source = Appearance.Image(record.Entry.Image), Width = 68, Height = 56, Stretch = Stretch.Uniform });
                heading.Children.Add(new TextBlock { Text = record.Entry.Name, FontSize = 16, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
                content.Children.Add(heading);
                uint value = showOriginal ? session.GetOriginalValue(field) : session.GetValue(field);
                content.Children.Add(new TextBlock { Text = $"{Statistics.Codes[stat]} — {Statistics.Names[stat]}\n" +
                    $"{(showOriginal ? "Original" : "Current")} value: {value} (0x{value:X8})\n" +
                    $"Original → current: {session.GetOriginalValue(field)} → {session.GetValue(field)}\n" +
                    $"Bookmark: 0x{record.Offset:X8}–0x{record.Offset + 55:X8}\nField: 0x{field:X8} · 4 bytes, little-endian\nHovered byte: 0x{address:X8}\nDouble-click to open this statistic.", TextWrapping = TextWrapping.Wrap });
            }
            ToolTip.SetTip(this, content);
            ToolTip.SetIsOpen(this, true);
        }
    }
}
