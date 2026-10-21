using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MVATool.ViewModels;

namespace MVATool.Views.Controls;

/// <summary>
/// LỚP PHỦ vẽ dây nối giữa các terminal — kiểu Job Editor của VisionPro:
///
///     O Ảnh ra ─────┐
///                   │      (dây đi vòng ở LỀ TRÁI, mỗi dây một "làn" riêng)
///     ...           │
///     O Giá trị  ◄──┘
///
/// Cách dùng: đặt control này làm con CUỐI CÙNG của Grid chứa danh sách tool
/// (để nằm đè lên trên). Chấm O trong DataTemplate phải có Tag="Terminal",
/// DataContext là InputTerminalViewModel / OutputTerminalViewModel.
///
/// Tương tác:
///   - Kéo từ chấm O này thả vào chấm O khác → ConnectCommand(TerminalPair)
///   - Chuột phải vào chấm O vào (hoặc vào dây) → DisconnectCommand(InputTerminalViewModel)
///
/// Đây là code-behind THUẦN VIEW (tính tọa độ, vẽ đường) — mọi quyết định
/// nối được hay không vẫn nằm ở ViewModel, nên vẫn đúng tinh thần MVVM.
/// </summary>
public class TerminalWires : Canvas
{
    public static readonly DependencyProperty ConnectCommandProperty = DependencyProperty.Register(
        nameof(ConnectCommand), typeof(ICommand), typeof(TerminalWires));

    public static readonly DependencyProperty DisconnectCommandProperty = DependencyProperty.Register(
        nameof(DisconnectCommand), typeof(ICommand), typeof(TerminalWires));

    /// <summary>Lề trái dành cho các làn dây (px) — Margin trái của danh sách tool phải bằng số này.</summary>
    public const double GutterWidth = 44;

    private const double LaneSpacing = 6;

    private Panel? _host;
    private string _lastSignature = "";

