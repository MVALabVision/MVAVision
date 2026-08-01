# MVAVision — Machine Vision Suite

Project luyện tập **Avalonia + MVVM**, đồng thời là nền để **tích hợp thuật toán
machine vision (OpenCV)**. Kiến trúc là bản rút gọn của VisionPlatform: cùng stack
(Avalonia, CommunityToolkit.Mvvm, OpenCvSharp), cùng tư tưởng tách tầng.

Giao diện theo phong cách **tool công nghiệp**: logo MVA, đèn LED trạng thái
(SẴN SÀNG / ĐANG XỬ LÝ / BATCH), đồng hồ hệ thống, và khối **giám sát tài nguyên**
(CPU / RAM / ổ đĩa / bộ nhớ app) cập nhật mỗi giây ngay trên giao diện.

## Ứng dụng làm được gì

- Mở ảnh (nút, `Ctrl+O`, hoặc **kéo thả file vào cửa sổ**).
- **Mở cả thư mục ảnh**: danh sách file hiện ở panel trái, click file nào xem file đó,
  điều hướng ◀ ▶, và **▶▶ chạy pipeline lần lượt với từng ảnh** (dừng được giữa chừng
  bằng nút ⏹); tùy chọn lưu kết quả từng ảnh vào thư mục con `ket-qua`.
- **Zoom / pan trên ảnh**: ảnh mở lên hiện **trọn vẹn, không phóng to quá 100%** —
  ảnh nhỏ giữ đúng kích thước gốc, ảnh to hơn khung được thu nhỏ vừa đủ để không
  bị cắt; lăn chuột để zoom (quanh vị trí con trỏ), kéo chuột để di chuyển,
  double-click hoặc nút "Vừa khung" / "1:1"; zoom to thấy rõ từng pixel
  (nearest-neighbor, không làm nhòe). Chạy lại pipeline với ảnh cùng kích thước
  thì **giữ nguyên** góc nhìn đang xem (tiện khi kéo slider so sánh kết quả).
- **Đọc pixel dưới con trỏ**: tọa độ (x, y) + giá trị màu R-G-B (hoặc mức xám)
  hiện trên thanh trạng thái — di chuột trên ảnh gốc lẫn ảnh kết quả đều được.
- **Thao tác với ảnh gốc**: ✂ cắt (kéo chuột chọn vùng), ⟲⟳ xoay 90°, ⇆⇅ lật,
  ↩ khôi phục về ảnh như lúc mở file.
- Xây **pipeline xử lý nhiều bước**: thêm / xóa / đổi thứ tự / bật-tắt từng bước.
- Mỗi bước có **tham số riêng** (Slider / CheckBox) — UI tự sinh theo kiểu tham số.
- **Auto-run**: kéo slider hay cắt/xoay ảnh là kết quả cập nhật ngay (xử lý chạy nền, UI không đơ).
- Xem **ảnh gốc / kết quả cạnh nhau**, thời gian chạy từng bước, lịch sử.
- Lưu kết quả (`Ctrl+S`), chạy tay bằng `F5`, đổi theme sáng / tối.
- **Giám sát hệ thống**: CPU toàn máy (`GetSystemTimes`), RAM đã dùng / tổng
  (`GlobalMemoryStatusEx`), dung lượng trống từng ổ đĩa (`DriveInfo`), working set
  của app — panel trái + bản rút gọn trên thanh trạng thái, cập nhật mỗi giây.

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
│   ├── IImageProcessor.cs         #    điểm mở rộng cho mọi thuật toán vision
│   ├── ProcessorCatalog.cs        #    danh mục thuật toán (nơi duy nhất phải sửa khi thêm mới)
│   ├── Parameters/                #    NumberParameter, BoolParameter — bind thẳng lên UI
│   └── Processors/                #    Grayscale, Invert, GaussianBlur, Threshold,
│                                  #    CannyEdge + BlobCount (bài tập)
│
├── MVATool/                       # APP AVALONIA
│   ├── App.axaml.cs               #    composition root — DI container thật (MS.Extensions.DI)
│   ├── Models/LogEntry.cs         #    MODEL: dữ liệu thuần
│   ├── ViewModels/                #    VIEWMODEL: MainWindowViewModel + PipelineStepViewModel
│   ├── Views/MainWindow.axaml     #    VIEW: chỉ XAML + binding; code-behind chỉ có
│   │                              #    drag-drop & đổi theme (ngoại lệ hợp lệ, có giải thích)
│   ├── Views/Controls/            #    CUSTOM CONTROL: ImageViewer — zoom/pan/crop/đọc pixel,
│   │                              #    giao tiếp với ViewModel qua StyledProperty + ICommand
│   └── Services/                  #    SERVICE: FilePicker (dialog), ImageService (Mat↔Bitmap),
│                                  #    SystemInfoService (CPU/RAM/ổ đĩa — P/Invoke Windows API)
│
└── MVATool.Tests/                 # UNIT TEST — chứng minh lợi ích lớn nhất của MVVM:
    ├── FakeServices.cs            #    service giả thay dialog/file thật
    ├── ProcessorTests.cs          #    test thuật toán không cần mở app
    └── MainWindowViewModelTests.cs#    test hành vi UI không cần cửa sổ nào
