using System;
using System.IO;
using System.Windows;

namespace Mk8Editor.App
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
                System.Threading.Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
                var application = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                ThemePalette.Apply(false);
                if (args.Length == 3 && args[0] == "--verify") return UiVerification.Run(args[1], args[2]);
                var controller = new WindowController();
                if (args.Length == 1) controller.LoadFile(args[0]);
                application.Run(controller.Window);
                return 0;
            }
            catch (Exception error)
            {
                if (args.Length > 0 && args[0] == "--verify")
                {
                    if (args.Length == 3) { Directory.CreateDirectory(args[2]); File.WriteAllText(Path.Combine(args[2], "failure.txt"), error.ToString()); }
                }
                else EnglishDialog.Show(null, UserMessages.For(error), "mk8deluxe editor", MessageBoxButton.OK);
                return 1;
            }
        }
    }
}
