using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace MVATool.Views.Controls;

/// <summary>
/// Control hiển thị ảnh có zoom (lăn chuột), pan (kéo chuột), chọn vùng cắt
/// và đọc tọa độ pixel dưới con trỏ.
///
/// Đây là CUSTOM CONTROL — nơi hợp lệ thứ hai (sau code-behind của Window) được
/// viết code xử lý sự kiện UI trong MVVM: toàn bộ logic ở đây là "toán học
/// hiển thị" (ma trận zoom/pan), KHÔNG phải logic nghiệp vụ.
///
/// Control giao tiếp với thế giới bên ngoài đúng chuẩn MVVM:
///   - Nhận dữ liệu qua StyledProperty (Source, IsCropEnabled) — bind được từ XAML.
///   - Trả kết quả tương tác qua 2 ICommand cũng là StyledProperty:
///       PixelHoverCommand(PixelPoint?) — con trỏ đang chỉ pixel nào (null = ra ngoài ảnh)
///       CropCommand(PixelRect)         — người dùng vừa kéo chọn xong vùng cắt
///   Nhờ vậy ViewModel không biết gì về control, control không biết gì về ViewModel.
/// </summary>
public partial class ImageViewer : UserControl
{
    public static readonly StyledProperty<Bitmap?> SourceProperty =
        AvaloniaProperty.Register<ImageViewer, Bitmap?>(nameof(Source));

    public static readonly StyledProperty<bool> IsCropEnabledProperty =
        AvaloniaProperty.Register<ImageViewer, bool>(nameof(IsCropEnabled));

    public static readonly StyledProperty<ICommand?> CropCommandProperty =
        AvaloniaProperty.Register<ImageViewer, ICommand?>(nameof(CropCommand));

    public static readonly StyledProperty<ICommand?> PixelHoverCommandProperty =
        AvaloniaProperty.Register<ImageViewer, ICommand?>(nameof(PixelHoverCommand));

    public Bitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public bool IsCropEnabled
    {
        get => GetValue(IsCropEnabledProperty);
        set => SetValue(IsCropEnabledProperty, value);
    }

    public ICommand? CropCommand
    {
        get => GetValue(CropCommandProperty);
        set => SetValue(CropCommandProperty, value);
    }

    public ICommand? PixelHoverCommand
    {
        get => GetValue(PixelHoverCommandProperty);
        set => SetValue(PixelHoverCommandProperty, value);
    }

    // Ma trận biến đổi: tọa độ ảnh (pixel) → tọa độ màn hình (trong control).
    // Zoom/pan chỉ là nhân thêm ma trận scale/translate vào đây.
    private Matrix _matrix = Matrix.Identity;

    /// <summary>
    /// Chế độ hiển thị hiện tại — quyết định control tự làm gì khi khung đổi kích thước:
    ///   Auto : MẶC ĐỊNH khi mở ảnh — hiện TRỌN VẸN ảnh nhưng không phóng to quá 100%:
    ///          ảnh nhỏ hơn khung giữ đúng kích thước gốc, ảnh to hơn khung được
    ///          thu nhỏ vừa đủ để không bị cắt mất phần nào.
    ///   Fit  : vừa khung kể cả phải phóng to (chỉ khi bấm nút / double-click).
    ///   Free : người dùng đã zoom/pan tay — control không tự đụng vào nữa.
    /// </summary>
    private enum ViewMode { Auto, Fit, Free }

    private ViewMode _mode = ViewMode.Auto;
    private PixelSize _lastSourceSize; // để nhận ra "vẫn ảnh kích thước cũ" khi Source đổi
    private Point? _panLast;           // vị trí chuột lần trước khi đang kéo pan
    private PixelPoint? _cropStart;    // pixel bắt đầu kéo chọn vùng cắt

