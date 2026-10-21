# MVAVision — Machine Vision Suite

Project luyện tập **WPF + MVVM**, đồng thời là nền để **tích hợp thuật toán
machine vision (OpenCV)**. Kiến trúc là bản rút gọn của VisionPlatform:
cùng stack MVVM (WPF, CommunityToolkit.Mvvm, OpenCvSharp), cùng tư tưởng tách tầng.

Pipeline xử lý ảnh được tổ chức thành **job kiểu VisionPro**: mỗi bước là một **tool**
có **terminal vào / ra** (chấm O), các tool **nối dây** với nhau để truyền ảnh, số đo,
contour, Đúng/Sai... và job có thể tự **phán định OK/NG**.

Giao diện theo phong cách **tool công nghiệp**: logo MVA, đèn LED trạng thái
(SẴN SÀNG / ĐANG XỬ LÝ / BATCH), đồng hồ hệ thống, và khối **giám sát tài nguyên**
(CPU / RAM / ổ đĩa / bộ nhớ app) cập nhật mỗi giây ngay trên giao diện.

## Ứng dụng làm được gì

- Mở ảnh (nút, `Ctrl+O`, **kéo thả file vào cửa sổ**, hoặc truyền đường dẫn
  qua dòng lệnh: `MVATool.exe C:\anh\mau.png`).
- **Mở cả thư mục ảnh**: danh sách file hiện ở panel trái, click file nào xem file đó,
  điều hướng ◀ ▶, và **▶▶ chạy job lần lượt với từng ảnh** (dừng được giữa chừng
  bằng nút ⏹); tùy chọn lưu kết quả từng ảnh vào thư mục con `ket-qua`.
- **Zoom / pan trên ảnh**: ảnh mở lên hiện **trọn vẹn, không phóng to quá 100%** —
  ảnh nhỏ giữ đúng kích thước gốc, ảnh to hơn khung được thu nhỏ vừa đủ để không
  bị cắt; lăn chuột để zoom (quanh vị trí con trỏ), kéo chuột để di chuyển,
  double-click hoặc nút "Vừa khung" / "1:1"; zoom to thấy rõ từng pixel
  (nearest-neighbor, không làm nhòe). Chạy lại job với ảnh cùng kích thước
  thì **giữ nguyên** góc nhìn đang xem (tiện khi kéo slider so sánh kết quả).
- **Đọc pixel dưới con trỏ**: tọa độ (x, y) + giá trị màu R-G-B (hoặc mức xám)
  hiện trên thanh trạng thái — di chuột trên ảnh gốc lẫn ảnh kết quả đều được.
- **Thao tác với ảnh gốc**: ✂ cắt (kéo chuột chọn vùng), ⟲⟳ xoay 90°, ⇆⇅ lật,
  ↩ khôi phục về ảnh như lúc mở file.
