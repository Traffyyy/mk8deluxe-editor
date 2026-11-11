using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Mk8Editor.Core;

namespace Mk8Editor.Desktop;

/// <summary>Frozen item column, synchronized headers and rectangular statistic selection.</summary>
public sealed class StatisticsTable : UserControl
{
    private const double CellWidth = 76, RowHeight = 68;
    private readonly EditorState state;
    private readonly Grid values = new(), items = new();
    private readonly ScrollViewer valuesScroll, itemsScroll, headingScroll;
    private readonly Dictionary<int, Border> cells = [];
    private readonly Dictionary<int, TextBlock> labels = [];
    private RecordData[] records = [];
    private bool dragging, additiveDrag;
    private int? keyboardTarget;
    public event Action? SelectionChanged;
    public event Action? EditRequested;
    public Func<ContextMenu>? CreateContextMenu { get; set; }
    public int CellCount => cells.Count;
    public int VisibleRowCount => records.Length;
    public IReadOnlyDictionary<int, Border> Cells => cells;

    public StatisticsTable(EditorState state)
    {
        this.state = state;
        Focusable = true;
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("260,*"), RowDefinitions = new RowDefinitions("42,*") };
        var itemHeading = new Border { Padding = new Thickness(14, 10), Child = new TextBlock { Text = "Item", FontWeight = FontWeight.SemiBold } };
        root.Children.Add(itemHeading);
        var headings = new StackPanel { Orientation = Orientation.Horizontal };
        for (int i = 0; i < 14; i++)
        {
            var label = new TextBlock { Text = Statistics.Codes[i], Width = CellWidth, Height = 42,
                TextAlignment = TextAlignment.Center, Padding = new Thickness(0, 12), FontWeight = FontWeight.SemiBold };
            ToolTip.SetTip(label, Statistics.Names[i]);
            headings.Children.Add(label);
        }
        headingScroll = new ScrollViewer { Content = headings, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetColumn(headingScroll, 1); root.Children.Add(headingScroll);
        itemsScroll = new ScrollViewer { Content = items, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden };
        Grid.SetRow(itemsScroll, 1); root.Children.Add(itemsScroll);
        valuesScroll = new ScrollViewer { Content = values, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetColumn(valuesScroll, 1); Grid.SetRow(valuesScroll, 1); root.Children.Add(valuesScroll);
        bool scrolling = false;
        valuesScroll.ScrollChanged += (_, _) =>
        {
            if (scrolling) return;
            scrolling = true;
            itemsScroll.Offset = new Vector(0, valuesScroll.Offset.Y);
            headingScroll.Offset = new Vector(valuesScroll.Offset.X, 0);
            scrolling = false;
        };
        itemsScroll.ScrollChanged += (_, _) =>
        {
            if (scrolling) return;
            scrolling = true;
            valuesScroll.Offset = new Vector(valuesScroll.Offset.X, itemsScroll.Offset.Y);
            scrolling = false;
        };
        values.PointerMoved += (_, e) =>
        {
            if (!dragging || !e.GetCurrentPoint(values).Properties.IsLeftButtonPressed) return;
            var position = e.GetPosition(values);
            int row = (int)Math.Floor(position.Y / RowHeight), column = (int)Math.Floor(position.X / CellWidth);
            if (row < 0 || row >= records.Length || column < 0 || column >= 14) return;
            state.Select(records[row].Offset + column * 4, additiveDrag, true);
            RefreshSelection(); SelectionChanged?.Invoke(); e.Handled = true;
        };
        values.PointerReleased += (_, e) => { dragging = false; e.Pointer.Capture(null); };
        values.PointerCaptureLost += (_, _) => dragging = false;
        Content = root;
        AutomationProperties.SetName(this, "Item statistics");
    }

    public void Rebuild()
    {
        records = state.VisibleRecords;
        cells.Clear(); labels.Clear(); values.Children.Clear(); items.Children.Clear();
        values.ColumnDefinitions.Clear(); values.RowDefinitions.Clear(); items.RowDefinitions.Clear();
        for (int i = 0; i < 14; i++) values.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(CellWidth)));
        for (int row = 0; row < records.Length; row++)
        {
            var record = records[row];
            values.RowDefinitions.Add(new RowDefinition(new GridLength(RowHeight))); items.RowDefinitions.Add(new RowDefinition(new GridLength(RowHeight)));
            var identity = new Grid { ColumnDefinitions = new ColumnDefinitions("66,*") };
            identity.Children.Add(new Image { Source = Appearance.Image(record.Entry.Image), Width = 56, Height = 56, Margin = new Thickness(5), Stretch = Stretch.Uniform });
            var name = new TextBlock { Text = record.Entry.Name, VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 0, 10, 0), FontWeight = FontWeight.SemiBold };
            Grid.SetColumn(name, 1); identity.Children.Add(name);
            ToolTip.SetTip(identity, string.IsNullOrWhiteSpace(record.Entry.Note) ? record.Entry.Name : record.Entry.Note);
            var identityBorder = new Border { Child = identity, BorderThickness = new Thickness(0, 0, 1, 1), BorderBrush = Appearance.Border(state.Dark), Background = Appearance.Neutral(state.Dark) };
            Grid.SetRow(identityBorder, row); items.Children.Add(identityBorder);
            for (int column = 0; column < 14; column++)
            {
                int offset = record.Offset + column * 4;
                var label = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 15 };
                var cell = new Border { Child = label, Focusable = true, BorderThickness = new Thickness(0, 0, 1, 1),
                    BorderBrush = Appearance.Border(state.Dark), Cursor = new Cursor(StandardCursorType.Hand) };
                Grid.SetColumn(cell, column); Grid.SetRow(cell, row); values.Children.Add(cell);
                cells.Add(offset, cell); labels.Add(offset, label);
                ToolTip.SetTip(cell, $"{record.Entry.Name} · {Statistics.Names[column]}\nDouble-click or F2 to edit · Shift: rectangle · Ctrl/⌘: add cells");
                AutomationProperties.SetName(cell, record.Entry.Name + " " + Statistics.Names[column]);
                cell.PointerPressed += (_, e) =>
                {
                    var point = e.GetCurrentPoint(cell);
                    bool additive = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
                    bool range = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                    if (point.Properties.IsRightButtonPressed)
                    {
                        if (!state.SelectedOffsets.Contains(offset)) state.Select(offset, false, false);
                    }
                    else if (point.Properties.IsLeftButtonPressed)
                    {
                        state.Select(offset, additive, range);
                        dragging = e.ClickCount != 2; additiveDrag = additive;
                        if (dragging) e.Pointer.Capture(cell);
                    }
                    keyboardTarget = offset; cell.Focus(); RefreshSelection(); SelectionChanged?.Invoke();
                    e.Handled = point.Properties.IsLeftButtonPressed;
                    if (point.Properties.IsLeftButtonPressed && e.ClickCount == 2)
                    {
                        e.Pointer.Capture(null);
                        EditRequested?.Invoke();
                    }
                };