    public ImageViewer()
    {
        InitializeComponent();

        PointerWheelChanged += OnPointerWheelChanged;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerExited += OnPointerExited;
        DoubleTapped += (_, _) => { if (!IsCropEnabled) Fit(); };

        PartFitButton.Click += (_, _) => Fit();
        PartActualButton.Click += (_, _) => SetActualSize();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SourceProperty)
        {
            PartImage.Source = Source;

            if (Source is null)
            {
                _lastSourceSize = default;
                _mode = ViewMode.Auto;
                _matrix = Matrix.Identity;
                UpdateView();
            }
            else if (Source.PixelSize != _lastSourceSize)
            {
                // Ảnh MỚI (khác kích thước) → hiện trọn vẹn ảnh, tối đa 100%:
                // ảnh nhỏ giữ đúng kích thước gốc, ảnh to thu nhỏ cho khỏi bị cắt.
                _lastSourceSize = Source.PixelSize;
                AutoView();
            }
            else
            {
                // CÙNG kích thước (chạy lại pipeline, kéo slider, lật ảnh...) →
                // GIỮ NGUYÊN zoom/pan đang xem, chỉ thay nội dung ảnh.
                UpdateView();
            }
        }
        else if (change.Property == IsCropEnabledProperty)
        {
            // Đổi con trỏ chuột thành dấu + khi ở chế độ cắt — phản hồi trực quan.
            Cursor = IsCropEnabled ? new Cursor(StandardCursorType.Cross) : Cursor.Default;
            if (!IsCropEnabled)
            {
                _cropStart = null;
                PartSelection.IsVisible = false;
            }
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        // Khung đổi kích thước: chế độ tự động nào thì căn lại theo chế độ đó.
        // (Quan trọng lúc mở app: Source được set khi khung còn 0×0 — sự kiện
        // này chạy sau khi layout xong mới căn giữa đúng được.)
        if (_mode == ViewMode.Auto)
            AutoView();
        else if (_mode == ViewMode.Fit)
            Fit();
    }

    // ===== Zoom / pan =====

    /// <summary>
    /// Chế độ mặc định: hiện TRỌN VẸN ảnh, căn giữa, KHÔNG phóng to quá 100%.
    /// scale = min(1, tỉ lệ vừa khung) — ảnh nhỏ giữ nguyên kích thước gốc
    /// (1 pixel ảnh = 1 đơn vị màn hình), ảnh to thu nhỏ vừa đủ để không bị cắt.
    /// </summary>
    private void AutoView()
    {
        if (Source is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            UpdateView();
            return;
        }

        var scale = Math.Min(1.0, Math.Min(
            Bounds.Width / Source.PixelSize.Width,
            Bounds.Height / Source.PixelSize.Height));
        var offsetX = (Bounds.Width - Source.PixelSize.Width * scale) / 2;
        var offsetY = (Bounds.Height - Source.PixelSize.Height * scale) / 2;

        // Ở 100%, làm tròn offset để mép ảnh nằm đúng lưới pixel màn hình.
        if (scale >= 1.0)
        {
            offsetX = Math.Round(offsetX);
            offsetY = Math.Round(offsetY);
        }

        _matrix = new Matrix(scale, 0, 0, scale, offsetX, offsetY);
        _mode = ViewMode.Auto;
        UpdateView();
    }

    /// <summary>Thu phóng để ảnh vừa khít khung, căn giữa.</summary>
    private void Fit()
    {
        if (Source is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            UpdateView();
            return;
        }

        var scale = Math.Min(
            Bounds.Width / Source.PixelSize.Width,
            Bounds.Height / Source.PixelSize.Height);
        var offsetX = (Bounds.Width - Source.PixelSize.Width * scale) / 2;
        var offsetY = (Bounds.Height - Source.PixelSize.Height * scale) / 2;

        _matrix = new Matrix(scale, 0, 0, scale, offsetX, offsetY);
        _mode = ViewMode.Fit;
        UpdateView();
    }

    /// <summary>Về zoom 100% (1 pixel ảnh = 1 pixel màn hình), giữ tâm khung đứng yên.</summary>
    private void SetActualSize()
    {
        if (Source is null)
            return;
        ZoomAround(new Point(Bounds.Width / 2, Bounds.Height / 2), 1.0 / _matrix.M11);
    }

    /// <summary>
    /// Zoom quanh một điểm màn hình: điểm đó đứng yên, mọi thứ khác nở ra / co lại.
    /// Công thức: M' = M · T(-p) · S(f) · T(p)  (dịch điểm p về gốc, scale, dịch trả lại).
    /// </summary>
    private void ZoomAround(Point screenPoint, double factor)
    {
        _matrix = _matrix
                  * Matrix.CreateTranslation(-screenPoint.X, -screenPoint.Y)
                  * Matrix.CreateScale(factor, factor)
                  * Matrix.CreateTranslation(screenPoint.X, screenPoint.Y);
        _mode = ViewMode.Free;
        UpdateView();
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (Source is null)
            return;

        var factor = e.Delta.Y > 0 ? 1.25 : 1 / 1.25;
        var newScale = _matrix.M11 * factor;
        if (newScale is < 0.02 or > 64) // chặn zoom quá đà
            return;

        ZoomAround(e.GetPosition(this), factor);
        e.Handled = true;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);