- Xây **job nhiều tool**: thêm / xóa / đổi thứ tự / bật-tắt từng tool
  (26 tool có sẵn — xem [Danh mục tool](#danh-mục-tool)).
- Mỗi tool có **tham số riêng** (Slider / CheckBox / ComboBox) — UI tự sinh theo kiểu tham số.
- **Terminal & dây nối** (giống Job Editor của VisionPro): terminal vào là chấm O rỗng,
  terminal ra là chấm O đặc kèm giá trị lần chạy gần nhất ("128", "OK", "640×480",
  "12 contour"...). **Kéo** từ chấm này thả vào chấm kia để nối, **chuột phải** vào
  chấm / dây để gỡ. Chỉ nối được tới tool **đứng trước** và **cùng kiểu dữ liệu**
  (Ảnh, Số, Đúng/Sai, Chữ, Contours) — nối sai kiểu bị chặn ngay lúc nối.
  "Ảnh vào" không nối thì tự lấy ảnh ra của tool trước → xếp
  Grayscale → Blur → Threshold là chạy luôn, không cần nối dây.
- **Phán định OK/NG**, ví dụ: Threshold → Tìm contour → So sánh (nối "Giá trị" ←
  "Số contour", chọn "=", đặt "Ngưỡng") → Hiển thị OK/NG (nối "Đạt" ← "Đạt") —
  ảnh kết quả có chữ OK xanh / NG đỏ. Nhiều phép kiểm tra thì gộp bằng Logic AND/OR.
- **Lưu / mở job** (`Ctrl+Shift+S` / `Ctrl+Shift+O`): file `.mvajob` dạng JSON, mở bằng
  Notepad cũng đọc được; lưu cả giá trị tham số lẫn dây nối. File thiếu/thừa tham số
  hoặc chứa tool lạ vẫn mở được — chỗ không khớp chỉ sinh cảnh báo.
- **Auto-run**: kéo slider, nối dây hay cắt/xoay ảnh là kết quả cập nhật ngay
  (xử lý chạy nền, UI không đơ).
- Xem **ảnh gốc / kết quả cạnh nhau**, thời gian chạy từng tool, lịch sử.
- Lưu kết quả (`Ctrl+S`), chạy tay bằng `F5`, đổi theme sáng / tối.
- **Giám sát hệ thống**: CPU toàn máy (`GetSystemTimes`), RAM đã dùng / tổng
  (`GlobalMemoryStatusEx`), dung lượng trống từng ổ đĩa (`DriveInfo`), working set
  của app — panel trái + bản rút gọn trên thanh trạng thái, cập nhật mỗi giây.

### Phím tắt

| Phím | Chức năng |
|---|---|
| `Ctrl+O` | Mở ảnh |
| `Ctrl+S` | Lưu ảnh kết quả |
| `F5` | Chạy job |
| `Ctrl+Shift+O` | Mở job (`.mvajob`) |
| `Ctrl+Shift+S` | Lưu job |

## Danh mục tool

| Nhóm | Tool | Terminal vào / ra phụ (ngoài "Ảnh vào" / "Ảnh ra") |
|---|---|---|
| Tiền xử lý | Grayscale, Invert, Gaussian Blur, Median Filter, Bilateral Filter, Khử nhiễu (Non-Local Means), Làm sắc nét (Unsharp), Cân bằng histogram | — |
| Hình học | Thay đổi kích thước | ra: Rộng, Cao |
| | Xoay ảnh | — |
| Phân đoạn & biên | Threshold, Morphology, Sobel, Laplacian, Canny Edge | — |
| | Áp mặt nạ (Mask) | vào: Mặt nạ |
| Contour & đo lường | Tìm contour | ra: Contours, Số contour |
| | Xấp xỉ contour (đa giác) | vào: Contours · ra: Contours xấp xỉ, Số đỉnh |
| | Bounding Box / Min Area Rect | vào: Contours · ra: Số hộp, Rộng, Cao, Góc |
| | Convex Hull & Defects | vào: Contours · ra: Hulls, Số defect, Độ đặc |
| | Vẽ contour | vào: Contours |
| | Đo độ sáng TB | ra: Độ sáng TB |
| | Đếm blob (**bài tập**) | — |
| Phán định | So sánh (OK/NG) — không có ảnh | vào: Giá trị, Phép so sánh, Ngưỡng · ra: Đạt, Kết quả |
| | Logic AND/OR — không có ảnh | vào: A, Phép, B · ra: Đạt, Kết quả |
| | Hiển thị OK/NG | vào: Đạt |

Ngoài ra mọi tham số Slider / CheckBox cũng là terminal vào: có thể nối vào terminal ra
cùng kiểu (Số / Đúng/Sai) thay vì nhập tay. Tham số ComboBox thì chỉ chọn tay.

## Cách chạy

```bash
dotnet run --project MVATool     # chạy app
dotnet test                      # chạy unit test
```

Yêu cầu: .NET 8 SDK, Windows.

## Kiến trúc 3 project — giống mô hình VisionPlatform thật

```
MVATOOL/
├── MVATool.Core/                  # ⭐ TẦNG THUẬT TOÁN — không biết gì về UI
│   ├── IImageProcessor.cs         #    thuật toán ảnh: 1 ảnh vào → 1 ảnh ra (+ đầu ra phụ)
│   ├── IVisionTool.cs             #    TOOL tổng quát: terminal vào/ra + Run() (kiểu CogTool)
│   ├── ProcessorCatalog.cs        #    danh mục tool (nơi duy nhất phải sửa khi thêm mới)
│   ├── Parameters/                #    TERMINAL VÀO: Number/Bool/ChoiceParameter (bind thẳng lên UI)
│   │                              #    + InputPort<T> (chỉ nhận dữ liệu qua dây)
│   ├── Pipeline/                  #    JOB: PipelineNode, PipelineRunner (chạy + truyền qua dây),
│   │                              #    ToolOutput<T> (terminal ra), ImageProcessorTool (bọc processor),
│   │                              #    JobSerializer (lưu/mở .mvajob), PortTypes (tên kiểu dữ liệu)
│   ├── Processors/                #    thuật toán OpenCV: lọc, biên, hình học, contour, đo lường...
│   └── Tools/                     #    tool không xử lý ảnh: CompareTool, LogicTool
│
├── MVATool/                       # APP WPF
│   ├── App.xaml.cs                #    composition root — DI container thật (MS.Extensions.DI)
│   ├── Assets/                    #    logo MVA, icon .exe
│   ├── Themes/                    #    bảng màu công nghiệp Dark/Light (hoán đổi lúc chạy)
│   ├── Models/                    #    MODEL: LogEntry, ImageFileItem — dữ liệu thuần
│   ├── ViewModels/                #    VIEWMODEL: MainWindowViewModel, PipelineStepViewModel,
│   │                              #    TerminalViewModels (terminal vào/ra), SystemStatusViewModel
│   ├── Views/MainWindow.xaml      #    VIEW: chỉ XAML + binding; code-behind chỉ có
│   │                              #    drag-drop & đổi theme (ngoại lệ hợp lệ, có giải thích)
│   ├── Views/Controls/            #    CUSTOM CONTROL: ImageViewer — zoom/pan/crop/đọc pixel;
│   │                              #    TerminalWires — lớp phủ vẽ & kéo dây nối giữa các terminal
│   └── Services/                  #    SERVICE: FilePicker (dialog ảnh/thư mục/job), ImageService
│                                  #    (Mat↔BitmapSource), SystemInfoService (CPU/RAM/ổ đĩa — P/Invoke)
│
└── MVATool.Tests/                 # UNIT TEST — chứng minh lợi ích lớn nhất của MVVM:
    ├── FakeServices.cs            #    service giả thay dialog/file thật
    ├── ProcessorTests.cs          #    test thuật toán không cần mở app
    ├── AlgorithmTests.cs          #    test các thuật toán contour / đo lường / phán định
    ├── PipelineLinkTests.cs       #    test nối dây, chặn sai kiểu, truyền dữ liệu giữa tool
    ├── JobFileTests.cs            #    test lưu/mở .mvajob (round-trip, file hỏng, tool lạ)
    ├── FolderBatchTests.cs        #    test chạy cả thư mục + hủy giữa chừng
    ├── ImageEditingTests.cs       #    test cắt / xoay / lật ảnh gốc
    └── MainWindowViewModelTests.cs#    test hành vi UI không cần cửa sổ nào
```

**Quy tắc vàng:** `MVATool.Core` không được tham chiếu WPF. ViewModel không được
`using System.Windows.Controls`. View không chứa logic nghiệp vụ.

## Các khái niệm MVVM xuất hiện ở đâu

| Khái niệm | Xem ở |
|---|---|
| `[ObservableProperty]` (INotifyPropertyChanged) | `MainWindowViewModel` — `ResultImage`, `IsBusy`... |
| `[RelayCommand]` + `CanExecute` (nút tự khóa/mở) | `RunPipelineAsync` + `CanRunPipeline` |
| `[NotifyCanExecuteChangedFor]` | property `IsBusy` — đổi là 3 nút tự cập nhật |
| `[NotifyPropertyChangedFor]` (property phụ thuộc) | `NumberParameter.Value` → `DisplayText`; `ProcessorParameter.Source` → `IsLinked` |
| Command có THAM SỐ (`CommandParameter`) | nút ↑ ↓ ✕ của từng tool trong job |
| Binding thoát khỏi DataTemplate (`RelativeSource AncestorType=Window`) | `MainWindow.xaml`, phần tool card |
| DataTemplate **ngầm định theo kiểu dữ liệu** | `Window.Resources` — Slider / CheckBox / ComboBox / InputPort theo kiểu tham số |
| `ObservableCollection` | `Pipeline`, `History` |
| `IValueConverter` tự viết (null / 0 / phủ định → Visibility) | `Converters.cs` — dòng gợi ý khi chưa có ảnh / job rỗng |
| `KeyBinding` gọi command | `Ctrl+O`, `Ctrl+S`, `F5`, `Ctrl+Shift+O`, `Ctrl+Shift+S` |
| Async command + `IsBusy` + `Task.Run` | `RunPipelineAsync` — xử lý nặng không đơ UI |
| **Hủy tác vụ dài**: `CancellationToken` + `IncludeCancelCommand` | `RunFolderAsync` — nút ⏹ Dừng bind vào `RunFolderCancelCommand` |
| Selection-driven: bind `SelectedItem`, phản ứng trong hook | `OnSelectedImageFileChanged` — chọn file là load, không cần Click event |
| Coalescing (dồn yêu cầu chạy lại) | `TriggerAutoRun` + `_rerunRequested` |
| Partial method hook của toolkit | `OnIsAutoRunEnabledChanged`, `OnIsEnabledChanged` |
| Hook `OnXxxChanging` để chặn giá trị sai | `ProcessorParameter.OnSourceChanging` — nối sai kiểu ném lỗi ngay lúc nối |
| Wrapper ViewModel bọc đối tượng Core | `PipelineStepViewModel` bọc `PipelineNode`; `InputTerminalViewModel` / `OutputTerminalViewModel` bọc terminal |
| Gộp nhiều thay đổi thành 1 event | `PipelineStepViewModel.SettingsChanged` → auto-run (bỏ qua Value do dây nối chép vào, tránh lặp vô tận) |
| DI container + constructor injection | `App.xaml.cs` (composition root) |
| Code-behind "hợp lệ" (drag-drop, theme) | `MainWindow.xaml.cs` — đọc comment đầu file |
| **Custom control** với `DependencyProperty` | `Views/Controls/ImageViewer` — Source, IsCropEnabled |
| Control trả kết quả qua `ICommand` property | `ImageViewer` — `CropCommand`, `PixelHoverCommand`; `TerminalWires` — `ConnectCommand`, `DisconnectCommand` |
| Lớp phủ thuần View (tính tọa độ, vẽ đường) — quyết định vẫn ở ViewModel | `TerminalWires` — kéo dây gửi `TerminalPair`, ViewModel quyết định nối được hay không |
| Ma trận biến đổi 2D (zoom quanh 1 điểm, nghịch đảo để đổi hệ tọa độ) | `ImageViewer.xaml.cs` — `ZoomAround`, `ToPixel` |
| Child-ViewModel (tách mảng chức năng riêng) | `SystemStatusViewModel` — View bind `{Binding SystemStatus.CpuText}` |
| Property TÍNH TOÁN + `[NotifyPropertyChangedFor]` | `SystemStateText` — đèn LED đổi theo `IsBusy`/`IsBatchRunning` |
| `DataTrigger` + `Storyboard` (style phản ứng theo trạng thái) | đèn LED đổi màu + nhấp nháy, thanh đo chuyển đỏ khi quá tải |
| Đổi theme lúc chạy (hoán đổi `ResourceDictionary`) | `Themes/Dark.xaml` ↔ `Light.xaml` — bộ màu công nghiệp `MvaXxx` |
| Unit test ViewModel bằng fake service | `MVATool.Tests` |

## Tích hợp thuật toán machine vision như thế nào?

Có **2 điểm mở rộng** trong `MVATool.Core`:

```csharp
// 1) Thuật toán ảnh: 1 ảnh vào → 1 ảnh ra — dùng cho hầu hết trường hợp
public interface IImageProcessor
{
    string Name { get; }
    IReadOnlyList<ProcessorParameter> Parameters { get; }  // terminal vào, hiện lên UI
    Mat Process(Mat input);                                 // nhận Mat, trả Mat MỚI
    IReadOnlyList<ToolOutput> Outputs => Array.Empty<ToolOutput>(); // terminal ra PHỤ (tùy chọn)
}

// 2) Tool tổng quát — không bắt buộc có ảnh (so sánh số, logic AND/OR...)
public interface IVisionTool
{
    string Name { get; }
    IReadOnlyList<ProcessorParameter> Parameters { get; }  // terminal vào
    IReadOnlyList<ToolOutput> Outputs { get; }              // terminal ra
    void Run();                                             // đọc .Value vào, ghi .Value ra
}
```

`ImageProcessorTool` bọc mỗi `IImageProcessor` thành `IVisionTool` và tự thêm terminal
"Ảnh vào" / "Ảnh ra". `PipelineRunner` chạy job từ trên xuống, với mỗi tool:
chép giá trị theo dây nối (`PullFromSource`) → cấp ảnh của tool trước nếu "Ảnh vào"
chưa nối → gọi `Run()`. Ảnh trung gian được giữ tới hết lần chạy (tool sau còn có thể
cần) rồi Dispose hết, chỉ giữ lại ảnh cuối.

Ba bước để thêm thuật toán mới:

1. Tạo class trong `MVATool.Core/Processors/` implement `IImageProcessor`
   (hoặc trong `MVATool.Core/Tools/` implement `IVisionTool` nếu không xử lý ảnh).
2. Khai báo tham số bằng `NumberParameter` / `BoolParameter` / `ChoiceParameter`
   (UI tự sinh Slider / CheckBox / ComboBox), dùng `InputPort<T>` cho dữ liệu chỉ nhận
   qua dây; kết quả phụ khai báo bằng `ToolOutput<T>` và ghi `.Value` trong
   `Process` / `Run` (xem `MeanBrightnessProcessor`, `FindContoursProcessor`, `MaskProcessor`).
3. Thêm **1 dòng** `Describe(() => new XxxProcessor())` vào `ProcessorCatalog`.

Không phải sửa ViewModel, không phải sửa XAML. Id ghi trong file job là **tên class**,
nên đổi tên hiển thị tiếng Việt thoải mái mà file job cũ vẫn mở được.

### Định dạng file job (`.mvajob`)

```json
{
  "app": "MVAVision", "version": 1,
  "tools": [
    { "type": "FindContoursProcessor", "name": "Tìm contour", "enabled": true,
      "values": { "Diện tích min": 50 },
      "links":  {} },
    { "type": "CompareTool", "name": "So sánh (OK/NG)", "enabled": true,
      "values": { "Phép so sánh": "=", "Ngưỡng": 3 },
      "links":  { "Giá trị": { "tool": 0, "output": "Số contour" } } }
  ]
}
```

- `type` = Id trong `ProcessorCatalog`; `name` chỉ để người đọc dễ hiểu.
- `links`: terminal vào → (chỉ số tool nguồn, tên terminal ra); `tool: -1` là "Ảnh gốc".
- Tham số khớp theo **tên** — thêm/bớt tham số ở phiên bản sau, file cũ vẫn mở được.

## Bài tập (từ dễ đến khó)

1. **Đếm blob** — hoàn thành `BlobCountProcessor` (đã có gợi ý từng bước trong file):
   threshold → `FindContours` → lọc theo diện tích → `DrawContours` + `PutText`;
   nâng cao: thêm terminal ra `ToolOutput<double>("Số blob")` để nối sang So sánh.
2. **Tool đo mới có terminal ra** — đo diện tích / chu vi contour lớn nhất
   (nhận `InputPort<Point[][]>` "Contours", ra 2 số), rồi dùng So sánh để phán OK/NG.
3. **Tool số học** — `IVisionTool` cộng/trừ/nhân/chia 2 số (`ChoiceParameter` chọn phép),
   ví dụ tính tỉ lệ Rộng/Cao từ Bounding Box.
4. **Preview từng tool** — click vào một tool trong job thì panel "Kết quả"
   hiển thị ảnh SAU tool đó (gợi ý: chạy job tới tool được chọn rồi dừng).
5. **Hủy giữa chừng khi chạy 1 ảnh** — job dài + ảnh lớn: thêm `CancellationToken` vào
   `PipelineRunner.Run`, nút Chạy biến thành nút Hủy khi `IsBusy`.
6. **Đồng bộ 2 viewer** — thêm `DependencyProperty` kiểu `Matrix` (TwoWay) vào
   `ImageViewer`, bind cả 2 viewer vào cùng một property của ViewModel →
   pan/zoom bên này, bên kia đi theo (rất tiện khi so sánh trước/sau).
7. **Kính lúp** — khi giữ `Ctrl`, hiện ô phóng to 8× vùng quanh con trỏ kèm lưới
   giá trị pixel (gợi ý: thêm 1 lớp overlay nữa vào `ImageViewer`).
8. **Undo / Redo** — stack các thay đổi của ảnh gốc (cắt/xoay/lật) và của job
   (thêm/xóa tool, nối/gỡ dây).
9. **Thống kê batch** — khi chạy cả thư mục, thu thập thời gian và kết quả OK/NG từng ảnh
   rồi hiện báo cáo: min / max / trung bình, tỉ lệ OK, ảnh nào chậm nhất
   (dữ liệu đã có sẵn trong vòng lặp `RunFolderAsync`).
10. **Chạy batch không chặn UI hoàn toàn** — bỏ `Task.Delay` "cho mắt nhìn", thay bằng
    tốc độ tối đa + ảnh hiển thị cập nhật mỗi N ảnh; so sánh thời gian tổng.
11. **Job file v2** — thêm mục mới vào `.mvajob` (vd: ghi chú cho từng tool), tăng
    `Version` và đảm bảo file v1 cũ vẫn mở được (viết test trong `JobFileTests`).
12. **Viết thêm test** — test `CannyEdgeProcessor` cho ra ảnh 1 kênh; test tắt tool nguồn
    trong khi tool sau đang nối dây vào nó thì job báo lỗi rõ ràng.

## Ghi chú cho người hướng dẫn

- Trình tự dạy gợi ý: `GrayscaleProcessor` (interface + catalog) → `GaussianBlurProcessor`
  (parameter + binding) → `MainWindowViewModel` (command, CanExecute, async) →
  `MainWindow.xaml` (DataTemplate, `RelativeSource` binding) → `App.xaml.cs` (DI) → Tests →
  `IVisionTool` / `ToolOutput` / `PipelineRunner` (terminal & dây nối) → `CompareTool`
  (tool không xử lý ảnh) → `JobSerializer` (lưu/mở job, tương thích phiên bản).
- Dây nối chỉ đi tới tool **đứng trước**, nên chạy job một lượt từ trên xuống là đủ và
  không bao giờ có vòng lặp — đáng giải thích vì sao không cần sắp xếp topo.
- `ImageService.ToBitmap` dùng `BitmapSource.Create` đọc thẳng buffer của Mat
  (không encode PNG trung gian) — layout pixel Gray8/BGR24/BGRA32 của OpenCV trùng
  khớp `PixelFormats` của WPF, đáng để giải thích cho học viên về stride.
- Lưu ý SDK: khi bật `UseWPF`, implicit usings **không còn `System.IO`** —
  các file dùng `Path`/`Directory`/`DriveInfo` phải `using System.IO;` tường minh.
- Khi học viên nắm vững project này, đọc code VisionPlatform sẽ dễ hơn nhiều.
