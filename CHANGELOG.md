# Nhật ký thay đổi (Changelog)

Tất cả các thay đổi đáng chú ý đối với dự án này sẽ được ghi lại trong tệp này.

Định dạng dựa trên [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
và dự án này tuân thủ [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.4.0] - 2026-09-02

### Đã thêm (Added)
- **Tải hàng loạt URL (Batch Download):** Cho phép nhập danh sách nhiều link (mỗi dòng một URL) để tải toàn bộ bộ phim hoặc danh sách video tự động.
- Hỗ trợ định dạng `URL | Tên file` trên từng dòng để đặt tên riêng cho từng tập/video.
- Nếu đặt "Tên file" chung, phần mềm sẽ tự động đánh số thứ tự (ví dụ: `TenPhim_01`, `TenPhim_02`,...).
- Tự động chuyển tiếp tải từng video trong danh sách, theo dõi tiến độ chi tiết `[1/N]`, báo cáo tổng kết số lượng thành công/thất bại sau khi hoàn tất.
- Hỗ trợ nút Dừng/Pause để hủy nhanh toàn bộ hàng đợi đang tải.
- Tự động cấp phát cổng HTTP cục bộ tránh xung đột khi tải liên tục.

## [1.3.0] - 2026-09-01

### Đã sửa lỗi (Fixed)
- Sửa lỗi nghiêm trọng: nhấn **Tải** lần 2 với URL mới nhưng vẫn tải lại file video cũ (do yt-dlp resume/skip file trùng tên). Thêm cờ `--no-continue` và `--force-overwrites` để bắt buộc tải mới hoàn toàn.

### Đã thêm (Added)
- Thêm ô nhập **Tên file** ngay dưới phần thư mục lưu, cho phép người dùng đặt tên file video trước khi tải.
- Tên file tùy chỉnh được lưu lại vào config, không cần nhập lại mỗi lần.
- Nếu để trống ô tên file, ứng dụng tự đặt tên theo thời gian (`video_YYYYMMDD_HHmmss`).
- Tự động loại bỏ các ký tự không hợp lệ trong tên file.

### Đã thay đổi (Changed)
- Cải thiện giao diện phần **Thư mục lưu**: thêm nhãn rõ ràng "Thư mục:" và "Tên file:".
- Cập nhật phiên bản ứng dụng lên 1.3.0.

## [1.2.0] - 2026-05-05

### Đã sửa lỗi (Fixed)
- Lỗi tương thích MP4 trên các thiết bị di động bằng cách ép chuẩn màu `yuv420p` và codec H.264/AAC.
- Thiếu thông tin metadata của video (độ dài, độ phân giải, tốc độ bit) trong Windows Explorer.
- Lỗi khởi động chậm/buffering trên di động bằng cách bật tính năng `faststart` (di chuyển moov atom lên đầu file).

### Đã thêm (Added)
- Tích hợp FFmpeg gốc thông qua thư mục `Tools/ffmpeg/`.
- Tự động sửa chữa các phân đoạn video bị lỗi bằng `--fixup force`.
- Tự động bảo tồn metadata bằng cách sử dụng `--add-metadata`.

## [1.1.0] - 2025-10-07

### Đã thêm (Added)
- Hỗ trợ tải phim từ Animevietsub (tự động trích xuất link, xử lý token, vượt qua anti-bot).
- Thêm tính năng nhập nội dung M3U8 thô để tải trực tiếp.
- Cài đặt nâng cao: cấu hình kích thước lô chuyển đổi M3U8 (số lượng luồng chuyển đổi song song).
- Những cải tiến nhỏ và cập nhật các thư viện phụ thuộc.

### Đã thay đổi (Changed)
- Cập nhật phiên bản ứng dụng lên 1.1.0.

## [1.0.0] - 2024-01-XX

### Đã thêm (Added)
- Bản phát hành đầu tiên của M3U8 Downloader.
- Giao diện người dùng WPF hiện đại với khung WPF-UI.
- Tính năng tải luồng video M3U8.
- Tích hợp với yt-dlp để tăng cường khả năng tương thích.
- File thực thi độc lập (không yêu cầu cài đặt thêm phụ thuộc).
- Hỗ trợ tùy chỉnh tiêu đề (headers) và đường dẫn tải về.
- Theo dõi tiến độ tải về trong thời gian thực.
- Xử lý lỗi và phản hồi cho người dùng.
