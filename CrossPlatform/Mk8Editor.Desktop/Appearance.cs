using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Controls.Shapes;

namespace Mk8Editor.Desktop;

internal static class Appearance
{
    private static readonly Dictionary<string, Bitmap> Images = [];
    public static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
    public static IBrush Surface(bool dark) => Brush(dark ? "#151B24" : "#F3F5F8");
    public static IBrush Text(bool dark) => Brush(dark ? "#E8EDF5" : "#192D47");
    public static IBrush Neutral(bool dark) => Brush(dark ? "#202A38" : "#FFFFFF");
    public static IBrush Border(bool dark) => Brush(dark ? "#3E4D62" : "#D1D9E5");
    public static IBrush Bookmark(bool dark, int index) => Brush(dark
        ? (index % 2 == 0 ? "#276875" : "#854B69")
        : (index % 2 == 0 ? "#AFCFF2" : "#D9B7EC"));
    public static Bitmap Image(string name)
    {
        if (Images.TryGetValue(name, out var bitmap)) return bitmap;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Mk8Editor.Assets." + name)
            ?? throw new InvalidDataException("Missing item image: " + name);
        return Images[name] = new Bitmap(stream);
    }
    public static Control Icon(string data, IBrush? color = null) => new Viewbox
    {
        Width = 22, Height = 22,
        Child = new Canvas { Width = 24, Height = 24, Children = {
            new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(data), Stroke = color ?? Text(false), StrokeThickness = 1.8 }
        }}
    };
    public const string Open = "M3,7 L9,7 11,9 21,9 18,20 3,20 Z M3,17 L3,4 9,4 11,6 20,6 20,9";
    public const string Save = "M4,3 L18,3 21,6 21,21 3,21 3,3 Z M7,3 L7,10 17,10 17,3 M7,21 L7,14 17,14 17,21 M14,5 L14,8";
    public const string Undo = "M8,5 L3,10 8,15 M3,10 L15,10 C23,10 23,20 15,20";
    public const string Reset = "M4,9 A9,9 0 1 1 4,17 M4,3 L4,9 10,9";
    public const string Speaker = "M3,9 L7,9 12,4 12,20 7,15 3,15 Z M16,8 Q21,12 16,16 M19,4 Q28,12 19,20";
    public const string Moon = "M19,17 A9,9 0 0 1 9,3 A8,8 0 0 0 19,17 Z";
    public const string Sun = "M12,7 A5,5 0 1 0 12,17 A5,5 0 1 0 12,7 M12,1 L12,4 M12,20 L12,23 M1,12 L4,12 M20,12 L23,12 M4,4 L6,6 M18,18 L20,20 M4,20 L6,18 M18,6 L20,4";
}
