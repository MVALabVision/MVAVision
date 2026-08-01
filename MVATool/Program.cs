using Avalonia;

namespace MVATool;

internal static class Program
{
    // Điểm khởi động của ứng dụng. Không viết logic ở đây —
    // mọi thứ bắt đầu từ App.axaml.cs (composition root).
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
