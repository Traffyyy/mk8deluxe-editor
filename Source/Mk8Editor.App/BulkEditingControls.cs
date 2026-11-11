using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Mk8Editor.Core;

namespace Mk8Editor.App
{
    /// <summary>Selection-aware commands. Clipboard access is isolated for silent verification.</summary>
    public sealed class BulkEditingControls
    {
        private readonly WindowController controller;
        private readonly Func<string> readClipboard;
        private readonly Action<string> writeClipboard;
        private readonly ContextMenu menu;
        private readonly MenuItem copy;
        private readonly MenuItem paste;
        private readonly MenuItem setValue;
        private readonly MenuItem showInHex;
        private DataGridCellInfo lastActiveCell;

        public BulkEditingControls(WindowController controller, Func<string> readClipboard, Action<string> writeClipboard)
        {
            this.controller = controller; this.readClipboard = readClipboard; this.writeClipboard = writeClipboard;
            copy = new MenuItem { Header = "Copy all 14 stats", InputGestureText = "Ctrl+Shift+C" };
            paste = new MenuItem { Header = "Paste all 14 stats into selected items", InputGestureText = "Ctrl+Shift+V" };
            setValue = new MenuItem { Header = "Set selected statistics...", InputGestureText = "Ctrl+Enter" };
            showInHex = new MenuItem { Header = "Show statistic in Hex", InputGestureText = "Ctrl+H" };
            menu = new ContextMenu { Style = (Style)controller.Window.FindResource("StatisticsContextMenu") };
            menu.Items.Add(copy); menu.Items.Add(paste); menu.Items.Add(new Separator()); menu.Items.Add(setValue);
            menu.Items.Add(new Separator()); menu.Items.Add(showInHex);
            foreach (var item in menu.Items.OfType<MenuItem>()) item.Style = (Style)controller.Window.FindResource("StatisticsMenuItem");
            foreach (var separator in menu.Items.OfType<Separator>()) separator.Style = (Style)controller.Window.FindResource("StatisticsMenuSeparator");
            copy.Click += delegate { Run(Copy); };
            paste.Click += delegate { Run(Paste); };
            setValue.Click += delegate { Run(ShowValueDialog); };
            showInHex.Click += delegate { controller.ShowSelectedStatisticInHex(); };
            menu.Opened += delegate { UpdateMenu(); };
            controller.Grid.ContextMenu = menu;
            controller.Grid.CurrentCellChanged += delegate {
                var current = controller.Grid.CurrentCell;
                if (IsAvailable(current)) lastActiveCell = current;
            };
            controller.Grid.PreviewMouseRightButtonDown += SelectContextCell;
            var button = controller.Find<Button>("StatsActionsButton");
            button.Click += delegate {
                menu.PlacementTarget = button; menu.Placement = PlacementMode.Bottom; menu.IsOpen = true;
            };
            controller.Window.PreviewKeyDown += OnKeyDown;
        }

        private RowViewModel CurrentItem
        {
            get { return ActiveCell.Item as RowViewModel; }
        }

        private bool IsAvailable(DataGridCellInfo cell)
        { return cell.Item is RowViewModel && cell.Column != null && controller.Grid.Items.Contains(cell.Item); }

        public DataGridCellInfo ActiveCell
        {
            get {
                if (IsAvailable(controller.Grid.CurrentCell)) return controller.Grid.CurrentCell;
                // Popup/toolbar focus clears CurrentCell, but leaves the user's selection intact.
                // Never reuse a remembered cell after filtering, switching files or clearing selection.
                if (IsAvailable(lastActiveCell) && controller.Grid.SelectedCells.Contains(lastActiveCell)) return lastActiveCell;
                var selected = Selection;
                return selected.Length == 1 ? selected[0] : new DataGridCellInfo();
            }
        }

        private DataGridCellInfo[] Selection
        {
            get { return controller.Grid.SelectedCells.Where(cell => cell.Item is RowViewModel && controller.Grid.Items.Contains(cell.Item)).ToArray(); }
        }

        private int[] StatisticOffsets()
        {
            return Selection.Where(cell => cell.Column != null && controller.Grid.Columns.IndexOf(cell.Column) >= 2)
                .Select(cell => ((RowViewModel)cell.Item).Record.Offset + (controller.Grid.Columns.IndexOf(cell.Column) - 2) * 4)
                .Distinct().ToArray();
        }

        public void Copy()
        {
            if (controller.Session == null) return;
            var item = CurrentItem;
            if (item == null) throw new InvalidOperationException("Select the source item first.");
            if (!controller.CommitPendingEdit()) return;
            uint[] values = Enumerable.Range(0, 14).Select(index => controller.Session.GetValue(item.Record.Offset + index * 4)).ToArray();
            writeClipboard(StatisticTransfer.Format(values));
            SetStatus("Copied all 14 statistics from " + item.Name + ".");
        }