        if (IsCropEnabled && point.Properties.IsLeftButtonPressed
                          && ToPixel(point.Position) is { } start)
        {
            // Bắt đầu kéo chọn vùng cắt (chỉ khi bấm TRÊN ảnh).
            _cropStart = start;
            UpdateSelection(start, start);
        }
        else if (point.Properties.IsLeftButtonPressed || point.Properties.IsMiddleButtonPressed)
        {
            // Bắt đầu pan (kéo thả để di chuyển ảnh).
            _panLast = point.Position;
        }
        else
        {
            return;
        }

        // Capture: từ giờ mọi sự kiện chuột đổ về control này kể cả khi
        // con trỏ trượt ra ngoài — kéo thả mới không bị đứt giữa chừng.
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var position = e.GetPosition(this);

        if (_cropStart is { } start)
        {
            UpdateSelection(start, ClampToImage(position));
        }
        else if (_panLast is { } last)
        {
            var delta = position - last;
            _matrix = _matrix * Matrix.CreateTranslation(delta.X, delta.Y);
            _panLast = position;
            _mode = ViewMode.Free;
            UpdateView();
        }

        // Báo cho ViewModel biết con trỏ đang chỉ pixel nào (null = ngoài ảnh).
        PixelHoverCommand?.Execute(ToPixel(position));
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_cropStart is { } start)
        {
            var end = ClampToImage(e.GetPosition(this));
            _cropStart = null;
            PartSelection.IsVisible = false;

            var rect = NormalizedRect(start, end);
            if (rect.Width >= 2 && rect.Height >= 2)
                CropCommand?.Execute(rect); // giao vùng đã chọn cho ViewModel xử lý
        }

        _panLast = null;
        e.Pointer.Capture(null);
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
        => PixelHoverCommand?.Execute(null);

    // ===== Đổi hệ tọa độ: màn hình ↔ pixel ảnh =====

    /// <summary>Điểm màn hình → pixel ảnh; null nếu ngoài ảnh (dùng ma trận NGHỊCH ĐẢO).</summary>
    private PixelPoint? ToPixel(Point screenPoint)
    {
        if (Source is null || !_matrix.TryInvert(out var inverse))
            return null;

        var p = screenPoint.Transform(inverse);
        var x = (int)Math.Floor(p.X);
        var y = (int)Math.Floor(p.Y);
        if (x < 0 || y < 0 || x >= Source.PixelSize.Width || y >= Source.PixelSize.Height)
            return null;

        return new PixelPoint(x, y);
    }

    /// <summary>Như ToPixel nhưng kẹp vào mép ảnh — dùng khi kéo chọn vùng vượt ra ngoài.</summary>
    private PixelPoint ClampToImage(Point screenPoint)
    {
        if (Source is null || !_matrix.TryInvert(out var inverse))
            return default;

        var p = screenPoint.Transform(inverse);
        var x = Math.Clamp((int)Math.Floor(p.X), 0, Source.PixelSize.Width - 1);
        var y = Math.Clamp((int)Math.Floor(p.Y), 0, Source.PixelSize.Height - 1);
        return new PixelPoint(x, y);
    }

    private static PixelRect NormalizedRect(PixelPoint a, PixelPoint b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        return new PixelRect(x, y, Math.Abs(a.X - b.X) + 1, Math.Abs(a.Y - b.Y) + 1);
    }

    // ===== Vẽ =====

    private void UpdateSelection(PixelPoint a, PixelPoint b)
    {
        var rect = NormalizedRect(a, b);
        // Khung chọn lưu theo PIXEL ẢNH, vẽ theo MÀN HÌNH → transform 2 góc qua ma trận.
        var topLeft = new Point(rect.X, rect.Y).Transform(_matrix);
        var bottomRight = new Point(rect.X + rect.Width, rect.Y + rect.Height).Transform(_matrix);

        Canvas.SetLeft(PartSelection, topLeft.X);
        Canvas.SetTop(PartSelection, topLeft.Y);
        PartSelection.Width = bottomRight.X - topLeft.X;
        PartSelection.Height = bottomRight.Y - topLeft.Y;
        PartSelection.IsVisible = true;
    }

    private void UpdateView()
    {
        PartImage.RenderTransform = new MatrixTransform(_matrix);
        PartZoomText.Text = Source is null ? "" : $"{_matrix.M11 * 100:F0}%";
    }
}
