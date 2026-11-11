using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

namespace Mk8Editor.App
{
    /// <summary>Explicitly applies one validated value to the selected statistic cells.</summary>
    public static class BulkValueDialog
    {
        public static Window Create(Window owner, int statisticCount, int itemCount, Action<uint> apply)
        {
            var dialog = new Window {
                Title = "Set selected statistics", Owner = owner.IsVisible ? owner : null, Width = 440, SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 14,
                Background = ThemePalette.GetBrush("Surface"), Foreground = ThemePalette.GetBrush("BodyText")
            };
            var panel = new StackPanel { Margin = new Thickness(24) };
            panel.Children.Add(new TextBlock { Text = "Set " + statisticCount + " selected statistics across " + itemCount + " item(s) to the same value.", TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(new TextBlock { Text = "New value", Margin = new Thickness(0, 18, 0, 6) });
            var input = new TextBox { Name = "BulkValueInput", Text = "", Padding = new Thickness(8), Background = ThemePalette.GetBrush("Surface"), Foreground = ThemePalette.GetBrush("BodyText"), CaretBrush = ThemePalette.GetBrush("BodyText"), BorderBrush = ThemePalette.GetBrush("InputBorder") };
            AutomationProperties.SetName(input, "New value for selected statistics");
            panel.Children.Add(input);
            var hint = new TextBlock { Text = "Enter a whole number from 0 to 4294967295.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 18), Foreground = ThemePalette.GetBrush("SecondaryText") };
            panel.Children.Add(hint);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0) };
            var confirm = new Button { Name = "ApplyBulkValue", Content = "Apply", IsDefault = true, IsEnabled = false, Padding = new Thickness(14, 8, 14, 8) };
            foreach (var button in new[] { cancel, confirm }) {
                button.Style = owner.TryFindResource(typeof(Button)) as Style;
                button.Background = ThemePalette.GetBrush("Surface"); button.Foreground = ThemePalette.GetBrush("BodyText");
                button.BorderBrush = ThemePalette.GetBrush("Border"); actions.Children.Add(button);
            }
            input.TextChanged += delegate { uint value; confirm.IsEnabled = StatCell.TryParse(input.Text, out value); };
            confirm.Click += delegate { uint value; if (StatCell.TryParse(input.Text, out value)) { apply(value); dialog.Close(); } };
            cancel.Click += delegate { dialog.Close(); };
            dialog.Loaded += delegate { input.Focus(); };
            panel.Children.Add(actions); dialog.Content = panel;
            return dialog;
        }

        public static uint? Read(Window owner, int statisticCount, int itemCount)
        {
            uint? value = null;
            Create(owner, statisticCount, itemCount, result => value = result).ShowDialog();
            return value;
        }
    }
}
