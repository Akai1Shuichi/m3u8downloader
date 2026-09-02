import re
from urllib.parse import urljoin, urlparse
import requests
from PyQt6.QtCore import QThread, pyqtSignal


def parse_headers(headers_text: str) -> dict[str, str]:
    """Parse HTTP headers from raw multiline string."""
    headers_dict = {}
    if not headers_text or not headers_text.strip():
        return {
            "accept": "*/*",
            "accept-language": "en-US,en;q=0.9,vi;q=0.8",
            "user-agent": (
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
                "AppleWebKit/537.36 (KHTML, like Gecko) "
                "Chrome/133.0.0.0 Safari/537.36"
            ),
        }

    for line in headers_text.splitlines():
        line = line.strip()
        if ":" in line and not line.startswith("#"):
            k, v = line.split(":", 1)
            k = k.strip()
            v = v.strip()
            if k and v:
                headers_dict[k] = v
    return headers_dict


class SizeCheckerThread(QThread):
    progress_signal = pyqtSignal(str)
    finished_signal = pyqtSignal(str)

    def __init__(self, target_url: str, headers_text: str = ""):
        super().__init__()
        self.target_url = target_url.strip()
        self.headers_text = headers_text
        self._is_cancelled = False

    def cancel(self):
        self._is_cancelled = True

    def run(self):
        if not self.target_url:
            self.finished_signal.emit("❌ Vui lòng nhập URL hợp lệ!")
            return

        self.progress_signal.emit(f"🔍 Đang kiểm tra kích thước của: {self.target_url}")
        headers = parse_headers(self.headers_text)

        try:
            session = requests.Session()
            session.headers.update(headers)

            if ".m3u8" in self.target_url.lower():
                self._check_m3u8_size(session)
            else:
                self._check_direct_file_size(session)
        except requests.Timeout:
            self.finished_signal.emit("⏱️ Timeout khi kiểm tra URL! Vui lòng thử lại.")
        except Exception as ex:
            self.finished_signal.emit(f"❌ Lỗi khi kiểm tra kích thước: {ex}")

    def _check_m3u8_size(self, session: requests.Session):
        self.progress_signal.emit("📋 Đang tải và phân tích M3U8 playlist...")
        res = session.get(self.target_url, timeout=15)
        res.raise_for_status()

        lines = res.text.splitlines()
        segment_urls: list[str] = []

        for line in lines:
            if self._is_cancelled:
                return
            line = line.strip()
            if line and not line.startswith("#"):
                abs_url = urljoin(self.target_url, line)
                segment_urls.append(abs_url)

        if not segment_urls:
            self.finished_signal.emit("❌ Không tìm thấy segment nào trong M3U8!")
            return

        self.progress_signal.emit(f"📊 Tìm thấy {len(segment_urls)} segments. Đang đo kích thước mẫu...")

        max_sample = 1 if len(segment_urls) == 1 else min(4, len(segment_urls))
        total_sample_bytes = 0
        samples_ok = 0

        for i in range(max_sample):
            if self._is_cancelled:
                return
            seg_url = segment_urls[i]
            try:
                head_res = session.head(seg_url, timeout=8, allow_redirects=True)
                cl = head_res.headers.get("Content-Length")
                if cl and cl.isdigit() and int(cl) > 0:
                    total_sample_bytes += int(cl)
                    samples_ok += 1
                else:
                    # Fallback small range GET if HEAD has no content-length
                    get_res = session.get(seg_url, headers={"Range": "bytes=0-1024"}, timeout=8)
                    cr = get_res.headers.get("Content-Range")
                    if cr and "/" in cr:
                        full_len = cr.split("/")[-1]
                        if full_len.isdigit():
                            total_sample_bytes += int(full_len)
                            samples_ok += 1
            except Exception:
                pass

        if samples_ok > 0:
            avg_seg_size = total_sample_bytes / samples_ok
            estimated_total_bytes = avg_seg_size * len(segment_urls)
            size_mb = estimated_total_bytes / (1024 * 1024)

            if size_mb >= 1024:
                size_str = f"{size_mb / 1024:.2f} GB"
            else:
                size_str = f"{size_mb:.2f} MB"

            result = f"📊 Ước tính dung lượng: {size_str} ({len(segment_urls)} segments, ~{avg_seg_size / 1024:.1f} KB/segment)"
            self.finished_signal.emit(result)
        else:
            self.finished_signal.emit(f"📊 Tìm thấy {len(segment_urls)} segments (Server không trả về Content-Length).")

    def _check_direct_file_size(self, session: requests.Session):
        res = session.head(self.target_url, timeout=15, allow_redirects=True)
        cl = res.headers.get("Content-Length")
        if cl and cl.isdigit():
            total_bytes = int(cl)
            size_mb = total_bytes / (1024 * 1024)
            if size_mb >= 1024:
                size_str = f"{size_mb / 1024:.2f} GB"
            else:
                size_str = f"{size_mb:.2f} MB"
            self.finished_signal.emit(f"📊 Kích thước file: {size_str}")
        else:
            self.finished_signal.emit("⚠️ Không xác định được dung lượng qua HEAD request.")