        public void Paste()
        {
            if (controller.Session == null || !controller.CommitPendingEdit()) return;
            var targets = Selection.Select(cell => (RowViewModel)cell.Item).Distinct().ToArray();
            if (targets.Length == 0) throw new InvalidOperationException("Select one or more destination items first.");
            uint[] values = StatisticTransfer.Parse(readClipboard());
            var batch = new Dictionary<int, uint>();
            foreach (var item in targets)
                for (int index = 0; index < values.Length; index++) batch.Add(item.Record.Offset + index * 4, values[index]);
            Apply(batch, "Pasted all 14 statistics into " + targets.Length + " item(s). Undo reverses the entire paste.");
        }

        public void SetSelectedValue(uint value)
        {
            if (controller.Session == null || !controller.CommitPendingEdit()) return;
            int[] offsets = StatisticOffsets();
            if (offsets.Length == 0) throw new InvalidOperationException("Select statistic cells with Ctrl+click or Shift+click first.");
            Apply(offsets.ToDictionary(offset => offset, offset => value), "Updated " + offsets.Length + " selected statistic(s). Undo reverses the entire change.");
        }

        private void Apply(IDictionary<int, uint> batch, string message)
        {
            controller.Session.SetValues(batch);
            foreach (var row in controller.Rows) row.Refresh();
            controller.UpdateStatus(); SetStatus(message);
        }

        private void ShowValueDialog()
        {
            if (controller.Session == null || !controller.CommitPendingEdit()) return;
            int count = StatisticOffsets().Length;
            if (count == 0) throw new InvalidOperationException("Select statistic cells with Ctrl+click or Shift+click first.");
            uint? value = BulkValueDialog.Read(controller.Window, count, Selection.Select(cell => cell.Item).Distinct().Count());
            if (value.HasValue) SetSelectedValue(value.Value);
        }

        private void SetStatus(string message) { controller.Find<TextBlock>("StatusText").Text = message; }

        private void UpdateMenu()
        {
            bool available = controller.Session != null && controller.Grid.Visibility == Visibility.Visible;
            copy.IsEnabled = available && CurrentItem != null;
            paste.IsEnabled = available && Selection.Length > 0;
            setValue.IsEnabled = available && StatisticOffsets().Length > 0;
            showInHex.IsEnabled = available && CurrentItem != null && ActiveCell.Column != null && controller.Grid.Columns.IndexOf(ActiveCell.Column) >= 2;
            copy.Header = CurrentItem == null ? "Copy all 14 stats" : "Copy all 14 stats from " + CurrentItem.Name;
            paste.Header = "Paste all 14 stats into " + Selection.Select(cell => cell.Item).Distinct().Count() + " selected item(s)";
            setValue.Header = "Set " + StatisticOffsets().Length + " selected statistic(s)...";
            menu.Background = ThemePalette.GetBrush("Surface"); menu.Foreground = ThemePalette.GetBrush("BodyText");
            menu.BorderBrush = ThemePalette.GetBrush("Border");
            foreach (var item in new[] { copy, paste, setValue, showInHex }) {
                item.Background = ThemePalette.GetBrush("Surface"); item.Foreground = ThemePalette.GetBrush("BodyText");
            }
        }

        private void SelectContextCell(object sender, MouseButtonEventArgs args)
        {
            var hit = args.OriginalSource as DependencyObject;
            while (hit != null && hit != controller.Grid)
            {
                var cell = hit as DataGridCell;
                if (cell != null && cell.DataContext is RowViewModel)
                {
                    var info = new DataGridCellInfo(cell.DataContext, cell.Column);
                    if (!controller.Grid.SelectedCells.Contains(info)) {
                        if (!controller.CommitPendingEdit()) { args.Handled = true; return; }
                        controller.Grid.SelectedCells.Clear(); controller.Grid.SelectedCells.Add(info);
                    }
                    controller.Grid.CurrentCell = info;
                    return;
                }
                hit = hit is Visual ? VisualTreeHelper.GetParent(hit) : LogicalTreeHelper.GetParent(hit);
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs args)
        {
            if (controller.Grid.Visibility != Visibility.Visible || !controller.Grid.IsKeyboardFocusWithin || Keyboard.FocusedElement is TextBox) return;
            Action action = null;
            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) {
                if (args.Key == Key.C) action = Copy;
                else if (args.Key == Key.V) action = Paste;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && args.Key == Key.Enter) action = ShowValueDialog;
            if (action == null) return;
            args.Handled = true; Run(action);
        }

        private void Run(Action action)
        {
            try { action(); }
            catch (Exception error) {
                string message = error is System.Runtime.InteropServices.ExternalException ? "The clipboard is busy. Please try again." : error.Message;
                EnglishDialog.Show(controller.Window, message, "Statistics action", MessageBoxButton.OK);
            }
        }
    }
}
