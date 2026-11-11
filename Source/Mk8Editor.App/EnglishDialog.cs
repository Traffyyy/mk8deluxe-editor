using System.Windows;
using System.Windows.Controls;

namespace Mk8Editor.App
{
    /// <summary>App-owned, themed dialogs keep confirmation buttons in English on any Windows language.</summary>
    public static class EnglishDialog
    {
        public static MessageBoxResult Show(Window owner, string message, string title, MessageBoxButton buttons)
        {
            MessageBoxResult result = MessageBoxResult.Cancel;
            var dialog = new Window {
                Title = title, Owner = owner, Width = 470, SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize, WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 14,
                Background = ThemePalette.GetBrush("Surface"), Foreground = ThemePalette.GetBrush("BodyText"), ShowInTaskbar = false
            };
            var panel = new StackPanel { Margin = new Thickness(24) };
            panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 24) });
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            string[] labels = buttons == MessageBoxButton.YesNoCancel ? new[] { "Yes", "No", "Cancel" } :
                buttons == MessageBoxButton.YesNo ? new[] { "Yes", "No" } : new[] { "OK" };
            foreach (string label in labels)
            {
                string choice = label;
                var button = new Button {
                    Content = label, MinWidth = 76, Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(8, 0, 0, 0),
                    Background = ThemePalette.GetBrush("Surface"), Foreground = ThemePalette.GetBrush("BodyText"),
                    BorderBrush = ThemePalette.GetBrush("Border"), IsDefault = label == "Yes" || label == "OK", IsCancel = label == "Cancel"
                };
                if (owner != null) button.Style = owner.TryFindResource(typeof(Button)) as Style;
                button.Click += delegate {
                    result = choice == "Yes" ? MessageBoxResult.Yes : choice == "No" ? MessageBoxResult.No : choice == "OK" ? MessageBoxResult.OK : MessageBoxResult.Cancel;
                    dialog.Close();
                };
                actions.Children.Add(button);
            }
            panel.Children.Add(actions); dialog.Content = panel; dialog.ShowDialog(); return result;
        }
    }
}
