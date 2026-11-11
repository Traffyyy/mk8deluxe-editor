using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;
using Mk8Editor.Core;

namespace Mk8Editor.App
{
    /// <summary>Connects native UI controls to the session; binary logic stays in Core.</summary>
    public sealed class WindowController
    {
        public Window Window { get; private set; }
        public EditSession Session { get; private set; }
        public List<RowViewModel> Rows { get; private set; }
        public DataGrid Grid { get; private set; }
        public BackgroundMusic Music { get; private set; }
        public AudioControls Audio { get; private set; }
        public HexView Hex { get; private set; }
        public HexComparison Comparison { get; private set; }
        public BulkEditingControls Bulk { get; private set; }
        private TabControl tabs;
        private TextBox search;
        private bool ready;
        private int selectedCategory;
        public WindowController(bool silentVerification = false, Func<string> readClipboard = null, Action<string> writeClipboard = null)
        {
            ThemePalette.Apply(false);
            using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Mk8Editor.MainWindow.xaml"))
                Window = (Window)XamlReader.Load(stream);
            using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Mk8Editor.App.ico"))
            {
                var icon = System.Windows.Media.Imaging.BitmapFrame.Create(stream, System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                icon.Freeze(); Window.Icon = icon;
            }
            Grid = Find<DataGrid>("StatsGrid"); tabs = Find<TabControl>("CategoryTabs"); search = Find<TextBox>("SearchBox");
            Rows = new List<RowViewModel>();
            BuildColumns();
            Hex = new HexView(Find<DataGrid>("HexGrid"), NavigateToStatistic);
            Comparison = new HexComparison(new HexView(Find<DataGrid>("OriginalHexGrid"), NavigateToStatistic, true), Hex);
            Music = new BackgroundMusic(silentVerification);
            Audio = new AudioControls(Window, Music);
            Bulk = new BulkEditingControls(this, readClipboard ?? (() => Clipboard.GetText()), writeClipboard ?? (text => Clipboard.SetText(text)));
            Find<Button>("ThemeButton").Click += delegate { ToggleTheme(); };
            Window.Loaded += delegate { Music.Start(); };
            Window.Closed += delegate { Audio.Dispose(); Music.Dispose(); };
            Grid.CellEditEnding += delegate(object sender, DataGridCellEditEndingEventArgs args) {
                if (args.EditAction != DataGridEditAction.Cancel) return;
                // WPF cancels the editor, but a failed binding has already reached the cell model.
                // Restore its valid session value so Escape also clears the validation state.
                var row = args.Row.Item as RowViewModel;
                int statistic = Grid.Columns.IndexOf(args.Column) - 2;
                if (row != null && statistic >= 0 && statistic < 14)
                    Window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.DataBind, new Action(delegate {
                        // Run after WPF has finished restoring the canceled editor binding.
                        row.Cells[statistic].Refresh(); UpdateStatus();
                    }));
            };
            Grid.RowEditEnding += delegate(object sender, DataGridRowEditEndingEventArgs args) {
                if (args.EditAction != DataGridEditAction.Cancel) return;
                var row = args.Row.Item as RowViewModel;
                if (row != null) row.Refresh();
                UpdateStatus();
            };
            Find<Button>("OpenButton").Click += delegate { OpenFromDialog(); };
            Find<Button>("SaveButton").Click += delegate { SaveFromDialog(); };
            Find<Button>("UndoButton").Click += delegate { Undo(); };
            Find<Button>("ResetButton").Click += delegate { Reset(); };
            search.TextChanged += delegate { if (ready && CommitPendingEdit()) FilterRows(); };
            tabs.SelectionChanged += delegate(object sender, SelectionChangedEventArgs args) {
                if (!ready || args.Source != tabs) return;
                if (CommitPendingEdit()) { selectedCategory = tabs.SelectedIndex; FilterRows(); }
                else { ready = false; tabs.SelectedIndex = selectedCategory; ready = true; }
            };
            Window.Closing += OnClosing;
            Window.PreviewKeyDown += OnKeyDown;
            ready = true;
        }
        public T Find<T>(string name) where T : FrameworkElement { return (T)Window.FindName(name); }
        public void ToggleTheme()
        {
            ThemePalette.Apply(!ThemePalette.IsDark);
            // A theme change must not reset pending text or commit any edits.
            foreach (var row in Rows) foreach (var cell in row.Cells) cell.RefreshTheme();
            Find<System.Windows.Shapes.Path>("SunIcon").Visibility = ThemePalette.IsDark ? Visibility.Visible : Visibility.Collapsed;
            Find<System.Windows.Shapes.Path>("MoonIcon").Visibility = ThemePalette.IsDark ? Visibility.Collapsed : Visibility.Visible;
            string themeAction = ThemePalette.IsDark ? "Switch to light mode" : "Switch to dark mode";
            Find<Button>("ThemeButton").ToolTip = themeAction;
            System.Windows.Automation.AutomationProperties.SetName(Find<Button>("ThemeButton"), themeAction);
            Comparison.Refresh();
        }
        private static DataTemplate Template(string body)
        {
            return (DataTemplate)XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>" + body + "</DataTemplate>");
        }
        private void BuildColumns()
        {
            Grid.Columns.Add(new DataGridTemplateColumn {
                Header = "No.", Width = 48, MinWidth = 48, IsReadOnly = true,
                CellTemplate = Template("<TextBlock Text='{Binding Number}' Foreground='{DynamicResource NumberText}' HorizontalAlignment='Center' VerticalAlignment='Center'/>") });
            Grid.Columns.Add(new DataGridTemplateColumn {
                Header = "Item", Width = 267, MinWidth = 267, IsReadOnly = true,
                CellTemplate = Template("<Grid Background='{DynamicResource Identity}' ToolTip='{Binding Detail}'><Grid.ColumnDefinitions><ColumnDefinition Width='91'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>" +
                    "<Image Source='{Binding Image}' Width='77' Height='57' Margin='6,0' Stretch='Uniform'/>" +
                    "<TextBlock Grid.Column='1' Text='{Binding Name}' Foreground='{DynamicResource NameText}' FontWeight='SemiBold' VerticalAlignment='Center' TextWrapping='Wrap' Margin='4,0,10,0'/></Grid>") });
            for (int index = 0; index < 14; index++)
            {
                string cell = "Cells[" + index + "]";
                var header = new TextBlock { Text = Statistics.Codes[index], ToolTip = Statistics.Names[index] };
                Grid.Columns.Add(new DataGridTemplateColumn {
                    Header = header, Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 61,
                    CellTemplate = Template("<Border Background='{Binding " + cell + ".Background}' ToolTip='{Binding " + cell + ".Hint}'>" +
                        "<TextBlock Text='{Binding " + cell + ".Text}' FontSize='15' TextAlignment='Center' TextTrimming='CharacterEllipsis' Margin='4,0' VerticalAlignment='Center'/></Border>"),
                    CellEditingTemplate = Template("<TextBox Style='{DynamicResource StatEditor}' Text='{Binding " + cell + ".Text, Mode=TwoWay, UpdateSourceTrigger=LostFocus, ValidatesOnDataErrors=True}'/>") });
            }
        }
        public void LoadFile(string path)
        {
            // Parse and resolve every image before replacing the currently open session.
            var nextSession = EditSession.Open(path);
            var nextRows = nextSession.File.Records.Select(record => new RowViewModel(record, nextSession, UpdateStatus)).ToList();
            Session = nextSession; Rows = nextRows;
            Hex.Load(Session, Rows);
            Comparison.Original.Load(Session, Rows);
            search.Text = "";
            Find<TextBlock>("FileNameText").Text = Path.GetFileName(path);
            Find<TextBlock>("FileDetailText").Text = "131 items · 14 statistics · " + nextSession.File.Length.ToString("N0") + " bytes";
            Find<TextBlock>("FileNameText").ToolTip = Session.SourcePath;
            FilterRows(); UpdateStatus();
        }
        private void OpenFromDialog()
        {
            if (!CommitPendingEdit()) return;
            var picker = new OpenFileDialog { Title = "Open Performance.bin", Filter = "Performance files (*.bin)|*.bin|All files (*.*)|*.*", CheckFileExists = true };
            if (picker.ShowDialog(Window) != true) return;
            if (!ConfirmUnsaved()) return;
            try { LoadFile(picker.FileName); }
            catch (Exception error) { ShowError("Unable to open file", UserMessages.For(error)); }
        }
        private bool SaveFromDialog()
        {
            if (Session == null || !CommitPendingEdit()) return false;
            var picker = new SaveFileDialog {
                Title = "Save a new copy", Filter = "Binary file (*.bin)|*.bin", DefaultExt = ".bin", AddExtension = true,
                FileName = "Performance_modified.bin", InitialDirectory = Path.GetDirectoryName(Session.SourcePath), OverwritePrompt = false
            };
            if (picker.ShowDialog(Window) != true) return false;
            try
            {
                Session.SaveCopy(picker.FileName); UpdateStatus();
                Find<TextBlock>("StatusText").Text = "Copy saved: " + picker.FileName;
                return true;
            }
            catch (Exception error) { ShowError("Copy not saved", UserMessages.For(error)); return false; }
        }
        public bool CommitPendingEdit()
        {
            bool committed = Grid.CommitEdit(DataGridEditingUnit.Cell, true);
            committed = Grid.CommitEdit(DataGridEditingUnit.Row, true) && committed;
            bool valid = Rows.All(row => row.Cells.All(cell => cell.IsValid));
            if (!committed || !valid)
            {
                Find<TextBlock>("StatusText").Text = "Correct the highlighted cell: enter a whole number from 0 to 4294967295.";
                return false;
            }
            return true;
        }
        public void SelectCategory(int index) { tabs.SelectedIndex = index; }
        public bool ShowSelectedStatisticInHex()
        {
            var active = Bulk.ActiveCell;
            var item = active.Item as RowViewModel;
            int statistic = active.Column == null ? -1 : Grid.Columns.IndexOf(active.Column) - 2;
            if (Session == null || Grid.Visibility != Visibility.Visible || item == null || !Grid.Items.Contains(item) || statistic < 0 || statistic >= 14) return false;
            if (!CommitPendingEdit()) return false;
            int offset = item.Record.Offset + statistic * 4;
            search.Text = "";
            tabs.SelectedIndex = 4;
            Find<Grid>("RootLayout").UpdateLayout();
            Comparison.ShowStatistic(offset);
            Find<TextBlock>("CategoryDescription").Text = item.Name + " · " + Statistics.Codes[statistic] + " · 0x" + offset.ToString("X8");
            Find<TextBlock>("StatusText").Text = Statistics.Names[statistic] + " · selected bytes 0x" + offset.ToString("X8") + "–0x" + (offset + 3).ToString("X8");
            return true;
        }
        public void NavigateToStatistic(HexCell cell)
        {
            if (cell.Bookmark == null || !CommitPendingEdit()) return;
            search.Text = "";
            string[] categories = { "PTBD", "PTDV", "PTWG", "PTTR" };
            tabs.SelectedIndex = Array.IndexOf(categories, cell.Bookmark.Record.Entry.Section);
            Grid.ScrollIntoView(cell.Bookmark, Grid.Columns[cell.StatisticIndex + 2]);
            Grid.UpdateLayout();
            Grid.CurrentCell = new DataGridCellInfo(cell.Bookmark, Grid.Columns[cell.StatisticIndex + 2]);
            Grid.Focus(); Grid.BeginEdit();
        }
        private void FilterRows()
        {
            if (!ready) return;
            string section = (string)((TabItem)tabs.SelectedItem).Tag;
            string query = search.Text.Trim();
            bool isHex = section == "HEX";
            Find<Button>("StatsActionsButton").Visibility = isHex ? Visibility.Collapsed : Visibility.Visible;
            Find<Grid>("HexComparisonPanel").Visibility = isHex ? Visibility.Visible : Visibility.Collapsed;
            Find<StackPanel>("ChangeLegend").Visibility = isHex ? Visibility.Collapsed : Visibility.Visible;
            Find<TextBlock>("HexLegend").Visibility = isHex ? Visibility.Visible : Visibility.Collapsed;
            Grid.Visibility = isHex ? Visibility.Collapsed : Visibility.Visible;
            Hex.Grid.Visibility = isHex ? Visibility.Visible : Visibility.Collapsed;
            Find<TextBlock>("ViewHint").Text = isHex ? "Hover for bookmarks · double-click a highlighted byte to edit" : "Right-click for actions · Ctrl+H to view statistic in Hex";
            search.ToolTip = isHex ? "Search an item name or enter an offset such as 0x1354" : "Filter the current tab by name";
            if (isHex)
            {
                Comparison.Filter(query);
                Find<TextBlock>("CategoryDescription").Text = "Hex" + (Session != null ? " · " + Session.File.Length.ToString("N0") + " bytes · 131 bookmarks" : "");
                Find<Border>("EmptyPanel").Visibility = Session == null || Hex.Grid.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                Find<TextBlock>("EmptyTitle").Text = Session == null ? "Start with your file." : "No bookmark or offset found.";
                Find<TextBlock>("EmptyDescription").Text = Session == null ? "Open Performance.bin to inspect its bytes and bookmarks." : "Search an item name or an offset inside this file, such as 0x1354.";
                return;
            }
            var filtered = Rows.Where(row => row.Record.Entry.Section == section && row.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            Grid.ItemsSource = filtered;
            var descriptions = new Dictionary<string, string> {
                { "PTBD", "Vehicle bodies · karts, bikes and ATVs" }, { "PTDV", "Characters · including DLC additions and the second Mii record" },
                { "PTWG", "Gliders" }, { "PTTR", "Wheels" }
            };
            Find<TextBlock>("CategoryDescription").Text = descriptions[section] + (Session != null ? "  /  " + filtered.Count : "");
            Find<Border>("EmptyPanel").Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            Find<TextBlock>("EmptyTitle").Text = Session == null ? "Start with your file." : "No items found.";
            Find<TextBlock>("EmptyDescription").Text = Session == null ? "Open Performance.bin to view images and statistics. Changes are saved to a new copy." : "Try another name or clear the Search field.";
        }
        public void UpdateStatus()
        {
            if (Session == null) return;
            Comparison.Refresh();
            bool valid = Rows.All(row => row.Cells.All(cell => cell.IsValid));
            Find<Button>("SaveButton").IsEnabled = valid;
            Find<Button>("UndoButton").IsEnabled = valid && Session.CanUndo;
            Find<Button>("ResetButton").IsEnabled = Session.ChangedCount > 0 || !valid;
            string changes = Session.ChangedCount == 1 ? "1 statistic changed" : Session.ChangedCount + " statistics changed";
            Find<TextBlock>("StatusText").Text = !valid ? "Invalid value: correct the highlighted cell before saving." :
                Session.ChangedCount == 0 ? "Original values from the opened file · no changes" : changes + (Session.HasUnexportedChanges ? " · not yet saved to a copy" : " · copy saved");
            Window.Title = "mk8deluxe editor — " + Path.GetFileName(Session.SourcePath) + (Session.HasUnexportedChanges ? " *" : "");
        }
        private void Undo()
        {
            if (Session == null || !CommitPendingEdit()) return;
            Session.Undo(); foreach (var row in Rows) row.Refresh(); UpdateStatus();
        }
        private void Reset()
        {
            if (Session == null) return;
            if (EnglishDialog.Show(Window, "Reset all values to the opened file?", "Reset changes", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            Grid.CancelEdit(DataGridEditingUnit.Cell); Grid.CancelEdit(DataGridEditingUnit.Row);
            Session.Reset(); foreach (var row in Rows) row.Refresh(); UpdateStatus();
        }
        private bool ConfirmUnsaved()
        {
            if (Session == null || !Session.HasUnexportedChanges) return true;
            var choice = EnglishDialog.Show(Window, "Some changes have not been saved to a copy. Would you like to save them?", "Unsaved changes", MessageBoxButton.YesNoCancel);
            if (choice == MessageBoxResult.Cancel) return false;
            return choice == MessageBoxResult.No || SaveFromDialog();
        }
        private void OnClosing(object sender, CancelEventArgs args)
        {
            if (!CommitPendingEdit())
            {
                if (EnglishDialog.Show(Window, "The cell contains an invalid value. Discard that input and close?", "Invalid value", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                { args.Cancel = true; return; }
                Grid.CancelEdit(DataGridEditingUnit.Cell); Grid.CancelEdit(DataGridEditingUnit.Row);
                foreach (var row in Rows) row.Refresh();
            }
            if (!ConfirmUnsaved()) args.Cancel = true;
        }
        private void OnKeyDown(object sender, KeyEventArgs args)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && args.Key == Key.H && Grid.IsKeyboardFocusWithin)
            { ShowSelectedStatisticInHex(); args.Handled = true; return; }
            if (Keyboard.Modifiers == ModifierKeys.Control && args.Key == Key.O) { OpenFromDialog(); args.Handled = true; }
            else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && args.Key == Key.S) { SaveFromDialog(); args.Handled = true; }
            else if (Keyboard.Modifiers == ModifierKeys.Control && args.Key == Key.Z && !(Keyboard.FocusedElement is TextBox)) { Undo(); args.Handled = true; }
        }
        private void ShowError(string title, string message) { EnglishDialog.Show(Window, message, title, MessageBoxButton.OK); }
    }
}
