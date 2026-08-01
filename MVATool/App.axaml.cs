using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MVATool.Core;
using MVATool.Services;
using MVATool.ViewModels;
using MVATool.Views;

namespace MVATool;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // ===== COMPOSITION ROOT =====
            // Nơi DUY NHẤT trong app biết cách "lắp ráp" mọi thứ, dùng DI container
            // thật (Microsoft.Extensions.DependencyInjection — VisionPlatform cũng dùng).
            //
            // ViewModel chỉ khai báo NHỮNG GÌ nó cần qua constructor (các interface);
            // container lo phần TẠO RA và TIÊM VÀO. Đổi implementation = đổi 1 dòng ở đây.
            var mainWindow = new MainWindow();

            var services = new ServiceCollection();
            services.AddSingleton<IImageService, ImageService>();
            services.AddSingleton<IProcessorCatalog, ProcessorCatalog>();
            // FilePickerService cần biết Window chủ để mở dialog → đăng ký instance tạo sẵn.
            services.AddSingleton<IFilePickerService>(new FilePickerService(mainWindow));
            // Khối giám sát CPU / RAM / ổ đĩa trên giao diện.
            services.AddSingleton<ISystemInfoService, SystemInfoService>();
            services.AddSingleton<SystemStatusViewModel>();
            services.AddSingleton<MainWindowViewModel>();

            var provider = services.BuildServiceProvider();

            var viewModel = provider.GetRequiredService<MainWindowViewModel>();
            mainWindow.DataContext = viewModel;
            desktop.MainWindow = mainWindow;

            // Bật timer đo tài nguyên SAU khi đã có UI thread — unit test
            // tạo ViewModel này sẽ không bao giờ đi qua đây nên không cần UI.
            viewModel.SystemStatus.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
