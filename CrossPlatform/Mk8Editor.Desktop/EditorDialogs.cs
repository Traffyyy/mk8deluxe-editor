using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Media;
using System.Globalization;

namespace Mk8Editor.Desktop;

internal static class EditorDialogs
{
    public static async Task<bool> Confirm(Window owner, string title, string message, string action)
    {
        var dialog = Create(owner, title);
        var cancel = new Button { Content = "Cancel" };
        var accept = new Button { Content = action };
        cancel.Click += (_, _) => dialog.Close(false);
        accept.Click += (_, _) => dialog.Close(true);
        dialog.Content = Content(message, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, accept } });
        return await dialog.ShowDialog<bool>(owner);
    }

    public static async Task Message(Window owner, string title, string message)
    {
        var dialog = Create(owner, title);
        var close = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => dialog.Close();
        dialog.Content = Content(message, close);
        await dialog.ShowDialog(owner);
    }

    public static async Task<uint?> Value(Window owner, int count, uint initial)
    {
        var dialog = Create(owner, count == 1 ? "Edit statistic" : $"Edit {count} statistics");
        var input = new TextBox { Text = initial.ToString(CultureInfo.InvariantCulture), PlaceholderText = "0–4294967295" };
        var error = new TextBlock { Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap };
        var cancel = new Button { Content = "Cancel" };
        var apply = new Button { Content = "Apply" };
        void Submit()
        {
            if (uint.TryParse(input.Text, NumberStyles.None, CultureInfo.InvariantCulture, out uint value)) dialog.Close((uint?)value);
            else error.Text = "Enter a whole number from 0 to 4294967295.";
        }
        cancel.Click += (_, _) => dialog.Close((uint?)null);
        apply.Click += (_, _) => Submit();
        input.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Submit(); e.Handled = true; } };
        dialog.Opened += (_, _) => { input.Focus(); input.SelectAll(); };
        dialog.Content = Content("The value applies to every selected statistic. Undo restores the entire edit.",
            input, error, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10,
                HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, apply } });
        return await dialog.ShowDialog<uint?>(owner);
    }

    private static Window Create(Window owner, string title)
    {
        var dialog = new Window { Title = title, Width = 440, SizeToContent = SizeToContent.Height,
            CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = owner.Background, Foreground = owner.Foreground, RequestedThemeVariant = owner.ActualThemeVariant };
        dialog.KeyDown += (_, e) => { if (e.Key == Key.Escape) dialog.Close(); };
        return dialog;
    }
    private static StackPanel Content(string message, params Control[] controls)
    {
        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        foreach (var control in controls) panel.Children.Add(control);
        return panel;
    }
}