                if (CreateContextMenu != null) cell.ContextMenu = CreateContextMenu();
            }
        }
        RefreshValues();
    }

    public void RefreshValues()
    {
        foreach (var pair in labels) pair.Value.Text = state.Session!.GetValue(pair.Key).ToString();
        RefreshSelection();
    }
    public void RefreshSelection()
    {
        foreach (var pair in cells)
        {
            uint current = state.Session!.GetValue(pair.Key), original = state.Session.GetOriginalValue(pair.Key);
            bool selected = state.SelectedOffsets.Contains(pair.Key);
            pair.Value.Background = selected ? Appearance.Brush(state.Dark ? "#315F91" : "#BCD8FA")
                : current > original ? Appearance.Brush(state.Dark ? "#214B3B" : "#DCF1DE")
                : current < original ? Appearance.Brush(state.Dark ? "#633746" : "#F7DDE3") : Appearance.Neutral(state.Dark);
            pair.Value.BorderBrush = selected ? Appearance.Brush("#438CE8") : Appearance.Border(state.Dark);
            pair.Value.BorderThickness = selected ? new Thickness(1.5) : new Thickness(0, 0, 1, 1);
            labels[pair.Key].Foreground = Appearance.Text(state.Dark);
        }
    }
    public void RevealActive()
    {
        if (state.ActiveOffset is int offset && cells.TryGetValue(offset, out var cell))
        { cell.BringIntoView(); cell.Focus(); }
    }
    public void MoveSelection(Key key, bool extend)
    {
        if (records.Length == 0) return;
        int row = 0, column = 0;
        int? current = extend ? keyboardTarget ?? state.ActiveOffset : state.ActiveOffset;
        if (current is int offset && state.RecordAt(offset) is RecordData record)
        { row = Math.Max(0, Array.IndexOf(records, record)); column = (offset - record.Offset) / 4; }
        row = Math.Clamp(row + (key == Key.Down ? 1 : key == Key.Up ? -1 : 0), 0, records.Length - 1);
        column = Math.Clamp(column + (key == Key.Right ? 1 : key == Key.Left ? -1 : 0), 0, 13);
        keyboardTarget = records[row].Offset + column * 4;
        state.Select(keyboardTarget.Value, false, extend);
        RefreshSelection(); RevealActive(); SelectionChanged?.Invoke();
    }
}



