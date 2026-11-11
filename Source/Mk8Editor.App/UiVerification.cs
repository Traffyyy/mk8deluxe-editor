using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Threading;

namespace Mk8Editor.App
{
    /// <summary>Exercises and renders the real WPF view without showing a desktop window.</summary>
    public static class UiVerification
    {
        public static int Run(string fixture, string output)
        {
            Directory.CreateDirectory(output);
            string testClipboard = "";
            var controller = new WindowController(true, () => testClipboard, text => testClipboard = text);
            Render(controller, 1360, 800, Path.Combine(output, "empty.png"));
            controller.LoadFile(fixture);
            VerifyCopyAfterFocusLeavesGrid(controller, () => testClipboard, text => testClipboard = text);
            Check(controller.Rows.Count == 131, "All rows loaded");
            Check(controller.Rows.All(row => row.Image.PixelWidth > 0 && row.Image.PixelHeight > 0), "All images decoded");
            Check(controller.Rows.All(row => row.Cells.All(stat => ((SolidColorBrush)stat.Background).Color == Colors.White)), "All unchanged statistic columns start white, including MT and all four speed columns");
            int[] counts = { 41, 53, 15, 22 };
            string[] names = { "kart", "personaggi", "glider", "ruote" };
            for (int index = 0; index < 4; index++)
            {
                controller.SelectCategory(index);
                Check(controller.Grid.Items.Count == counts[index], "Category count " + names[index]);
                Render(controller, 1360, 800, Path.Combine(output, names[index] + ".png"));
            }
            controller.SelectCategory(0);
            controller.Find<TextBox>("SearchBox").Text = "Master Cycle";
            Check(controller.Grid.Items.Count == 2, "Search uses name, not stat equality");
            controller.Find<TextBox>("SearchBox").Text = "no_such_record";
            Check(controller.Grid.Items.Count == 0, "Empty search result");
            controller.Find<TextBox>("SearchBox").Text = "";
            // Exercise the actual DataGrid editing template and binding, not only its model.
            controller.Grid.ScrollIntoView(controller.Rows[0], controller.Grid.Columns[2]);
            controller.Grid.UpdateLayout();
            controller.Grid.CurrentCell = new DataGridCellInfo(controller.Rows[0], controller.Grid.Columns[2]);
            Check(controller.Grid.BeginEdit(), "DataGrid starts cell editing");
            controller.Grid.UpdateLayout();
            var editor = FindChild<TextBox>(controller.Grid.Columns[2].GetCellContent(controller.Rows[0]));
            Check(editor != null, "Native cell editor is present");
            editor.Text = "8";
            editor.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            Check(controller.CommitPendingEdit() && controller.Session.GetValue(controller.Rows[0].Record.Offset) == 8, "DataGrid binding commits edited value");
            controller.Session.Undo(); controller.Rows[0].Refresh();
            Check(controller.Grid.BeginEdit(), "DataGrid can edit again");
            controller.Grid.UpdateLayout();
            editor = FindChild<TextBox>(controller.Grid.Columns[2].GetCellContent(controller.Rows[0]));
            editor.Text = "-1";
            editor.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            Check(!controller.CommitPendingEdit(), "Real editor rejects invalid input");
            bool cellCanceled = controller.Grid.CancelEdit(DataGridEditingUnit.Cell);
            bool rowCanceled = controller.Grid.CancelEdit(DataGridEditingUnit.Row);
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Loaded, new Action(delegate { }));
            Check(cellCanceled && rowCanceled && controller.Rows[0].Cells[0].IsValid, "Cancel restores valid input");
            var cell = controller.Rows[0].Cells[0];
            string baseline = cell.Text;
            cell.Text = "-1";
            Check(!cell.IsValid && !controller.Find<Button>("SaveButton").IsEnabled && !controller.CommitPendingEdit(), "Invalid input blocks saving");
            controller.SelectCategory(1);
            Check(controller.Find<TabControl>("CategoryTabs").SelectedIndex == 0, "Invalid edit keeps category and visible rows consistent");
            cell.Text = "4294967296"; Check(!cell.IsValid, "Overflow blocked");
            cell.Text = "7"; Check(cell.IsValid && controller.Session.ChangedCount == 1, "Valid cell edit applied");
            Render(controller, 1100, 720, Path.Combine(output, "compact-modified.png"));
            controller.Session.Undo(); controller.Rows[0].Refresh();
            Check(cell.Text == baseline, "Undo reflected in UI");
            controller.UpdateStatus();
            byte[] beforeTheme = controller.Session.Serialize();
            controller.ToggleTheme();
            Check(ThemePalette.IsDark && AutomationProperties.GetName(controller.Find<Button>("ThemeButton")) == "Switch to light mode", "Theme icon updates");
            Check(((SolidColorBrush)cell.Background).Color == ((SolidColorBrush)ThemePalette.GetBrush("Statistic")).Color, "Cells use the dark palette");
            Check(controller.Rows.All(row => row.Cells.All(stat => ((SolidColorBrush)stat.Background).Color == ((SolidColorBrush)ThemePalette.GetBrush("Statistic")).Color)), "All unchanged columns share the same neutral background in dark mode");
            VerifyMenuAppearance(controller, output, "dark");
            for (int index = 0; index < 4; index++)
            {
                controller.SelectCategory(index);
                Render(controller, 1360, 800, Path.Combine(output, "dark-" + names[index] + ".png"));
            }
            controller.SelectCategory(0);
            cell.Text = "7";
            Render(controller, 960, 620, Path.Combine(output, "dark-compact.png"));
            controller.Session.Undo(); controller.Rows[0].Refresh();
            cell.Text = "-1";
            controller.ToggleTheme();
            Check(!cell.IsValid && cell.Text == "-1" && !controller.Find<Button>("SaveButton").IsEnabled, "Theme toggle preserves invalid pending input");
            cell.Refresh(); controller.UpdateStatus();
            VerifyMenuAppearance(controller, output, "light");
            Check(beforeTheme.SequenceEqual(controller.Session.Serialize()), "Theme toggle never changes binary values");
            Check(AutomationProperties.GetName(controller.Find<Button>("OpenButton")) == "Open Performance.bin", "Open icon accessible name");
            Check(AutomationProperties.GetName(controller.Find<Button>("SaveButton")) == "Save a new copy" && controller.Find<Button>("SaveButton").Content is Viewbox, "Save uses an accessible icon");
            Check(controller.Find<TextBlock>("StatusText").Text.StartsWith("Original values"), "English status message");
            Check(controller.Rows.All(row => !row.Name.Contains("costumi") && !row.Detail.Contains("riconciliato")), "Catalog notes are English");
            var trackTitle = controller.Find<TextBlock>("TrackTitle");
            string renderedTitle = new System.Windows.Documents.TextRange(trackTitle.ContentStart, trackTitle.ContentEnd).Text;
            Check(renderedTitle == BackgroundMusic.TrackTitle, "Exact Unicode track title displayed: " + renderedTitle);
            Check(controller.Window.Icon != null, "Metal Mario window icon loaded");
            Check(trackTitle.Visibility == Visibility.Visible, "Track title visible while audio enabled");
            Point songPosition = trackTitle.TranslatePoint(new Point(), controller.Find<Grid>("RootLayout"));
            Point speakerBottom = controller.Find<Button>("AudioButton").TranslatePoint(new Point(0, controller.Find<Button>("AudioButton").ActualHeight), controller.Find<Grid>("RootLayout"));
            Check(songPosition.Y >= speakerBottom.Y && Math.Abs(songPosition.X - speakerBottom.X) < 1, "Track title positioned directly below audio icon");
            var audioButton = controller.Find<Button>("AudioButton");
            var volumeSlider = controller.Find<Slider>("VolumeSlider");
            audioButton.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
            Check(controller.Audio.VolumePanelVisible, "Hover opens volume control");
            volumeSlider.Value = 80;
            Check(Math.Abs(controller.Music.Volume - 0.8) < 0.001 && controller.Find<TextBlock>("VolumeValue").Text == "80%", "Slider changes volume and percentage");
            Check(controller.Music.OutputVolume == 0, "Verification volume remains silent after slider changes");
            Render(controller, 1360, 800, Path.Combine(output, "audio-volume-light.png"));
            audioButton.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });
            WaitUntil(() => false, 100);
            Check(controller.Audio.VolumePanelVisible, "Hover exit has a grace period");
            controller.Find<Border>("VolumePanel").RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
            WaitUntil(() => false, 350);
            Check(controller.Audio.VolumePanelVisible, "Moving into the slider panel keeps it open");
            controller.Find<Border>("VolumePanel").RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });
            Check(WaitUntil(() => !controller.Audio.VolumePanelVisible, 1000), "Leaving audio controls hides volume panel");
            volumeSlider.Value = 0; Check(controller.Music.Volume == 0, "Volume lower limit");
            volumeSlider.Value = 100; Check(controller.Music.Volume == 1, "Volume upper limit");
            volumeSlider.Value = 80;
            controller.ToggleTheme();
            Render(controller, 960, 620, Path.Combine(output, "track-title-compact.png"));
            controller.Audio.ShowVolume();
            Render(controller, 960, 620, Path.Combine(output, "audio-volume-dark.png"));
            controller.Audio.HideVolume(); controller.ToggleTheme();
            Check(beforeTheme.SequenceEqual(controller.Session.Serialize()), "Audio controls do not change binary values");
            VerifyStatisticNavigation(controller, output);
            VerifyHex(controller, output);
            VerifyBulkEditing(controller, () => testClipboard, text => testClipboard = text, output);

            // Decode and play through the real Windows media engine at zero volume.
            // This checks playback without making sound on the user's desktop.
            controller.Music.Start();
            Check(WaitUntil(() => controller.Music.IsReady || controller.Music.Error != null, 15000), "Audio decoder responds");
            Check(controller.Music.IsReady, "Bundled WAV decodes: " + controller.Music.Error);
            Check(controller.Music.Duration.TotalSeconds > 150, "Full song duration loaded");
            Check(WaitUntil(() => controller.Music.Position.TotalMilliseconds > 200, 5000), "Audio playback advances");
            audioButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(!controller.Music.Enabled && AutomationProperties.GetName(audioButton) == "Turn audio on", "Single click turns audio off");
            Check(((SolidColorBrush)audioButton.Background).Color == (Color)ColorConverter.ConvertFromString("#BF303D"), "Audio off icon is red");
            Check(trackTitle.Visibility != Visibility.Visible, "Track title hidden while audio off");
            Render(controller, 1360, 800, Path.Combine(output, "audio-off.png"));
            TimeSpan paused = controller.Music.Position;
            WaitUntil(() => false, 350);
            Check(Math.Abs((controller.Music.Position - paused).TotalMilliseconds) < 100, "Audio off pauses playback");
            audioButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(controller.Music.Enabled && AutomationProperties.GetName(audioButton) == "Turn audio off", "Single click turns audio on");
            Check(((SolidColorBrush)audioButton.Background).Color == (Color)ColorConverter.ConvertFromString("#147D44"), "Audio on icon is green");
            Check(trackTitle.Visibility == Visibility.Visible, "Track title returns when audio enabled");
            Check(Math.Abs(controller.Music.Volume - 0.8) < 0.001, "Audio toggle preserves chosen volume");
            Check(WaitUntil(() => controller.Music.Position > paused + TimeSpan.FromMilliseconds(200), 5000), "Audio on resumes playback");
            controller.Audio.Dispose(); controller.Music.Dispose();
            File.WriteAllText(Path.Combine(output, "ui-verification.txt"), "PASS: 131 images and records; four item tabs plus two-pane Hex; original/current views of one file with synchronized vertical and horizontal scrolling; precise changed-byte outlines; wide scrollbars and minimum 48-DIP grips; high-contrast light bookmarks; exact bookmark byte coverage with headers excluded; alternating colors across all 131 bookmarks in both themes, stable while filtering and editing; byte values follow edits and undo; bookmark name/image/statistic tooltip; name and offset search; double-click navigates to exact statistic; copy/paste all 14 statistics across items and categories; multi-cell editing with one-step Undo; invalid clipboard and batch input rejected; actual DataGrid editing and validation; English icon controls including Save; both toolbar rows aligned at all captured window sizes; light/dark themes; exact Unicode song title below the speaker and hidden when audio off; Metal Mario portrait window icon; volume controls; actual bundled WAV decoding, playback, pause and resume at zero volume. No desktop window shown or system clipboard changed.");
            return 0;
        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void VerifyCopyAfterFocusLeavesGrid(WindowController controller, Func<string> readClipboard, Action<string> writeClipboard)
        {
            controller.SelectCategory(0);
            var row = controller.Rows[0];
            var selected = new DataGridCellInfo(row, controller.Grid.Columns[2]);
            controller.Grid.SelectedCells.Clear(); controller.Grid.SelectedCells.Add(selected);
            controller.Grid.CurrentCell = selected;
            // WPF clears CurrentCell when keyboard focus moves into the toolbar or popup.
            controller.Grid.CurrentCell = new DataGridCellInfo();
            var menu = controller.Grid.ContextMenu;
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            var copyItem = menu.Items.OfType<MenuItem>().First(item => item.InputGestureText == "Ctrl+Shift+C");
            Check(copyItem.IsEnabled, "Copy remains enabled when opening the actions menu moves focus out of the grid");
            writeClipboard(""); copyItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            var copied = Mk8Editor.Core.StatisticTransfer.Parse(readClipboard());
            Check(copied.SequenceEqual(Enumerable.Range(0, 14).Select(index => controller.Session.GetValue(row.Record.Offset + index * 4))), "Actual Copy menu action writes the selected item's 14 current values after focus loss");
            byte[] beforePaste = controller.Session.Serialize();
            var target = controller.Rows[3];
            var targetCell = new DataGridCellInfo(target, controller.Grid.Columns[1]);
            controller.Grid.SelectedCells.Clear(); controller.Grid.SelectedCells.Add(targetCell); controller.Grid.CurrentCell = targetCell;
            controller.Grid.CurrentCell = new DataGridCellInfo();
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            var pasteItem = menu.Items.OfType<MenuItem>().First(item => item.InputGestureText == "Ctrl+Shift+V");
            Check(pasteItem.IsEnabled, "Paste retains its destination when the menu takes focus");
            pasteItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(Enumerable.Range(0, 14).All(index => controller.Session.GetValue(target.Record.Offset + index * 4) == copied[index]), "Actual Copy-to-Paste menu workflow transfers all 14 values");
            controller.Session.Undo(); foreach (var item in controller.Rows) item.Refresh(); controller.UpdateStatus();
            Check(beforePaste.SequenceEqual(controller.Session.Serialize()), "One Undo reverses the menu paste without modifying the original");
            controller.Grid.SelectedCells.Clear(); controller.Grid.SelectedCells.Add(selected); controller.Grid.CurrentCell = selected;
            controller.Grid.CurrentCell = new DataGridCellInfo(); menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            var hexItem = menu.Items.OfType<MenuItem>().First(item => item.InputGestureText == "Ctrl+H");
            Check(hexItem.IsEnabled, "Show in Hex remains enabled after menu focus loss");
            hexItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(controller.Find<TabControl>("CategoryTabs").SelectedIndex == 4 && controller.Hex.Grid.SelectedCells.Count == 4, "Actual Hex menu action retains the selected statistic after focus loss");
            controller.SelectCategory(0); controller.Grid.CurrentCell = new DataGridCellInfo();
            controller.Grid.SelectedCells.Clear();
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            Check(!copyItem.IsEnabled, "Clearing selection disables Copy rather than reusing an old item");
            controller.UpdateStatus();
        }
        private static void VerifyMenuAppearance(WindowController controller, string output, string theme)
        {
            controller.SelectCategory(0);
            var selected = new DataGridCellInfo(controller.Rows[0], controller.Grid.Columns[2]);
            controller.Grid.SelectedCells.Clear(); controller.Grid.SelectedCells.Add(selected); controller.Grid.CurrentCell = selected;
            controller.Grid.CurrentCell = new DataGridCellInfo();
            var menu = controller.Grid.ContextMenu;
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            menu.ApplyTemplate(); menu.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
            menu.Arrange(new Rect(new Point(), menu.DesiredSize)); menu.UpdateLayout();
            var surface = menu.Template.FindName("MenuSurface", menu) as Border;
            Check(!menu.HasDropShadow && surface != null && ((SolidColorBrush)surface.Background).Color == ((SolidColorBrush)ThemePalette.GetBrush("Surface")).Color, "Menu chrome follows theme without the white system shadow or gutter");
            Check(menu.ActualWidth > 100 && menu.ActualHeight > 100 && !menu.IsOpen, "Native menu renders off-screen without opening a popup");
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth), (int)Math.Ceiling(menu.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(menu);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(Path.Combine(output, "actions-menu-" + theme + ".png"))) encoder.Save(stream);
        }
        private static void VerifyStatisticNavigation(WindowController controller, string output)
        {
            byte[] before = controller.Session.Serialize();
            controller.SelectCategory(0);
            var source = controller.Rows[0];
            controller.Grid.CurrentCell = new DataGridCellInfo(source, controller.Grid.Columns[2]);
            source.Cells[0].Text = "-1";
            Check(!controller.ShowSelectedStatisticInHex() && controller.Find<TabControl>("CategoryTabs").SelectedIndex == 0, "Invalid input blocks Hex navigation");
            source.Refresh(); controller.UpdateStatus();
            controller.Grid.CurrentCell = new DataGridCellInfo(source, controller.Grid.Columns[1]);
            Check(!controller.ShowSelectedStatisticInHex(), "Item name is not treated as a statistic");
            string[] sections = { "PTBD", "PTDV", "PTWG", "PTTR" };
            for (int category = 0; category < sections.Length; category++)
            {
                controller.SelectCategory(category);
                var item = controller.Rows.Last(row => row.Record.Entry.Section == sections[category]);
                controller.Find<TextBox>("SearchBox").Text = item.Name;
                Render(controller, 960, 620, Path.Combine(output, "navigation-source.png"));
                controller.Grid.CurrentCell = new DataGridCellInfo(item, controller.Grid.Columns[15]);
                Check(controller.ShowSelectedStatisticInHex(), "Navigate to IV from " + sections[category]);
                PumpLayout(controller);
                int offset = item.Record.Offset + 13 * 4;
                Check(controller.Find<TabControl>("CategoryTabs").SelectedIndex == 4 && controller.Find<TextBox>("SearchBox").Text == "", "Navigation opens Hex and clears the item filter");
                foreach (var view in new[] { controller.Comparison.Original, controller.Hex })
                {
                    var addresses = view.Grid.SelectedCells.Select(cell => ((HexRow)cell.Item).Cells[view.Grid.Columns.IndexOf(cell.Column) - 1].Offset).OrderBy(address => address).ToArray();
                    Check(addresses.SequenceEqual(Enumerable.Range(offset, 4)), "Both panes select exactly the statistic's four bytes");
                    var row = view.Rows[offset / 16];
                    var first = view.Grid.Columns[offset % 16 + 1].GetCellContent(row);
                    var last = view.Grid.Columns[offset % 16 + 4].GetCellContent(row);
                    Check(first != null && last != null, "Navigation realizes the target bytes");
                    Point start = first.TranslatePoint(new Point(), view.Grid);
                    Point end = last.TranslatePoint(new Point(last.ActualWidth, last.ActualHeight), view.Grid);
                    Check(start.X >= 80 && end.X <= view.Grid.ActualWidth - 22 && start.Y >= 40 && end.Y <= view.Grid.ActualHeight - 20, "Selected statistic is visible in compact Hex viewport");
                }
                var left = FindChild<ScrollViewer>(controller.Comparison.Original.Grid);
                var right = FindChild<ScrollViewer>(controller.Hex.Grid);
                Check(Math.Abs(left.VerticalOffset - right.VerticalOffset) < 0.1 && Math.Abs(left.HorizontalOffset - right.HorizontalOffset) < 1, "Navigation keeps both axes aligned");
            }
            controller.ToggleTheme();
            Render(controller, 960, 620, Path.Combine(output, "statistic-to-hex-dark.png"));
            controller.ToggleTheme(); controller.SelectCategory(0);
            Check(before.SequenceEqual(controller.Session.Serialize()) && !controller.Session.CanUndo, "Statistic navigation changes no bytes or undo history");
        }
        private static void VerifyHex(WindowController controller, string output)
        {
            byte[] before = controller.Session.Serialize();
            controller.SelectCategory(4);
            Check(controller.Hex.Grid.Visibility == Visibility.Visible && controller.Grid.Visibility == Visibility.Collapsed, "Hex tab switches views");
            Check(controller.Find<StackPanel>("ChangeLegend").Visibility == Visibility.Collapsed && controller.Find<TextBlock>("HexLegend").Visibility == Visibility.Visible, "Hex legend describes bookmarks rather than value changes");
            Check(controller.Hex.Rows.Count == (before.Length + 15) / 16, "Hex covers complete file");
            Check(controller.Hex.Rows.Sum(row => row.Cells.Count(item => item.Bookmark != null)) == 131 * 56, "Hex bookmarks cover exactly 131 records");
            Func<int, HexCell> at = offset => controller.Hex.Rows[offset / 16].Cells[offset % 16];
            Check(at(0x1353).Bookmark == null && at(0x1344).Bookmark == null && at(0x27F4).Bookmark == null, "Headers never inherit item bookmarks");
            Check(at(0x1354).Name == "Standard Kart" && at(0x1354).Image == controller.Rows[0].Image, "Bookmark resolves correct name and image");
            Check(at(0x1354 + 55).StatisticIndex == 13 && at(0x1354 + 56).Bookmark == controller.Rows[1], "Record and statistic boundaries are exact");
            VerifyBookmarkColors(controller);
            Color initialColor = ((SolidColorBrush)at(0x1354).Background).Color;
            controller.Session.SetValue(0x1354, 0x12345678); controller.UpdateStatus();
            var originalCell = controller.Comparison.Original.Rows[0x1354 / 16].Cells[0x1354 % 16];
            Check(originalCell.Hex == before[0x1354].ToString("X2") && originalCell.Details.Contains("Original value:"), "Original pane retains opened bytes and original tooltip value");
            Check(at(0x1354).IsChanged && originalCell.IsChanged && !at(0x1358).IsChanged, "Both panes outline only changed bytes");
            Check(((SolidColorBrush)at(0x1354).Background).Color == initialColor, "Editing preserves bookmark color");
            Check(at(0x1354).Hex == "78" && at(0x1355).Hex == "56" && at(0x1356).Hex == "34" && at(0x1357).Hex == "12", "Hex uses current little-endian bytes");
            Check(at(0x1354).Details.Contains("305419896"), "Bookmark tooltip uses edited statistic value");
            controller.Session.Undo(); controller.Rows[0].Refresh(); controller.UpdateStatus();
            Check(at(0x1354).Hex == before[0x1354].ToString("X2"), "Hex follows undo");
            Check(!at(0x1354).IsChanged && !originalCell.IsChanged, "Undo clears difference markers");
            controller.Find<TextBox>("SearchBox").Text = "Pauline";
            Check(controller.Hex.Grid.Items.Count > 0 && controller.Hex.Grid.Items.Count < 6, "Hex bookmark-name search");
            Check(controller.Comparison.Original.Grid.Items.Count == controller.Hex.Grid.Items.Count, "Search filters both panes identically");
            Check(((SolidColorBrush)at(0x1354).Background).Color == initialColor, "Filtering preserves bookmark color");
            controller.Find<TextBox>("SearchBox").Text = "0x1354";
            Render(controller, 1360, 800, Path.Combine(output, "hex-bookmarks-light.png"));
            var hexRow = controller.Hex.Rows[0x1354 / 16];
            int column = 0x1354 % 16 + 1;
            controller.Hex.Grid.ScrollIntoView(hexRow, controller.Hex.Grid.Columns[column]); controller.Hex.Grid.UpdateLayout();
            var scroller = FindChild<ScrollViewer>(controller.Hex.Grid);
            // Off-screen rendering has no native bring-into-view route. Position the viewport explicitly.
            scroller.ScrollToVerticalOffset(0x1340 / 16); controller.Hex.Grid.UpdateLayout();
            Render(controller, 1360, 800, Path.Combine(output, "hex-bookmarks-light.png"));
            Check(scroller.VerticalOffset == 0x1340 / 16, "Light Hex preview includes adjacent complete bookmarks");
            VerifyComparisonScrolling(controller, scroller);
            controller.Session.SetValue(0x1354, 0x12345678); controller.UpdateStatus();
            Render(controller, 1360, 800, Path.Combine(output, "hex-diff-light.png"));
            var border = FindChild<Border>(controller.Hex.Grid.Columns[column].GetCellContent(hexRow));
            Check(border != null && border.ToolTip is ToolTip, "Real hexadecimal cell has bookmark tooltip");
            var tip = (ToolTip)border.ToolTip;
            var details = (FrameworkElement)tip.Content;
            tip.Content = null;
            details.DataContext = at(0x1354);
            var preview = new Border { Child = details, Padding = new Thickness(18), Background = ThemePalette.GetBrush("Surface") };
            System.Windows.Documents.TextElement.SetForeground(preview, ThemePalette.GetBrush("BodyText"));
            preview.Measure(new Size(400, 350)); preview.Arrange(new Rect(new Point(), preview.DesiredSize)); preview.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(preview.ActualWidth), (int)Math.Ceiling(preview.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(preview);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(Path.Combine(output, "bookmark-tooltip.png"))) encoder.Save(stream);
            preview.Child = null; tip.Content = details;
            controller.ToggleTheme();
            VerifyBookmarkColors(controller);
            Check(((SolidColorBrush)at(0x1354).Background).Color != initialColor, "Bookmark colors adapt to dark mode");
            Render(controller, 960, 620, Path.Combine(output, "hex-bookmarks-dark.png"));
            controller.Hex.Grid.ScrollIntoView(hexRow, controller.Hex.Grid.Columns[column]); controller.Hex.Grid.UpdateLayout();
            scroller.ScrollToVerticalOffset(0x1340 / 16); controller.Hex.Grid.UpdateLayout();
            Render(controller, 960, 620, Path.Combine(output, "hex-bookmarks-dark.png"));
            Check(scroller.VerticalOffset == 0x1340 / 16, "Dark Hex preview includes adjacent bookmarks");
            VerifyComparisonScrolling(controller, scroller);
            var leftScroll = FindChild<ScrollViewer>(controller.Comparison.Original.Grid);
            Check(scroller.ScrollableWidth > 0, "Compact comparison provides horizontal scrolling");
            scroller.ScrollToHorizontalOffset(100); PumpLayout(controller);
            Check(Math.Abs(leftScroll.HorizontalOffset - scroller.HorizontalOffset) < 1 && scroller.HorizontalOffset > 0, "Horizontal scrolling is synchronized from edited pane");
            leftScroll.ScrollToHorizontalOffset(40); PumpLayout(controller);
            Check(Math.Abs(leftScroll.HorizontalOffset - scroller.HorizontalOffset) < 1, "Horizontal scrolling is synchronized from original pane");
            scroller.ScrollToHorizontalOffset(0); PumpLayout(controller);
            controller.Session.Undo(); controller.Rows[0].Refresh(); controller.UpdateStatus();
            controller.Find<TextBox>("SearchBox").Text = "0x1354";
            Check(controller.Hex.Grid.Items.Count == controller.Hex.Rows.Count, "Hex address search retains file context");
            // Realize a fresh filtered viewport before dispatching synthetic hit events.
            controller.Find<TextBox>("SearchBox").Text = "Standard Kart";
            scroller.ScrollToHome(); controller.Hex.Grid.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Loaded, new Action(delegate { }));
            controller.Hex.Grid.CurrentCell = new DataGridCellInfo(hexRow, controller.Hex.Grid.Columns[column]);
            controller.Hex.Grid.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Control.MouseDoubleClickEvent });
            Check(controller.Find<TabControl>("CategoryTabs").SelectedIndex == 4, "Empty-grid double click never reuses selected bookmark");
            controller.Hex.Grid.ScrollIntoView(hexRow, controller.Hex.Grid.Columns[column]); controller.Hex.Grid.UpdateLayout();
            var header = FindChild<System.Windows.Controls.Primitives.DataGridColumnHeader>(controller.Hex.Grid);
            Check(header != null, "Hex header realized");
            controller.Hex.Grid.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Control.MouseDoubleClickEvent, Source = header });
            Check(controller.Find<TabControl>("CategoryTabs").SelectedIndex == 4, "Header double click never edits a statistic");
            var hit = FindChild<Border>(controller.Hex.Grid.Columns[column].GetCellContent(hexRow));
            Check(hit != null && hit.DataContext is HexCell, "Clicked byte has bookmark context");
            controller.Hex.Grid.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Control.MouseDoubleClickEvent, Source = hit });
            Check(controller.Find<TabControl>("CategoryTabs").SelectedIndex == 0 && controller.Grid.CurrentCell.Item == controller.Rows[0] && controller.Grid.CurrentCell.Column == controller.Grid.Columns[2], "Double click reaches exact editable statistic");
            controller.Grid.CancelEdit(DataGridEditingUnit.Cell); controller.Grid.CancelEdit(DataGridEditingUnit.Row);
            controller.ToggleTheme();
            Check(before.SequenceEqual(controller.Session.Serialize()), "Hex inspection and navigation preserve all bytes");
        }
        private static void PumpLayout(WindowController controller)
        {
            for (int pass = 0; pass < 3; pass++) {
                controller.Find<Grid>("RootLayout").UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Loaded, new Action(delegate { }));
            }
        }
        private static void VerifyComparisonScrolling(WindowController controller, ScrollViewer edited)
        {
            var original = FindChild<ScrollViewer>(controller.Comparison.Original.Grid);
            edited.ScrollToVerticalOffset(320); PumpLayout(controller);
            Check(original.VerticalOffset == edited.VerticalOffset && edited.VerticalOffset == 320, "Right-to-left synchronized vertical scrolling");
            original.ScrollToVerticalOffset(308); PumpLayout(controller);
            Check(original.VerticalOffset == edited.VerticalOffset && edited.VerticalOffset == 308, "Left-to-right synchronized vertical scrolling");
            foreach (var grid in new[] { controller.Hex.Grid, controller.Comparison.Original.Grid }) {
                var scrollbar = Descendants<System.Windows.Controls.Primitives.ScrollBar>(grid).First(bar => bar.Orientation == Orientation.Vertical);
                var thumb = FindChild<System.Windows.Controls.Primitives.Thumb>(scrollbar);
                Check(scrollbar.ActualWidth >= 24 && thumb.ActualHeight >= 48, "Hex scrollbar has a wide, usable grip");
            }
        }
        private static void VerifyBulkEditing(WindowController controller, Func<string> readClipboard, Action<string> writeClipboard, string output)
        {
            controller.SelectCategory(0); controller.Find<TextBox>("SearchBox").Text = "";
            byte[] before = controller.Session.Serialize();
            var source = controller.Rows[0];
            controller.Session.SetValue(source.Record.Offset, 123);
            controller.Session.SetValue(source.Record.Offset + 4, 456);
            controller.Grid.CurrentCell = new DataGridCellInfo(source, controller.Grid.Columns[2]);
            controller.Bulk.Copy();
            uint[] copied = Mk8Editor.Core.StatisticTransfer.Parse(readClipboard());
            Check(copied[0] == 123 && copied[1] == 456 && copied.Length == 14, "Copy captures current statistics, not the original baseline");
            controller.Session.Undo(); controller.Session.Undo(); source.Refresh();
            controller.Grid.SelectedCells.Clear();
            foreach (var target in controller.Rows.Skip(1).Take(2)) controller.Grid.SelectedCells.Add(new DataGridCellInfo(target, controller.Grid.Columns[1]));
            controller.Bulk.Paste();
            Check(controller.Rows.Skip(1).Take(2).All(row => Enumerable.Range(0, 14).All(index => controller.Session.GetValue(row.Record.Offset + index * 4) == copied[index])), "Paste applies all stats to both selected destination items");
            controller.Session.Undo(); foreach (var row in controller.Rows) row.Refresh(); controller.UpdateStatus();
            Check(before.SequenceEqual(controller.Session.Serialize()), "Single Undo restores a multi-item paste");
            writeClipboard("bad data");
            bool rejected = false;
            try { controller.Bulk.Paste(); } catch (FormatException) { rejected = true; }
            Check(rejected && before.SequenceEqual(controller.Session.Serialize()) && !controller.Session.CanUndo, "Invalid clipboard leaves selection data and history unchanged");
            writeClipboard(Mk8Editor.Core.StatisticTransfer.Format(copied));
            controller.SelectCategory(1);
            var character = controller.Rows.First(row => row.Record.Entry.Section == "PTDV");
            controller.Grid.SelectedCells.Clear(); controller.Grid.SelectedCells.Add(new DataGridCellInfo(character, controller.Grid.Columns[2]));
            controller.Bulk.Paste();
            Check(controller.Session.GetValue(character.Record.Offset) == copied[0], "Copied stats can be pasted across categories");
            controller.Session.Undo(); character.Refresh(); controller.SelectCategory(0);
            controller.Grid.SelectedCells.Clear();
            controller.Grid.SelectedCells.Add(new DataGridCellInfo(controller.Rows[0], controller.Grid.Columns[2]));
            controller.Grid.SelectedCells.Add(new DataGridCellInfo(controller.Rows[1], controller.Grid.Columns[5]));
            controller.Bulk.SetSelectedValue(321);
            Check(controller.Session.GetValue(controller.Rows[0].Record.Offset) == 321 && controller.Session.GetValue(controller.Rows[1].Record.Offset + 12) == 321 && controller.Session.ChangedCount == 2, "Bulk value changes exactly two non-contiguous selected statistics");
            Render(controller, 1360, 800, Path.Combine(output, "bulk-selected-statistics.png"));
            controller.Session.Undo(); foreach (var row in controller.Rows) row.Refresh(); controller.UpdateStatus();
            Check(before.SequenceEqual(controller.Session.Serialize()), "One Undo restores a multi-cell value edit");
            uint? accepted = null;
            var dialog = BulkValueDialog.Create(controller.Window, 2, 2, value => accepted = value);
            var input = FindChild<TextBox>((DependencyObject)dialog.Content);
            var apply = Descendants<Button>((DependencyObject)dialog.Content).First(button => button.Name == "ApplyBulkValue");
            input.Text = "-1"; Check(!apply.IsEnabled, "Batch dialog rejects negative input");
            input.Text = "4294967296"; Check(!apply.IsEnabled, "Batch dialog rejects overflowing input");
            input.Text = "4294967295"; Check(apply.IsEnabled, "Batch dialog accepts full uint32 range");
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(accepted == UInt32.MaxValue, "Batch dialog passes the validated value");
        }
        private static System.Collections.Generic.IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent is T) yield return (T)parent;
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
                foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(parent, index))) yield return child;
        }
        private static void VerifyBookmarkColors(WindowController controller)
        {
            if (ThemePalette.IsDark)
                foreach (string key in new[] { "HexBookmarkEven", "HexBookmarkOdd" }) {
                    var background = ((SolidColorBrush)ThemePalette.GetBrush(key)).Color;
                    Check(Contrast(background, ((SolidColorBrush)ThemePalette.GetBrush("BodyText")).Color) >= 4.5, "Night bookmark text remains readable");
                    Check(Contrast(background, ((SolidColorBrush)ThemePalette.GetBrush("Surface")).Color) >= 2, "Night bookmarks stand out from unbookmarked bytes");
                }
            Func<int, HexCell> at = offset => controller.Hex.Rows[offset / 16].Cells[offset % 16];
            Color? previous = null;
            Color surface = ((SolidColorBrush)ThemePalette.GetBrush("Surface")).Color;
            foreach (var item in controller.Rows.OrderBy(row => row.Record.Offset))
            {
                Color color = ((SolidColorBrush)at(item.Record.Offset).Background).Color;
                Check(color != surface && (!previous.HasValue || color != previous.Value), "Consecutive bookmarks have distinct colors: " + item.Name);
                for (int index = 0; index < 56; index++)
                    Check(((SolidColorBrush)at(item.Record.Offset + index).Background).Color == color, "Entire record has a uniform bookmark color: " + item.Name);
                previous = color;
            }
            Check(controller.Hex.Rows.SelectMany(row => row.Cells).Where(cell => cell.Bookmark == null)
                .All(cell => ((SolidColorBrush)cell.Background).Color == surface), "Headers and padding remain neutral");
        }
        private static double Contrast(Color first, Color second)
        {
            Func<byte, double> linear = channel => channel / 255.0 <= 0.04045 ? channel / 255.0 / 12.92 : Math.Pow((channel / 255.0 + 0.055) / 1.055, 2.4);
            Func<Color, double> luminance = color => 0.2126 * linear(color.R) + 0.7152 * linear(color.G) + 0.0722 * linear(color.B);
            double a = luminance(first), b = luminance(second);
            return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
        }
        private static void VerifyToolbarAlignment(WindowController controller)
        {
            var root = controller.Find<Grid>("RootLayout");
            Check(!Descendants<TextBlock>(root).Any(text => text.Text == "mk8deluxe editor"), "Redundant header title removed");
            foreach (string name in new[] { "AudioButton", "ThemeButton", "OpenButton", "SaveButton", "UndoButton", "ResetButton" }) {
                var button = controller.Find<Button>(name);
                var icon = FindChild<Viewbox>(button);
                Check(icon != null && icon.ActualHeight <= button.ActualHeight - button.Padding.Top - button.Padding.Bottom - 2 && icon.ActualWidth <= button.ActualWidth - button.Padding.Left - button.Padding.Right - 2, "Icon fits inside button content area: " + name);
            }
            Func<FrameworkElement, Point> center = element => element.TranslatePoint(new Point(element.ActualWidth / 2, element.ActualHeight / 2), root);
            string[] upper = { "AudioButton", "ThemeButton", "OpenButton" };
            string[] lower = { "UndoButton", "ResetButton", "SearchIcon" };
            for (int index = 0; index < upper.Length; index++)
                Check(Math.Abs(center(controller.Find<FrameworkElement>(upper[index])).X - center(controller.Find<FrameworkElement>(lower[index])).X) < 0.6, "Toolbar columns align: " + upper[index]);
            var save = controller.Find<Button>("SaveButton");
            double top = center(save).Y;
            foreach (string name in upper)
                Check(Math.Abs(center(controller.Find<Button>(name)).Y - top) < 0.6, "Top icons share a center line");
            var search = controller.Find<Border>("SearchField");
            Check(Math.Abs(save.TranslatePoint(new Point(save.ActualWidth, 0), root).X - search.TranslatePoint(new Point(search.ActualWidth, 0), root).X) < 0.6, "Save and search align at right edge");
            Check(Math.Abs(center(controller.Find<Button>("UndoButton")).Y - center(search).Y) < 0.6 && Math.Abs(center(controller.Find<Button>("ResetButton")).Y - center(search).Y) < 0.6, "Lower commands share a center line");
        }
        private static bool WaitUntil(Func<bool> condition, int milliseconds)
        {
            var frame = new DispatcherFrame();
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
            timer.Tick += delegate { if (condition() || DateTime.UtcNow >= deadline) frame.Continue = false; };
            timer.Start(); Dispatcher.PushFrame(frame); timer.Stop(); return condition();
        }
        private static T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            var match = parent as T;
            if (match != null) return match;
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                match = FindChild<T>(VisualTreeHelper.GetChild(parent, index));
                if (match != null) return match;
            }
            return null;
        }
        private static void Render(WindowController controller, int width, int height, string destination)
        {
            var content = (FrameworkElement)controller.Window.Content;
            content.Width = width; content.Height = height;
            content.Measure(new Size(width, height)); content.Arrange(new Rect(0, 0, width, height)); content.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Loaded, new Action(delegate { }));
            content.UpdateLayout();
            VerifyToolbarAlignment(controller);
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = System.IO.File.Create(destination)) encoder.Save(stream);
        }
    }
}