    // Trạng thái kéo dây
    private FrameworkElement? _dragStart;
    private readonly Path _dragWire = new() { StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 3, 2 }, IsHitTestVisible = false };

    public TerminalWires()
    {
        // Background null: chuột "xuyên qua" lớp phủ xuống slider/nút bên dưới,
        // chỉ các đường dây (con của Canvas) mới bắt được chuột.
        Background = null;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public ICommand? ConnectCommand
    {
        get => (ICommand?)GetValue(ConnectCommandProperty);
        set => SetValue(ConnectCommandProperty, value);
    }

    public ICommand? DisconnectCommand
    {
        get => (ICommand?)GetValue(DisconnectCommandProperty);
        set => SetValue(DisconnectCommandProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _host = VisualParent as Panel;
        if (_host is null)
            return;

        // Nghe sự kiện chuột ở Grid CHA: chấm O nằm dưới lớp phủ nên lớp phủ không tự nhận được.
        _host.PreviewMouseLeftButtonDown += OnHostMouseDown;
        _host.PreviewMouseMove += OnHostMouseMove;
        _host.PreviewMouseLeftButtonUp += OnHostMouseUp;
        _host.PreviewMouseRightButtonUp += OnHostRightUp;
        _host.LayoutUpdated += OnHostLayoutUpdated;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_host is null)
            return;

        _host.PreviewMouseLeftButtonDown -= OnHostMouseDown;
        _host.PreviewMouseMove -= OnHostMouseMove;
        _host.PreviewMouseLeftButtonUp -= OnHostMouseUp;
        _host.PreviewMouseRightButtonUp -= OnHostRightUp;
        _host.LayoutUpdated -= OnHostLayoutUpdated;
        _host = null;
    }

    // ===== Vẽ dây =====

    private record struct Wire(Point From, Point To, Brush Brush, InputTerminalViewModel Input);

    // LayoutUpdated bắn RẤT nhiều lần (kể cả do chính việc vẽ dây) → chỉ vẽ lại khi
    // vị trí chấm O hoặc dây nối thực sự đổi, nếu không sẽ lặp vô tận.
    private void OnHostLayoutUpdated(object? sender, EventArgs e)
    {
        if (_host is null || _dragStart is not null)
            return;

        var wires = CollectWires();
        var signature = new StringBuilder();
        foreach (var w in wires)
            signature.Append($"{w.From.X:F0},{w.From.Y:F0}>{w.To.X:F0},{w.To.Y:F0}|");

        if (signature.ToString() == _lastSignature)
            return;

        _lastSignature = signature.ToString();
        Redraw(wires);
    }

    private List<Wire> CollectWires()
    {
        var outputs = new Dictionary<object, Point>();
        var inputs = new List<(InputTerminalViewModel Vm, Point Center)>();

        foreach (var dot in FindTerminals(_host!))
        {
            var center = dot.TranslatePoint(new Point(dot.ActualWidth / 2, dot.ActualHeight / 2), this);
            switch (dot.DataContext)
            {
                case OutputTerminalViewModel o:
                    outputs[o.Output] = center;
                    break;
                case InputTerminalViewModel i:
                    inputs.Add((i, center));
                    break;
            }
        }

        var wires = new List<Wire>();
        foreach (var (input, center) in inputs)
        {
            var source = input.Parameter.Source;
            if (source is not null && outputs.TryGetValue(source, out var from))
                wires.Add(new Wire(from, center, TerminalBrushes.For(source.ValueType), input));
        }
        return wires;
    }

    private void Redraw(List<Wire> wires)
    {
        Children.Clear();

        // Chia LÀN: dây nào có khoảng dọc chồng lên nhau thì phải đi làn khác nhau
        // (dây dài đi làn ngoài cùng, giống cách VisionPro xếp).
        var ordered = wires.OrderBy(w => Math.Abs(w.To.Y - w.From.Y)).ToList();
        var lanes = new List<(double Top, double Bottom, int Lane)>();

        foreach (var wire in ordered)
        {
            var top = Math.Min(wire.From.Y, wire.To.Y);
            var bottom = Math.Max(wire.From.Y, wire.To.Y);
            var lane = 0;
            while (lanes.Any(l => l.Lane == lane && l.Top <= bottom + 2 && top <= l.Bottom + 2))
                lane++;
            lanes.Add((top, bottom, lane));

            var x = Math.Max(4, GutterWidth - 8 - lane * LaneSpacing);
            Children.Add(CreateWirePath(wire, x));
        }
    }

    private static Path CreateWirePath(Wire wire, double laneX)
    {
        // Đường gấp khúc: ra trái tới làn → đi dọc → vào phải tới chấm O + mũi tên.
        // IsFilled = false: chỉ tô mũi tên, không tô vùng bên trong đường gấp khúc.
        var figure = new PathFigure { StartPoint = wire.From, IsFilled = false };
        figure.Segments.Add(new LineSegment(new Point(laneX, wire.From.Y), true));
        figure.Segments.Add(new LineSegment(new Point(laneX, wire.To.Y), true));
        figure.Segments.Add(new LineSegment(new Point(wire.To.X - 6, wire.To.Y), true));

        var arrow = new PathFigure { StartPoint = new Point(wire.To.X - 11, wire.To.Y - 4), IsClosed = true, IsFilled = true };
        arrow.Segments.Add(new LineSegment(new Point(wire.To.X - 5, wire.To.Y), true));
        arrow.Segments.Add(new LineSegment(new Point(wire.To.X - 11, wire.To.Y + 4), true));

        var path = new Path
        {
            Data = new PathGeometry(new[] { figure, arrow }),
            Stroke = wire.Brush,
            Fill = wire.Brush,
            StrokeThickness = 1.6,
            Tag = wire.Input,
            Cursor = Cursors.Hand,
            ToolTip = $"{wire.Input.SourceLabel} → {wire.Input.Step.Title} › {wire.Input.Parameter.Name}\nChuột phải: cắt dây",
        };
        return path;
    }

    // ===== Kéo dây =====

    private void OnHostMouseDown(object sender, MouseButtonEventArgs e)
    {
        var dot = FindTerminalAt(e.OriginalSource as DependencyObject);
        if (dot is null)
            return;

        _dragStart = dot;
        var type = dot.DataContext switch
        {
            OutputTerminalViewModel o => o.DataType,
            InputTerminalViewModel i => i.DataType,
            _ => null,
        };
        _dragWire.Stroke = TerminalBrushes.For(type);
        UpdateDragWire(e.GetPosition(this));
        Children.Add(_dragWire);

        _host!.CaptureMouse();
        e.Handled = true;
    }

    private void OnHostMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is not null)
            UpdateDragWire(e.GetPosition(this));
    }

    private void OnHostMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is null)
            return;

        var start = _dragStart;
        _dragStart = null;
        Children.Remove(_dragWire);
        _host!.ReleaseMouseCapture();
        _lastSignature = ""; // vẽ lại dây ở lần layout tới

        var target = FindTerminalAt(_host.InputHitTest(e.GetPosition(_host)) as DependencyObject);
        if (target is not null && target != start)
        {
            var pair = new TerminalPair(start.DataContext, target.DataContext);
            if (ConnectCommand?.CanExecute(pair) == true)
                ConnectCommand.Execute(pair);
        }
        InvalidateArrange();
        e.Handled = true;
    }

    private void UpdateDragWire(Point mouse)
    {
        var from = _dragStart!.TranslatePoint(
            new Point(_dragStart.ActualWidth / 2, _dragStart.ActualHeight / 2), this);
        _dragWire.Data = new LineGeometry(from, mouse);
    }

    private void OnHostRightUp(object sender, MouseButtonEventArgs e)
    {
        // Chuột phải vào chấm O vào, hoặc vào chính sợi dây → cắt dây.
        var input = (e.OriginalSource as FrameworkElement)?.Tag as InputTerminalViewModel
                    ?? FindTerminalAt(e.OriginalSource as DependencyObject)?.DataContext as InputTerminalViewModel;
        if (input is null)
            return;

        if (DisconnectCommand?.CanExecute(input) == true)
            DisconnectCommand.Execute(input);
        e.Handled = true;
    }

    // ===== Tìm chấm O trong cây giao diện =====

    private static FrameworkElement? FindTerminalAt(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is FrameworkElement { Tag: "Terminal" } dot)
                return dot;
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }
        return null;
    }

    private IEnumerable<FrameworkElement> FindTerminals(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child == this)
                continue;

            if (child is FrameworkElement { Tag: "Terminal", IsVisible: true } dot)
                yield return dot;

            foreach (var nested in FindTerminals(child))
                yield return nested;
        }
    }
}
