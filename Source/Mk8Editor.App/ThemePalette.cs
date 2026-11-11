using System;
using System.Windows;
using System.Windows.Media;

namespace Mk8Editor.App
{
    /// <summary>One semantic palette shared by XAML, table cells and dialogs.</summary>
    public static class ThemePalette
    {
        public static bool IsDark { get; private set; }
        private static readonly string[,] Colors = {
            { "Canvas", "#F4F5F7", "#141922" },
            { "Surface", "#FFFFFF", "#202733" },
            { "BodyText", "#202B3B", "#E8EDF5" },
            { "ControlText", "#27374A", "#E8EDF5" },
            { "Border", "#D3DAE2", "#445168" },
            { "ScrollGrip", "#718096", "#91A0B8" },
            { "ScrollGripHover", "#465A76", "#C0CDE0" },
            { "Accent", "#1765D1", "#1765D1" },
            { "Focus", "#1765D1", "#78B4FF" },
            { "HeaderText", "#394A60", "#D2DCEB" },
            { "TabText", "#145CC0", "#B9D9FF" },
            { "TabSelected", "#EAF1FC", "#243C5C" },
            { "TabHover", "#EDF1F7", "#2A3547" },
            { "Header", "#E9EDF2", "#2A3444" },
            { "GridBorder", "#D6DDE5", "#414E63" },
            { "Selection", "#DBE9FD", "#304D70" },
            { "SelectionText", "#172D4C", "#EDF5FF" },
            { "HexBookmarkEven", "#AFCFF2", "#276875" },
            { "HexBookmarkOdd", "#D9B7EC", "#854B69" },
            { "DiffOutline", "#974A00", "#FFD166" },
            { "Invalid", "#FBE7E7", "#653039" },
            { "Divider", "#DCE2E9", "#344258" },
            { "TitleText", "#192B43", "#F2F6FC" },
            { "SecondaryText", "#52647A", "#B6C3D6" },
            { "InputBorder", "#CCD5E0", "#4D5E78" },
            { "GridLine", "#E0E5EB", "#354155" },
            { "Empty", "#F9FAFC", "#1B2330" },
            { "StatusText", "#42566E", "#C2CEE0" },
            { "Increase", "#DCEEDF", "#254B3A" },
            { "Decrease", "#F5DFDC", "#5D3438" },
            { "IncreaseBorder", "#B2CDB8", "#638F73" },
            { "DecreaseBorder", "#D8B9B4", "#A26C72" },
            { "NumberText", "#66768B", "#ADBBD0" },
            { "Identity", "#F7F8FA", "#1D2633" },
            { "NameText", "#1E426F", "#B8D8FF" },
            { "Statistic", "#FFFFFF", "#202A38" }
        };
        public static void Apply(bool dark)
        {
            IsDark = dark;
            for (int index = 0; index < Colors.GetLength(0); index++)
            {
                var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(Colors[index, dark ? 2 : 1]);
                brush.Freeze();
                Application.Current.Resources[Colors[index, 0]] = brush;
            }
        }
        public static Brush GetBrush(string key) { return (Brush)Application.Current.Resources[key]; }
    }
}
