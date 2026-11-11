using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Mk8Editor.Desktop;

public sealed class EditorApp : Application
{
    public EditorApp() => Name = "mk8deluxe editor";

    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Light;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;
            if (Program.Arguments.Length == 1)
                window.Opened += async (_, _) => await window.LoadPathAsync(Program.Arguments[0]);
        }
        base.OnFrameworkInitializationCompleted();
    }
}

