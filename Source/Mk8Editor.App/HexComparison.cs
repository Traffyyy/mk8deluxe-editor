using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mk8Editor.App
{
    /// <summary>Links the two views of one editing session, including both scroll axes.</summary>
    public sealed class HexComparison
    {
        public HexView Original { get; private set; }
        public HexView Edited { get; private set; }
        private ScrollViewer originalScroll;
        private ScrollViewer editedScroll;
        private bool synchronizing;

        public HexComparison(HexView original, HexView edited)
        {
            Original = original; Edited = edited;
            original.Grid.LayoutUpdated += AttachAfterLayout;
            edited.Grid.LayoutUpdated += AttachAfterLayout;
        }

        private void AttachAfterLayout(object sender, EventArgs args)
        {
            if (originalScroll == null) originalScroll = FindScrollViewer(Original.Grid);
            if (editedScroll == null) editedScroll = FindScrollViewer(Edited.Grid);
            if (originalScroll == null || editedScroll == null) return;
            Original.Grid.LayoutUpdated -= AttachAfterLayout;
            Edited.Grid.LayoutUpdated -= AttachAfterLayout;
            originalScroll.ScrollChanged += delegate(object source, ScrollChangedEventArgs change) { Synchronize(originalScroll, editedScroll, change); };
            editedScroll.ScrollChanged += delegate(object source, ScrollChangedEventArgs change) { Synchronize(editedScroll, originalScroll, change); };
        }

        private void Synchronize(ScrollViewer source, ScrollViewer target, ScrollChangedEventArgs change)
        {
            if (synchronizing || (change.VerticalChange == 0 && change.HorizontalChange == 0)) return;
            synchronizing = true;
            try {
                if (Math.Abs(target.VerticalOffset - source.VerticalOffset) > 0.1) target.ScrollToVerticalOffset(source.VerticalOffset);
                if (Math.Abs(target.HorizontalOffset - source.HorizontalOffset) > 0.5) target.ScrollToHorizontalOffset(source.HorizontalOffset);
            }
            finally { synchronizing = false; }
        }

        public void Refresh() { Original.Refresh(); Edited.Refresh(); }
        public void Filter(string query) { Original.Filter(query); Edited.Filter(query); }

        /// <summary>Selects the four bytes of a statistic in both panes and brings them into view.</summary>
        public void ShowStatistic(int offset)
        {
            if (offset < 0 || offset / 16 >= Edited.Rows.Count || offset % 4 != 0)
                throw new ArgumentOutOfRangeException("offset");
            Filter("");
            foreach (var view in new[] { Original, Edited })
            {
                var row = view.Rows[offset / 16];
                int firstColumn = offset % 16 + 1;
                view.Grid.CurrentCell = new DataGridCellInfo(row, view.Grid.Columns[firstColumn]);
                view.Grid.SelectedCells.Clear();
                for (int index = 0; index < 4; index++)
                    view.Grid.SelectedCells.Add(new DataGridCellInfo(row, view.Grid.Columns[firstColumn + index]));
                view.Grid.UpdateLayout();
                var scroll = FindScrollViewer(view.Grid);
                if (scroll == null) continue;
                scroll.ScrollToVerticalOffset(Math.Max(0, offset / 16 - 3));
                // Keep all four selected bytes visible to the right of the frozen address column.
                double fieldEnd = 0;
                for (int index = 1; index < firstColumn + 4; index++) fieldEnd += view.Grid.Columns[index].ActualWidth;
                double available = scroll.ViewportWidth - view.Grid.Columns[0].ActualWidth;
                scroll.ScrollToHorizontalOffset(Math.Max(0, fieldEnd - available + 12));
            }
            Edited.Grid.UpdateLayout(); Original.Grid.UpdateLayout();
            Edited.Grid.Focus();
        }

        private static ScrollViewer FindScrollViewer(DependencyObject parent)
        {
            if (parent is ScrollViewer) return (ScrollViewer)parent;
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++) {
                var found = FindScrollViewer(VisualTreeHelper.GetChild(parent, index));
                if (found != null) return found;
            }
            return null;
        }
    }
}