```

**Quy tắc vàng:** `MVATool.Core` không được tham chiếu Avalonia. ViewModel không được
`using Avalonia.Controls`. View không chứa logic nghiệp vụ.

## Các khái niệm MVVM xuất hiện ở đâu

| Khái niệm | Xem ở |
|---|---|
| `[ObservableProperty]` (INotifyPropertyChanged) | `MainWindowViewModel` — `ResultImage`, `IsBusy`... |
| `[RelayCommand]` + `CanExecute` (nút tự khóa/mở) | `RunPipelineAsync` + `CanRunPipeline` |
| `[NotifyCanExecuteChangedFor]` | property `IsBusy` — đổi là 3 nút tự cập nhật |
| `[NotifyPropertyChangedFor]` (property phụ thuộc) | `NumberParameter.Value` → `DisplayText` |
| Command có THAM SỐ (`CommandParameter`) | nút ↑ ↓ ✕ của từng bước pipeline |
| Binding thoát khỏi DataTemplate (`$parent[Window]`) | `MainWindow.axaml`, phần step card |
| DataTemplate **ngầm định theo kiểu dữ liệu** | `Window.DataTemplates` — Slider cho NumberParameter |
| `ObservableCollection` | `Pipeline`, `History` |
| Converter có sẵn (`ObjectConverters.IsNull`) | dòng gợi ý khi chưa có ảnh |
| Binding phủ định (`!Pipeline.Count`) | dòng gợi ý khi pipeline rỗng |
| `KeyBinding` gọi command | `Ctrl+O`, `Ctrl+S`, `F5` |
| Async command + `IsBusy` + `Task.Run` | `RunPipelineAsync` — xử lý nặng không đơ UI |
| **Hủy tác vụ dài**: `CancellationToken` + `IncludeCancelCommand` | `RunFolderAsync` — nút ⏹ Dừng bind vào `RunFolderCancelCommand` |
| Selection-driven: bind `SelectedItem`, phản ứng trong hook | `OnSelectedImageFileChanged` — chọn file là load, không cần Click event |
| Coalescing (dồn yêu cầu chạy lại) | `TriggerAutoRun` + `_rerunRequested` |
| Partial method hook của toolkit | `OnIsAutoRunEnabledChanged`, `OnIsEnabledChanged` |
| DI container + constructor injection | `App.axaml.cs` (composition root) |
| Code-behind "hợp lệ" (drag-drop, theme) | `MainWindow.axaml.cs` — đọc comment đầu file |
| **Custom control** với `StyledProperty` | `Views/Controls/ImageViewer` — Source, IsCropEnabled |
| Control trả kết quả qua `ICommand` property | `ImageViewer` — `CropCommand`, `PixelHoverCommand` |
| Ma trận biến đổi 2D (zoom quanh 1 điểm, nghịch đảo để đổi hệ tọa độ) | `ImageViewer.axaml.cs` — `ZoomAround`, `ToPixel` |
| Child-ViewModel (tách mảng chức năng riêng) | `SystemStatusViewModel` — View bind `{Binding SystemStatus.CpuText}` |
| Property TÍNH TOÁN + `[NotifyPropertyChangedFor]` | `SystemStateText` — đèn LED đổi theo `IsBusy`/`IsBatchRunning` |
| Style class động (`Classes.xxx="{Binding}"`) | đèn LED `.busy`/`.batch`, thanh đo `.hot` khi quá tải |
| ThemeDictionaries (bảng màu đổi theo theme) | `App.axaml` — bộ màu công nghiệp `MvaXxx` |
| Unit test ViewModel bằng fake service | `MVATool.Tests` |

## Tích hợp thuật toán machine vision như thế nào?

Toàn bộ thuật toán đi qua một interface duy nhất trong `MVATool.Core`:

```csharp
public interface IImageProcessor
{
    string Name { get; }
    IReadOnlyList<ProcessorParameter> Parameters { get; }  // tham số hiện lên UI
    Mat Process(Mat input);                                 // nhận Mat, trả Mat MỚI
}
```

Ba bước để thêm thuật toán mới:

1. Tạo class trong `MVATool.Core/Processors/` implement `IImageProcessor`.
2. Khai báo tham số bằng `NumberParameter` / `BoolParameter` — UI tự sinh Slider/CheckBox.
3. Thêm **1 dòng** vào `ProcessorCatalog`.

Không phải sửa ViewModel, không phải sửa XAML.

## Bài tập (từ dễ đến khó)

1. **Đếm blob** — hoàn thành `BlobCountProcessor` (đã có gợi ý từng bước trong file):
   threshold → `FindContours` → lọc theo diện tích → `DrawContours` + `PutText`.
2. **Processor mới có tham số** — viết `RotateProcessor` (góc xoay là `NumberParameter`,
   dùng `Cv2.GetRotationMatrix2D` + `Cv2.WarpAffine`) hoặc `MorphologyProcessor`
   (erode/dilate với `BoolParameter` chọn chế độ).
3. **Tham số kiểu mới** — thêm `ChoiceParameter` (danh sách lựa chọn) + DataTemplate
   ComboBox cho nó; dùng cho `MorphologyProcessor` (Erode/Dilate/Open/Close).
4. **Preview từng bước** — click vào một bước trong pipeline thì panel "Kết quả"
   hiển thị ảnh SAU bước đó (gợi ý: chạy pipeline tới bước được chọn rồi dừng).
5. **Hủy giữa chừng** — pipeline dài + ảnh lớn: thêm `CancellationToken` vào `Process`,
   nút Chạy biến thành nút Hủy khi `IsBusy` (xem `AsyncRelayCommand` cancel support).
6. **Lưu / mở pipeline** — serialize danh sách bước + giá trị tham số ra JSON
   (`System.Text.Json`), mở lại thành pipeline y hệt.
7. **Đồng bộ 2 viewer** — thêm `StyledProperty<Matrix> ViewMatrix` (TwoWay) vào
   `ImageViewer`, bind cả 2 viewer vào cùng một property của ViewModel →
   pan/zoom bên này, bên kia đi theo (rất tiện khi so sánh trước/sau).
8. **Kính lúp** — khi giữ `Ctrl`, hiện ô phóng to 8× vùng quanh con trỏ kèm lưới
   giá trị pixel (gợi ý: thêm 1 lớp overlay nữa vào `ImageViewer`).
9. **Undo / Redo** — stack các thay đổi của ảnh gốc (cắt/xoay/lật) và pipeline.
10. **Thống kê batch** — khi chạy cả thư mục, thu thập thời gian từng ảnh rồi hiện
    báo cáo: min / max / trung bình, ảnh nào chậm nhất (dữ liệu đã có sẵn trong vòng
    lặp `RunFolderAsync`).
11. **Chạy batch không chặn UI hoàn toàn** — bỏ `Task.Delay` "cho mắt nhìn", thay bằng
    tốc độ tối đa + ảnh hiển thị cập nhật mỗi N ảnh; so sánh thời gian tổng.
12. **Viết thêm test** — test `CannyEdgeProcessor` cho ra ảnh 1 kênh; test hủy giữa chừng:
    gọi `RunFolderCancelCommand` sau ảnh đầu tiên rồi kiểm tra `History` ghi đúng số ảnh đã xử lý.

## Ghi chú cho người hướng dẫn

- Trình tự dạy gợi ý: `GrayscaleProcessor` (interface + catalog) → `GaussianBlurProcessor`
  (parameter + binding) → `MainWindowViewModel` (command, CanExecute, async) →
  `MainWindow.axaml` (DataTemplate, `$parent` binding) → `App.axaml.cs` (DI) → Tests.
- `ImageService.ToBitmap` dùng `WriteableBitmap` copy pixel trực tiếp (unsafe) —
  nhanh hơn encode PNG trung gian rất nhiều, đáng để giải thích cho học viên về stride.
- Khi học viên nắm vững project này, đọc code VisionPlatform sẽ dễ hơn nhiều.
