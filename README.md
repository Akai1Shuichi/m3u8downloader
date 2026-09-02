# M3U8 Downloader (Python PyQt6)

Một ứng dụng desktop đa nền tảng hiện đại (**Windows**, **macOS**, **Ubuntu/Linux**) để tải các luồng video và playlist M3U8 với giao diện PyQt6 trực quan, hỗ trợ Dark Mode.

<p>
  <a href="https://donate-trtoan.vercel.app/">
    <img src="https://img.shields.io/badge/Support-Donate-EA4AAA?style=for-the-badge&logo=githubsponsors&logoColor=red" alt="Donate">
  </a>
</p>

## Tính năng nổi bật

- 🌐 **Đa nền tảng (Cross-Platform):** Chạy mượt mà trên Windows, macOS (Intel & Apple Silicon) và Ubuntu / Linux.
- 🎥 **Định dạng đa dạng:** Tải và chuyển đổi luồng M3U8 sang MP4, MKV, MP3, M4A/AAC.
- 📋 **3 Chế độ nhập liệu:**
  - **Nhập 1 URL:** Dán 1 link video / m3u8.
  - **Tải hàng loạt URL (Batch Download):** Nhập danh sách nhiều URL (mỗi dòng 1 URL, hỗ trợ cú pháp `URL | TênTùyChỉnh`), tự động đánh số thứ tự file `_01, _02,...`.
  - **Nội dung M3U8:** Dán trực tiếp nội dung playlist hoặc chuỗi AES-CBC đã mã hóa.
- 🔍 **Kiểm tra kích thước (Check Size):** Tự động phân tích playlist và ước tính dung lượng video trước khi tải.
- ⚡ **Tải siêu tốc:** Tùy chỉnh số luồng (`MaxWorker` / `concurrent-fragments`), hỗ trợ retry và chống drop stream.
- 📝 **Tùy biến Headers:** Thêm User-Agent, Referer, Cookie, Authorization dễ dàng.
- ⏹️ **Dừng tải & Dọn dẹp:** Dừng tải bất cứ lúc nào, tự động hủy tiến trình và xóa sạch file tạm (`.part`, `.ytdl`, `.temp`).
- 🤖 **CI/CD Tự động:** Tích hợp GitHub Actions Matrix tự động build và tạo release package cho cả 3 hệ điều hành.

---

## Cài đặt & Chạy từ Source Code (Development)

### 1. Yêu cầu hệ thống
- Python 3.10+
- FFmpeg (đã cài trong hệ thống hoặc đặt trong thư mục `Tools/ffmpeg/`)

### 2. Cài đặt thư viện
```bash
pip install -r requirements.txt
```

### 3. Khởi chạy ứng dụng
```bash
python main.py
```

---

## Đóng gói ứng dụng (Build Executable)

Để đóng gói ứng dụng thành file chạy độc lập (.exe trên Windows, .app trên macOS, binary trên Linux):

```bash
pip install pyinstaller
pyinstaller m3u8Downloader.spec --noconfirm
```

File sau khi build sẽ nằm trong thư mục `dist/M3U8Downloader/`.

---

## Tự động Build bằng GitHub Actions

Dự án đã tích hợp sẵn workflow `.github/workflows/release.yml`. Khi bạn tạo một tag mới (ví dụ: `v1.5.0`):
- GitHub Actions sẽ tự động build ứng dụng song song trên **Windows**, **macOS** và **Ubuntu**.
- Tự động nén và đính kèm các file `M3U8Downloader-windows-x64.zip`, `M3U8Downloader-macos.zip`, `M3U8Downloader-ubuntu-x64.tar.gz` vào phần GitHub Release.
