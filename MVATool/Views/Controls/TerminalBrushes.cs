using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using OpenCvSharp;

namespace MVATool.Views.Controls;

/// <summary>
/// Màu theo KIỂU DỮ LIỆU — chấm O và dây cùng màu, nhìn là biết nối được với nhau không:
/// Ảnh = xanh dương, Số = xanh lá, Đúng/Sai = cam, Chữ = hồng, Contours = tím.
/// </summary>
public static class TerminalBrushes
{
    private static readonly Brush Image = Freeze(Color.FromRgb(0x4F, 0xA3, 0xFF));
    private static readonly Brush Number = Freeze(Color.FromRgb(0x3D, 0xDC, 0x97));
    private static readonly Brush Bool = Freeze(Color.FromRgb(0xFF, 0xB5, 0x47));
    private static readonly Brush Text = Freeze(Color.FromRgb(0xF4, 0x72, 0xB6));
    private static readonly Brush Contours = Freeze(Color.FromRgb(0xA7, 0x8B, 0xFA));
    private static readonly Brush Other = Freeze(Color.FromRgb(0x9C, 0xA3, 0xAF));

    public static Brush For(Type? type) =>
        type == typeof(Mat) ? Image
        : type == typeof(double) ? Number
        : type == typeof(bool) ? Bool
        : type == typeof(string) ? Text
        : type == typeof(Point[][]) ? Contours
        : Other;

    private static Brush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}

/// <summary>Type → Brush, cho XAML: Fill="{Binding DataType, Converter={StaticResource TypeToBrush}}".</summary>
public class TypeToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        TerminalBrushes.For(value as Type);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
