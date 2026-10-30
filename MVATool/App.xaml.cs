using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MVATool.Core;
using MVATool.Core.Pipeline;
using MVATool.Services;
using MVATool.ViewModels;
using MVATool.Views;

namespace MVATool;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ===== COMPOSITION ROOT =====
        // Nơi DUY NHẤT trong app biết cách "lắp ráp" mọi thứ, dùng DI container
        // thật (Microsoft.Extensions.DependencyInjection).
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
        MainWindow = mainWindow;
        mainWindow.Show();

        // Bật timer đo tài nguyên SAU khi đã có UI thread — unit test
        // tạo ViewModel này sẽ không bao giờ đi qua đây nên không cần UI.
        viewModel.SystemStatus.Start();

        // Mở ảnh / thư mục / job truyền qua dòng lệnh (tiện gắn file association,
        // kéo file thả vào icon exe, hoặc mở sẵn một ca kiểm thử).
        _ = OpenFromCommandLineAsync(viewModel, e.Args); // lỗi đã được xử lý bên trong
    }

    /// <summary>
    /// <c>MVATool.exe [job.mvajob] [ảnh | thư mục]</c> — job được nạp TRƯỚC,
    /// để ảnh mở ra là chạy luôn job đó.
    /// </summary>
    private static async Task OpenFromCommandLineAsync(MainWindowViewModel viewModel, string[] args)
    {
        static bool IsJob(string path) => path.EndsWith(JobSerializer.FileExtension, StringComparison.OrdinalIgnoreCase);

        foreach (var job in args.Where(a => IsJob(a) && File.Exists(a)))
            await viewModel.LoadJobAsync(job);

        foreach (var path in args.Where(a => !IsJob(a)))
        {
            if (Directory.Exists(path))
                viewModel.LoadFolder(path);
            else if (File.Exists(path))
                await viewModel.LoadImageAsync(path);
        }
    }
}
