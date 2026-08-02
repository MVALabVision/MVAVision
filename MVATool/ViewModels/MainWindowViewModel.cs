using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MVATool.Core;
using MVATool.Models;
using MVATool.Services;
using OpenCvSharp;

namespace MVATool.ViewModels;

/// <summary>
/// VIEWMODEL chính — trung tâm của MVVM.
/// - Giữ TRẠNG THÁI mà View hiển thị (ảnh, pipeline, thông báo...).
/// - Cung cấp COMMAND để View gọi khi người dùng thao tác.
/// - KHÔNG tham chiếu tới bất kỳ control UI nào — mọi giao tiếp với View
///   đều qua data binding hoặc service interface.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IFilePickerService _filePicker;
    private readonly IImageService _imageService;

    // Dữ liệu ảnh dạng OpenCV Mat — chỉ ViewModel biết, View không bao giờ thấy Mat.
    private Mat? _pristineMat; // ảnh Y NGUYÊN như lúc mở file — để "Khôi phục"
    private Mat? _originalMat; // ảnh gốc hiện tại (có thể đã cắt / xoay / lật)
    private Mat? _resultMat;   // ảnh sau khi chạy pipeline
    private string _fileName = "";

    // Cờ "chạy xong thì chạy lại": khi người dùng kéo slider lúc pipeline đang
    // chạy, ta không chạy chồng lên mà ghi nhớ để chạy lại ngay khi xong.
    private bool _rerunRequested;

    public MainWindowViewModel(
        IFilePickerService filePicker,
        IImageService imageService,
        IProcessorCatalog catalog,
        SystemStatusViewModel systemStatus)
    {
        _filePicker = filePicker;
        _imageService = imageService;
        SystemStatus = systemStatus;
        AvailableProcessors = catalog.Available;
        SelectedProcessorInfo = AvailableProcessors.FirstOrDefault();
    }

    /// <summary>
    /// ViewModel con: khối giám sát CPU / RAM / ổ đĩa / đồng hồ.
    /// View bind lồng cấp: {Binding SystemStatus.CpuText}.
    /// </summary>
    public SystemStatusViewModel SystemStatus { get; }

    // ===== Trạng thái để View bind =====

    [ObservableProperty]
    private BitmapSource? _originalImage;

    [ObservableProperty]
    private BitmapSource? _resultImage;

    [ObservableProperty]
    private string _statusMessage = "Mở ảnh (Ctrl+O) hoặc kéo thả file ảnh vào cửa sổ để bắt đầu.";

    /// <summary>Tên file + kích thước, hiện góc phải thanh trạng thái.</summary>
    [ObservableProperty]
    private string _imageInfo = "";

    /// <summary>Tọa độ + màu của pixel dưới con trỏ (ImageViewer báo về qua command).</summary>
    [ObservableProperty]
    private string _pixelInfo = "";

    /// <summary>Bật chế độ cắt ảnh: kéo chuột trên ảnh gốc để chọn vùng.</summary>
    [ObservableProperty]
    private bool _isCropMode;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddStepCommand))]
    private ProcessorInfo? _selectedProcessorInfo;

    /// <summary>
    /// Đang xử lý? Điều khiển ProgressBar và khóa các nút.
    /// [NotifyCanExecuteChangedFor]: IsBusy đổi → các command này tự cập nhật enable/disable.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunPipelineCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveResultCommand))]
    [NotifyPropertyChangedFor(nameof(SystemStateText))]
    private bool _isBusy;

    /// <summary>
    /// Nhãn cạnh đèn LED trạng thái trên header — kiểu máy công nghiệp:
    /// SẴN SÀNG (xanh) / ĐANG XỬ LÝ (vàng) / BATCH (xanh dương nhấp nháy).
    /// Property TÍNH TOÁN: tự thông báo đổi nhờ [NotifyPropertyChangedFor]
    /// gắn trên IsBusy và IsBatchRunning.
    /// </summary>
    public string SystemStateText => IsBatchRunning ? "BATCH" : IsBusy ? "ĐANG XỬ LÝ" : "SẴN SÀNG";

    /// <summary>Bật thì mọi thay đổi (thêm bước, kéo slider, cắt ảnh...) tự chạy lại pipeline.</summary>
    [ObservableProperty]
    private bool _isAutoRunEnabled = true;

    public IReadOnlyList<ProcessorInfo> AvailableProcessors { get; }

    public ObservableCollection<PipelineStepViewModel> Pipeline { get; } = new();

    public ObservableCollection<LogEntry> History { get; } = new();

    // ===== Trạng thái chế độ thư mục (chạy hàng loạt) =====

    /// <summary>Danh sách ảnh trong thư mục đã mở.</summary>
    public ObservableCollection<ImageFileItem> ImageFiles { get; } = new();

    /// <summary>Ảnh đang chọn trong danh sách — chọn là load ngay (xem hook OnSelectedImageFileChanged).</summary>
    [ObservableProperty]
    private ImageFileItem? _selectedImageFile;

    /// <summary>"3/25" — vị trí ảnh hiện tại trong thư mục.</summary>
    [ObservableProperty]
    private string _folderPositionText = "";

    /// <summary>Đang chạy hàng loạt cả thư mục? Khóa gần hết các nút trong lúc chạy.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SystemStateText))]
    private bool _isBatchRunning;

    /// <summary>Bật thì khi chạy cả thư mục, kết quả từng ảnh được lưu vào thư mục con "ket-qua".</summary>
    [ObservableProperty]
    private bool _isBatchSaveEnabled;

    private bool HasImage => _originalMat is not null;

    // ===== Mở / lưu ảnh =====

    private bool CanOpenImage() => !IsBusy && !IsBatchRunning;

    [RelayCommand(CanExecute = nameof(CanOpenImage))]
    private async Task OpenImageAsync()
    {
        var path = await _filePicker.PickImageFileAsync();
        if (path is not null)
        {
            SelectedImageFile = null; // ảnh đơn lẻ không thuộc danh sách thư mục
            await LoadImageAsync(path);
        }
    }

    /// <summary>Public để code-behind gọi khi người dùng kéo thả file vào cửa sổ.</summary>
    public async Task LoadImageAsync(string path)
    {
        try
        {
            var loaded = _imageService.Load(path);

            _originalMat?.Dispose();
            _pristineMat?.Dispose();
            _originalMat = loaded;
            _pristineMat = loaded.Clone(); // bản dự phòng cho nút "Khôi phục"
            _fileName = Path.GetFileName(path);
            IsCropMode = false;

            OriginalImage = _imageService.ToBitmap(loaded);
            ImageInfo = $"{_fileName} — {loaded.Width} × {loaded.Height} px";
            StatusMessage = $"Đã mở {_fileName}. Lăn chuột để zoom, kéo chuột để di chuyển ảnh.";
            NotifyImageDependentCommands();

            // Khi chạy hàng loạt, vòng lặp batch tự gọi RunPipelineAsync — không auto-run ở đây.
            if (IsAutoRunEnabled && !IsBatchRunning && Pipeline.Count > 0)
                await RunPipelineAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi: {ex.Message}";
        }
    }

    private bool CanSaveResult() => _resultMat is not null && !IsBusy && !IsBatchRunning;

    [RelayCommand(CanExecute = nameof(CanSaveResult))]
    private async Task SaveResultAsync()
    {
        if (_resultMat is null)
            return;

        var path = await _filePicker.PickSaveFileAsync("ket-qua.png");
        if (path is null)
            return;

        try
        {
            _imageService.Save(_resultMat, path);
            StatusMessage = $"Đã lưu: {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi khi lưu: {ex.Message}";
        }
    }

    // ===== Chỉnh sửa ảnh gốc: cắt / xoay / lật / khôi phục =====
    // Tất cả đi qua MutateOriginal để dùng chung phần "thay ảnh + cập nhật UI + auto-run".

    /// <summary>
    /// ImageViewer gọi command này (qua binding CropCommand) khi người dùng
    /// kéo chọn xong vùng cắt — tham số là vùng chọn tính theo PIXEL ẢNH,
    /// dùng kiểu Rect của OpenCvSharp để ViewModel không dính dáng UI framework.
    /// </summary>
    [RelayCommand]
    private void ApplyCrop(Rect rect)
    {
        if (_originalMat is null || IsBatchRunning)
            return;

        // Kẹp vùng cắt vào trong ảnh cho chắc (control đã kẹp rồi, nhưng
        // ViewModel không nên tin dữ liệu từ ngoài một cách mù quáng).
        var x = Math.Clamp(rect.X, 0, _originalMat.Width - 1);
        var y = Math.Clamp(rect.Y, 0, _originalMat.Height - 1);
        var w = Math.Clamp(rect.Width, 1, _originalMat.Width - x);
        var h = Math.Clamp(rect.Height, 1, _originalMat.Height - y);
        if (w < 2 || h < 2)
            return;

        // new Mat(m, roi) chỉ là "cửa sổ nhìn" vào ảnh cũ → phải Clone
        // ra ảnh độc lập trước khi dispose ảnh cũ.
        MutateOriginal(m => new Mat(m, new Rect(x, y, w, h)).Clone(), $"Cắt ảnh {w} × {h}");
        IsCropMode = false;
    }

    [RelayCommand(CanExecute = nameof(CanEditImage))]
    private void RotateLeft() =>
        MutateOriginal(m => Rotated(m, RotateFlags.Rotate90Counterclockwise), "Xoay trái 90°");

    [RelayCommand(CanExecute = nameof(CanEditImage))]
    private void RotateRight() =>
        MutateOriginal(m => Rotated(m, RotateFlags.Rotate90Clockwise), "Xoay phải 90°");

    [RelayCommand(CanExecute = nameof(CanEditImage))]
    private void FlipHorizontal() =>
        MutateOriginal(m => Flipped(m, FlipMode.Y), "Lật ngang");

    [RelayCommand(CanExecute = nameof(CanEditImage))]
    private void FlipVertical() =>
        MutateOriginal(m => Flipped(m, FlipMode.X), "Lật dọc");

    [RelayCommand(CanExecute = nameof(CanEditImage))]
    private void RestoreOriginal()
    {
        if (_pristineMat is null)
            return;
        MutateOriginal(_ => _pristineMat.Clone(), "Khôi phục ảnh như lúc mở file");
    }

    private bool CanEditImage() => HasImage && !IsBatchRunning;

    /// <summary>Khung dùng chung: biến đổi ảnh gốc rồi cập nhật UI + lịch sử + auto-run.</summary>
    private void MutateOriginal(Func<Mat, Mat> transform, string operation)
    {
        if (_originalMat is null)
            return;

        var stopwatch = Stopwatch.StartNew();
        var result = transform(_originalMat);
        stopwatch.Stop();

        _originalMat.Dispose();
        _originalMat = result;

        OriginalImage = _imageService.ToBitmap(result);
        ImageInfo = $"{_fileName} — {result.Width} × {result.Height} px";
        StatusMessage = $"{operation}.";
        History.Insert(0, new LogEntry
        {
            Operation = operation,
            DurationMs = stopwatch.Elapsed.TotalMilliseconds,
        });
        TriggerAutoRun();
    }

    private static Mat Rotated(Mat source, RotateFlags flags)
    {
        var output = new Mat();
        Cv2.Rotate(source, output, flags);
        return output;
    }

    private static Mat Flipped(Mat source, FlipMode mode)
    {
        var output = new Mat();
        Cv2.Flip(source, output, mode);
        return output;
    }

    // ===== Đọc pixel dưới con trỏ =====
    // ImageViewer chỉ biết TỌA ĐỘ pixel; còn GIÁ TRỊ màu nằm trong Mat mà chỉ
    // ViewModel giữ → viewer báo tọa độ về đây, ViewModel tra màu rồi phơi
    // chuỗi PixelInfo cho View bind. Đúng phân vai: View đo, ViewModel tra cứu.

    [RelayCommand]
    private void HoverOriginalPixel(Point? point) => UpdatePixelInfo("Gốc", _originalMat, point);

    [RelayCommand]
    private void HoverResultPixel(Point? point) => UpdatePixelInfo("Kết quả", _resultMat, point);

    private void UpdatePixelInfo(string label, Mat? mat, Point? point)
    {
        if (mat is null || point is null)
        {
            PixelInfo = "";
            return;
        }

        var (x, y) = (point.Value.X, point.Value.Y);
        if (x < 0 || y < 0 || x >= mat.Width || y >= mat.Height)
        {
            PixelInfo = "";
            return;
        }

        PixelInfo = mat.Channels() switch
        {
            // Chú ý thứ tự kênh của OpenCV là B-G-R, không phải R-G-B!
            1 => $"{label} ({x}, {y})  xám={mat.At<byte>(y, x)}",
            3 => Format(mat.At<Vec3b>(y, x)),
            4 => Format(new Vec3b(mat.At<Vec4b>(y, x).Item0, mat.At<Vec4b>(y, x).Item1, mat.At<Vec4b>(y, x).Item2)),
            _ => $"{label} ({x}, {y})",
        };

        string Format(Vec3b c) => $"{label} ({x}, {y})  R={c.Item2} G={c.Item1} B={c.Item0}";
    }

    // ===== Quản lý pipeline =====
    // Các command dưới đây nhận THAM SỐ (CommandParameter từ XAML) — chú ý cách
    // nút ↑ ↓ ✕ trong DataTemplate truyền chính step của nó vào command.

    private bool CanAddStep() => SelectedProcessorInfo is not null;

    [RelayCommand(CanExecute = nameof(CanAddStep))]
    private void AddStep()
    {
        // Create() tạo instance MỚI — nhờ đó 2 bước cùng loại có tham số độc lập.
        var step = new PipelineStepViewModel(SelectedProcessorInfo!.Create());
        step.SettingsChanged += OnStepSettingsChanged;
        Pipeline.Add(step);
        StatusMessage = $"Đã thêm bước: {step.Name}";
        TriggerAutoRun();
    }

    [RelayCommand]
    private void RemoveStep(PipelineStepViewModel step)
    {
        step.SettingsChanged -= OnStepSettingsChanged;
        Pipeline.Remove(step);
        TriggerAutoRun();
    }

    [RelayCommand]
    private void MoveStepUp(PipelineStepViewModel step)
    {
        var index = Pipeline.IndexOf(step);
        if (index > 0)
        {
            Pipeline.Move(index, index - 1);
            TriggerAutoRun();
        }
    }

    [RelayCommand]
    private void MoveStepDown(PipelineStepViewModel step)
    {
        var index = Pipeline.IndexOf(step);
        if (index >= 0 && index < Pipeline.Count - 1)
        {
            Pipeline.Move(index, index + 1);
            TriggerAutoRun();
        }
    }

    [RelayCommand]
    private void ClearPipeline()
    {
        foreach (var step in Pipeline)
            step.SettingsChanged -= OnStepSettingsChanged;
        Pipeline.Clear();
        TriggerAutoRun();
    }

    // ===== Chạy pipeline =====

    private bool CanRunPipeline() => HasImage && !IsBusy && !IsBatchRunning;

    [RelayCommand(CanExecute = nameof(CanRunPipeline))]
    private async Task RunPipelineAsync()
    {
        if (_originalMat is null)
            return;

        IsBusy = true;
        try
        {
            var steps = Pipeline.Where(s => s.IsEnabled).ToList();

            // Clone TRÊN UI THREAD trước khi vào Task.Run: từ đây background thread
            // chỉ làm việc với bản sao, người dùng có sửa ảnh gốc cũng không sao.
            var input = _originalMat.Clone();
            var total = Stopwatch.StartNew();

            // Task.Run đẩy phần tính toán nặng ra background thread → UI không đơ.
            var (result, durations) = await Task.Run(() =>
            {
                var current = input;
                var times = new double[steps.Count];
                for (var i = 0; i < steps.Count; i++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    var next = steps[i].Processor.Process(current);
                    stopwatch.Stop();
                    times[i] = stopwatch.Elapsed.TotalMilliseconds;

                    current.Dispose(); // ảnh trung gian không cần nữa
                    current = next;
                }
                return (current, times);
            });
            total.Stop();

            // Từ sau await, code lại chạy trên UI thread → cập nhật property an toàn.
            for (var i = 0; i < steps.Count; i++)
                steps[i].DurationText = $"{durations[i]:F1} ms";

            _resultMat?.Dispose();
            _resultMat = result;
            ResultImage = _imageService.ToBitmap(result);
            StatusMessage = $"Pipeline xong: {steps.Count} bước, {total.Elapsed.TotalMilliseconds:F1} ms.";
            // Khi chạy hàng loạt, không ghi từng ảnh vào lịch sử (sẽ ngập) —
            // RunFolderAsync ghi MỘT dòng tổng kết khi xong.
            if (!IsBatchRunning)
            {
                History.Insert(0, new LogEntry
                {
                    Operation = $"Pipeline ({steps.Count} bước)",
                    DurationMs = total.Elapsed.TotalMilliseconds,
                });
            }
            SaveResultCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi khi xử lý: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            // Có yêu cầu chạy lại dồn trong lúc đang chạy? Chạy nốt.
            if (_rerunRequested)
            {
                _rerunRequested = false;
                _ = RunPipelineAsync();
            }
        }
    }

    // ===== Auto-run =====

    private void OnStepSettingsChanged(object? sender, EventArgs e) => TriggerAutoRun();

    // Hook của CommunityToolkit: bật công tắc auto-run là chạy ngay một lần.
    partial void OnIsAutoRunEnabledChanged(bool value)
    {
        if (value)
            TriggerAutoRun();
    }

    private void TriggerAutoRun()
    {
        if (!IsAutoRunEnabled || _originalMat is null || IsBatchRunning)
            return;

        if (IsBusy)
        {
            // Đang chạy rồi → chỉ ghi nhớ, RunPipelineAsync sẽ tự chạy lại khi xong.
            _rerunRequested = true;
            return;
        }

        // Fire-and-forget có chủ đích: lỗi đã được xử lý bên trong RunPipelineAsync.
        _ = RunPipelineAsync();
    }

    private void NotifyImageDependentCommands()
    {
        RunPipelineCommand.NotifyCanExecuteChanged();
        RotateLeftCommand.NotifyCanExecuteChanged();
        RotateRightCommand.NotifyCanExecuteChanged();
        FlipHorizontalCommand.NotifyCanExecuteChanged();
        FlipVerticalCommand.NotifyCanExecuteChanged();
        RestoreOriginalCommand.NotifyCanExecuteChanged();
    }

    // ===== Chế độ thư mục: mở, điều hướng, chạy hàng loạt =====

    private bool CanOpenFolder() => !IsBusy && !IsBatchRunning;

    [RelayCommand(CanExecute = nameof(CanOpenFolder))]
    private async Task OpenFolderAsync()
    {
        var folder = await _filePicker.PickFolderAsync();
        if (folder is null)
            return;

        ImageFiles.Clear();
        foreach (var file in _imageService.ListImageFiles(folder))
            ImageFiles.Add(new ImageFileItem(file));

        RunFolderCommand.NotifyCanExecuteChanged();

        if (ImageFiles.Count == 0)
        {
            SelectedImageFile = null;
            StatusMessage = "Thư mục không có file ảnh nào (png / jpg / jpeg / bmp).";
            return;
        }

        StatusMessage = $"Đã nạp {ImageFiles.Count} ảnh từ thư mục.";
        SelectedImageFile = ImageFiles[0]; // hook bên dưới tự load ảnh đầu tiên
    }

    /// <summary>
    /// Hook của CommunityToolkit: chọn ảnh nào trong ListBox là load ảnh đó ngay.
    /// Đây là pattern "selection-driven": ListBox bind SelectedItem (TwoWay),
    /// ViewModel phản ứng với thay đổi — không cần sự kiện Click nào cả.
    /// </summary>
    partial void OnSelectedImageFileChanged(ImageFileItem? value)
    {
        UpdateFolderPosition();
        PreviousImageCommand.NotifyCanExecuteChanged();
        NextImageCommand.NotifyCanExecuteChanged();

        // Khi chạy hàng loạt, vòng lặp batch tự load — hook chỉ cập nhật highlight.
        if (value is null || IsBatchRunning)
            return;

        _ = LoadImageAsync(value.FullPath); // lỗi đã được xử lý bên trong
    }

    private int SelectedImageIndex => SelectedImageFile is null ? -1 : ImageFiles.IndexOf(SelectedImageFile);

    private bool CanGoPrevious() => SelectedImageIndex > 0 && !IsBatchRunning;

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void PreviousImage() => SelectedImageFile = ImageFiles[SelectedImageIndex - 1];

    private bool CanGoNext() => SelectedImageIndex >= 0 && SelectedImageIndex < ImageFiles.Count - 1 && !IsBatchRunning;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextImage() => SelectedImageFile = ImageFiles[SelectedImageIndex + 1];

    private bool CanRunFolder() => ImageFiles.Count > 0 && !IsBusy && !IsBatchRunning;

    /// <summary>
    /// Chạy pipeline LẦN LƯỢT với từng ảnh trong thư mục.
    ///
    /// IncludeCancelCommand = true: toolkit sinh thêm RunFolderCancelCommand —
    /// nút "Dừng" bind vào đó, token sẽ được báo hủy. Đây là cách chuẩn để
    /// dừng một tác vụ dài trong MVVM (không bao giờ Thread.Abort!).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRunFolder), IncludeCancelCommand = true)]
    private async Task RunFolderAsync(CancellationToken token)
    {
        IsBatchRunning = true;
        var total = Stopwatch.StartNew();
        var processed = 0;

        try
        {
            for (var i = 0; i < ImageFiles.Count; i++)
            {
                if (token.IsCancellationRequested)
                    break;

                var item = ImageFiles[i];
                SelectedImageFile = item; // highlight trên ListBox theo tiến độ
                await LoadImageAsync(item.FullPath);
                await RunPipelineAsync();
                processed++;

                if (IsBatchSaveEnabled && _resultMat is not null)
                {
                    var outputPath = Path.Combine(
                        Path.GetDirectoryName(item.FullPath)!, "ket-qua",
                        Path.GetFileNameWithoutExtension(item.Name) + ".png");
                    _imageService.Save(_resultMat, outputPath);
                }

                StatusMessage = $"[{i + 1}/{ImageFiles.Count}] {item.Name}";

                // Nghỉ một nhịp cho mắt kịp nhìn kết quả — cũng là điểm nhận lệnh hủy.
                try
                {
                    await Task.Delay(350, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi khi chạy thư mục: {ex.Message}";
        }
        finally
        {
            total.Stop();
            IsBatchRunning = false;
            History.Insert(0, new LogEntry
            {
                Operation = $"Chạy thư mục ({processed}/{ImageFiles.Count} ảnh)",
                DurationMs = total.Elapsed.TotalMilliseconds,
            });
            StatusMessage = token.IsCancellationRequested
                ? $"Đã dừng: xử lý {processed}/{ImageFiles.Count} ảnh."
                : $"Xong cả thư mục: {processed} ảnh, {total.Elapsed.TotalMilliseconds:F0} ms.";
        }
    }

    // Bật/tắt chạy hàng loạt → gần như mọi nút đổi trạng thái enable.
    partial void OnIsBatchRunningChanged(bool value)
    {
        NotifyImageDependentCommands();
        OpenImageCommand.NotifyCanExecuteChanged();
        OpenFolderCommand.NotifyCanExecuteChanged();
        SaveResultCommand.NotifyCanExecuteChanged();
        RunFolderCommand.NotifyCanExecuteChanged();
        PreviousImageCommand.NotifyCanExecuteChanged();
        NextImageCommand.NotifyCanExecuteChanged();
    }

    private void UpdateFolderPosition()
    {
        FolderPositionText = SelectedImageFile is null || ImageFiles.Count == 0
            ? ""
            : $"{SelectedImageIndex + 1}/{ImageFiles.Count}";
    }
}
