using System.Globalization;
using Avalonia;
using Avalonia.Headless;

namespace Mk8Editor.Desktop;

internal static class Program
{
    public static string[] Arguments { get; private set; } = [];

    [STAThread]
    public static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        Arguments = args;
        if (args.Length > 0 && args[0] == "--verify")
        {
            try
            {
                BuildApp().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
                return UiChecks.Run(args.Skip(1).ToArray());
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error);
                if (args.Length >= 3)
                {
                    Directory.CreateDirectory(args[2]);
                    File.WriteAllText(Path.Combine(args[2], "failure.txt"), error.ToString());
                }
                return 1;
            }
        }
        return BuildApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildApp() => AppBuilder.Configure<EditorApp>()
        .UsePlatformDetect().WithInterFont().LogToTrace();
}
