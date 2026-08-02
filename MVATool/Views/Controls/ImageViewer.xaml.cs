using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
// Tọa độ pixel ảnh dùng kiểu của OpenCvSharp (int) để phân biệt rạch ròi với
// tọa độ màn hình của WPF (System.Windows.Point — double).
using CvPoint = OpenCvSharp.Point;
using CvRect = OpenCvSharp.Rect;

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
///   - Nhận dữ liệu qua DependencyProperty (Source, IsCropEnabled) — bind được từ XAML.
///   - Trả kết quả tương tác qua 2 ICommand cũng là DependencyProperty:
///       PixelHoverCommand(CvPoint?) — con trỏ đang chỉ pixel nào (null = ra ngoài ảnh)
///       CropCommand(CvRect)         — người dùng vừa kéo chọn xong vùng cắt
///   Nhờ vậy ViewModel không biết gì về control, control không biết gì về ViewModel.
/// </summary>
public partial class ImageViewer : UserControl
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(BitmapSource), typeof(ImageViewer),
        new PropertyMetadata(null, (d, _) => ((ImageViewer)d).OnSourceChanged()));

    public static readonly DependencyProperty IsCropEnabledProperty = DependencyProperty.Register(
        nameof(IsCropEnabled), typeof(bool), typeof(ImageViewer),
        new PropertyMetadata(false, (d, _) => ((ImageViewer)d).OnIsCropEnabledChanged()));

    public static readonly DependencyProperty CropCommandProperty = DependencyProperty.Register(
        nameof(CropCommand), typeof(ICommand), typeof(ImageViewer));

    public static readonly DependencyProperty PixelHoverCommandProperty = DependencyProperty.Register(
        nameof(PixelHoverCommand), typeof(ICommand), typeof(ImageViewer));

    public BitmapSource? Source
    {
        get => (BitmapSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public bool IsCropEnabled
    {
        get => (bool)GetValue(IsCropEnabledProperty);
        set => SetValue(IsCropEnabledProperty, value);
    }

    public ICommand? CropCommand
    {
        get => (ICommand?)GetValue(CropCommandProperty);
        set => SetValue(CropCommandProperty, value);
    }

    public ICommand? PixelHoverCommand
    {
        get => (ICommand?)GetValue(PixelHoverCommandProperty);
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
    private (int Width, int Height) _lastSourceSize; // nhận ra "vẫn ảnh kích thước cũ" khi Source đổi
    private Point? _panLast;                          // vị trí chuột lần trước khi đang kéo pan
    private CvPoint? _cropStart;                      // pixel bắt đầu kéo chọn vùng cắt

    public ImageViewer()
    {
        InitializeComponent();

        MouseWheel += OnMouseWheelChanged;
        MouseDown += OnMousePressed;
        MouseMove += OnMouseMoved;
        MouseUp += OnMouseReleased;
        MouseLeave += (_, _) => PixelHoverCommand?.Execute(null);
        SizeChanged += OnViewerSizeChanged;

        PartFitButton.Click += (_, _) => Fit();
        PartActualButton.Click += (_, _) => SetActualSize();
    }

    private void OnSourceChanged()
    {
        PartImage.Source = Source;

        if (Source is null)
        {
            _lastSourceSize = default;
            _mode = ViewMode.Auto;
            _matrix = Matrix.Identity;
            UpdateView();
        }
        else if ((Source.PixelWidth, Source.PixelHeight) != _lastSourceSize)
        {
            // Ảnh MỚI (khác kích thước) → hiện trọn vẹn ảnh, tối đa 100%:
            // ảnh nhỏ giữ đúng kích thước gốc, ảnh to thu nhỏ cho khỏi bị cắt.
            _lastSourceSize = (Source.PixelWidth, Source.PixelHeight);
            AutoView();
        }
        else
        {
            // CÙNG kích thước (chạy lại pipeline, kéo slider, lật ảnh...) →
            // GIỮ NGUYÊN zoom/pan đang xem, chỉ thay nội dung ảnh.
            UpdateView();
        }
    }

    private void OnIsCropEnabledChanged()
    {
        // Đổi con trỏ chuột thành dấu + khi ở chế độ cắt — phản hồi trực quan.
        Cursor = IsCropEnabled ? Cursors.Cross : null;
        if (!IsCropEnabled)
        {
            _cropStart = null;
            PartSelection.Visibility = Visibility.Collapsed;
        }
    }

    private void OnViewerSizeChanged(object sender, SizeChangedEventArgs e)
    {
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
        if (Source is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            UpdateView();
            return;
        }

        var scale = Math.Min(1.0, Math.Min(
            ActualWidth / Source.PixelWidth,
            ActualHeight / Source.PixelHeight));
        var offsetX = (ActualWidth - Source.PixelWidth * scale) / 2;
        var offsetY = (ActualHeight - Source.PixelHeight * scale) / 2;

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
        if (Source is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            UpdateView();
            return;
        }

        var scale = Math.Min(
            ActualWidth / Source.PixelWidth,
            ActualHeight / Source.PixelHeight);
        var offsetX = (ActualWidth - Source.PixelWidth * scale) / 2;
        var offsetY = (ActualHeight - Source.PixelHeight * scale) / 2;

        _matrix = new Matrix(scale, 0, 0, scale, offsetX, offsetY);
        _mode = ViewMode.Fit;
        UpdateView();
    }

    /// <summary>Về zoom 100% (1 pixel ảnh = 1 pixel màn hình), giữ tâm khung đứng yên.</summary>
    private void SetActualSize()
    {
        if (Source is null)
            return;
        ZoomAround(new Point(ActualWidth / 2, ActualHeight / 2), 1.0 / _matrix.M11);
    }

    /// <summary>
    /// Zoom quanh một điểm màn hình: điểm đó đứng yên, mọi thứ khác nở ra / co lại.
    /// Matrix.ScaleAt của WPF chính là phép M' = M · T(-p) · S(f) · T(p).
    /// </summary>
    private void ZoomAround(Point screenPoint, double factor)
    {
        _matrix.ScaleAt(factor, factor, screenPoint.X, screenPoint.Y);
        _mode = ViewMode.Free;
        UpdateView();
    }

    private void OnMouseWheelChanged(object sender, MouseWheelEventArgs e)
    {
        if (Source is null)
            return;

        var factor = e.Delta > 0 ? 1.25 : 1 / 1.25;
        var newScale = _matrix.M11 * factor;
        if (newScale is < 0.02 or > 64) // chặn zoom quá đà
            return;

        ZoomAround(e.GetPosition(this), factor);
        e.Handled = true;
    }

    private void OnMousePressed(object sender, MouseButtonEventArgs e)
    {
        // Double-click chuột trái = vừa khung (khi không ở chế độ cắt).
        if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2 && !IsCropEnabled)
        {
            Fit();
            e.Handled = true;
            return;
        }

        var position = e.GetPosition(this);

        if (IsCropEnabled && e.ChangedButton == MouseButton.Left
                          && ToPixel(position) is { } start)
        {
            // Bắt đầu kéo chọn vùng cắt (chỉ khi bấm TRÊN ảnh).
            _cropStart = start;
            UpdateSelection(start, start);
        }
        else if (e.ChangedButton is MouseButton.Left or MouseButton.Middle)
        {
            // Bắt đầu pan (kéo thả để di chuyển ảnh).
            _panLast = position;
        }
        else
        {
            return;
        }

        // Capture: từ giờ mọi sự kiện chuột đổ về control này kể cả khi
        // con trỏ trượt ra ngoài — kéo thả mới không bị đứt giữa chừng.
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMoved(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(this);

        if (_cropStart is { } start)
        {
            UpdateSelection(start, ClampToImage(position));
        }
        else if (_panLast is { } last)
        {
            _matrix.Translate(position.X - last.X, position.Y - last.Y);
            _panLast = position;
            _mode = ViewMode.Free;
            UpdateView();
        }

        // Báo cho ViewModel biết con trỏ đang chỉ pixel nào (null = ngoài ảnh).
        PixelHoverCommand?.Execute(ToPixel(position));
    }

    private void OnMouseReleased(object sender, MouseButtonEventArgs e)
    {
        if (_cropStart is { } start)
        {
            var end = ClampToImage(e.GetPosition(this));
            _cropStart = null;
            PartSelection.Visibility = Visibility.Collapsed;

            var rect = NormalizedRect(start, end);
            if (rect.Width >= 2 && rect.Height >= 2)
                CropCommand?.Execute(rect); // giao vùng đã chọn cho ViewModel xử lý
        }

        _panLast = null;
        ReleaseMouseCapture();
    }

    // ===== Đổi hệ tọa độ: màn hình ↔ pixel ảnh =====

    /// <summary>Điểm màn hình → pixel ảnh; null nếu ngoài ảnh (dùng ma trận NGHỊCH ĐẢO).</summary>
    private CvPoint? ToPixel(Point screenPoint)
    {
        if (Source is null || !_matrix.HasInverse)
            return null;

        // Matrix của WPF là struct → copy ra rồi Invert() không đụng bản gốc.
        var inverse = _matrix;
        inverse.Invert();

        var p = inverse.Transform(screenPoint);
        var x = (int)Math.Floor(p.X);
        var y = (int)Math.Floor(p.Y);
        if (x < 0 || y < 0 || x >= Source.PixelWidth || y >= Source.PixelHeight)
            return null;

        return new CvPoint(x, y);
    }

    /// <summary>Như ToPixel nhưng kẹp vào mép ảnh — dùng khi kéo chọn vùng vượt ra ngoài.</summary>
    private CvPoint ClampToImage(Point screenPoint)
    {
        if (Source is null || !_matrix.HasInverse)
            return default;

        var inverse = _matrix;
        inverse.Invert();

        var p = inverse.Transform(screenPoint);
        var x = Math.Clamp((int)Math.Floor(p.X), 0, Source.PixelWidth - 1);
        var y = Math.Clamp((int)Math.Floor(p.Y), 0, Source.PixelHeight - 1);
        return new CvPoint(x, y);
    }

    private static CvRect NormalizedRect(CvPoint a, CvPoint b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        return new CvRect(x, y, Math.Abs(a.X - b.X) + 1, Math.Abs(a.Y - b.Y) + 1);
    }

    // ===== Vẽ =====

    private void UpdateSelection(CvPoint a, CvPoint b)
    {
        var rect = NormalizedRect(a, b);
        // Khung chọn lưu theo PIXEL ẢNH, vẽ theo MÀN HÌNH → transform 2 góc qua ma trận.
        var topLeft = _matrix.Transform(new Point(rect.X, rect.Y));
        var bottomRight = _matrix.Transform(new Point(rect.X + rect.Width, rect.Y + rect.Height));

        Canvas.SetLeft(PartSelection, topLeft.X);
        Canvas.SetTop(PartSelection, topLeft.Y);
        PartSelection.Width = bottomRight.X - topLeft.X;
        PartSelection.Height = bottomRight.Y - topLeft.Y;
        PartSelection.Visibility = Visibility.Visible;
    }

    private void UpdateView()
    {
        PartImage.RenderTransform = new MatrixTransform(_matrix);
        PartZoomText.Text = Source is null ? "" : $"{_matrix.M11 * 100:F0}%";
    }
}
