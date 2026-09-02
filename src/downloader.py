import os
import re
import shutil
import subprocess
import sys
from datetime import datetime
from pathlib import Path
from typing import Optional
from urllib.parse import urljoin

from PyQt6.QtCore import QThread, pyqtSignal

from src.config import Config
from src.crypto import process_m3u8_data
from src.local_server import LocalHttpServer
from src.size_checker import parse_headers


def find_tool_executable(tool_name: str) -> Optional[str]:
    """Find executable path for yt-dlp or ffmpeg."""
    ext = ".exe" if sys.platform == "win32" else ""

    # 1. Check in local Tools/ directory
    base_dir = Path(getattr(sys, "_MEIPASS", Path(__file__).resolve().parent.parent))
    local_tool = base_dir / "Tools" / tool_name / f"{tool_name}{ext}"
    if local_tool.is_file():
        return str(local_tool)

    # 2. Check system PATH
    found = shutil.which(tool_name)
    if found:
        return found

    return None


def get_safe_filename(name: str) -> str:
    """Sanitize string for filename."""
    invalid_chars = r'[\\/*?:"<>|]'
    safe = re.sub(invalid_chars, "_", name.strip())
    return safe if safe else "video"


def normalize_m3u8_content(content: str, base_url: str) -> str:
    """Normalize relative segment URLs inside M3U8 using base_url."""
    if not content or not base_url or not base_url.strip():
        return content

    base_url = base_url.strip()
    if not base_url.endswith("/") and "." not in base_url.split("/")[-1]:
        base_url += "/"

    lines = content.splitlines()
    output_lines = []
    for line in lines:
        trimmed = line.strip()
        if not trimmed or trimmed.startswith("#"):
            output_lines.append(trimmed)
            continue

        if trimmed.startswith("http://") or trimmed.startswith("https://"):
            output_lines.append(trimmed)
        else:
            output_lines.append(urljoin(base_url, trimmed))

    return "\n".join(output_lines)


class DownloadTask:
    def __init__(self, input_source: str, custom_name: str = "", is_raw_m3u8: bool = False):
        self.input_source = input_source.strip()
        self.custom_name = custom_name.strip()
        self.is_raw_m3u8 = is_raw_m3u8


class DownloaderThread(QThread):
    log_signal = pyqtSignal(str)
    progress_signal = pyqtSignal(str)
    finished_signal = pyqtSignal(bool, str)

    def __init__(self, tasks: list[DownloadTask], config: Config):
        super().__init__()
        self.tasks = tasks
        self.config = config
        self._is_cancelled = False
        self._current_process: Optional[subprocess.Popen] = None
        self._http_server: Optional[LocalHttpServer] = None
        self._last_safe_name = ""

    def cancel(self):
        """Cancel the download and terminate subprocess."""
        self._is_cancelled = True
        if self._current_process:
            try:
                self._current_process.kill()
            except Exception:
                pass

        if self._http_server:
            try:
                self._http_server.stop()
            except Exception:
                pass
            self._http_server = None

        self._cleanup_temp_files()

    def run(self):
        total_items = len(self.tasks)
        if total_items == 0:
            self.finished_signal.emit(False, "❌ Không có URL hoặc nội dung nào để tải!")
            return

        # Locate yt-dlp binary or check if python yt_dlp module is available
        yt_dlp_bin = find_tool_executable("yt-dlp")
        using_python_module = False
        if not yt_dlp_bin:
            try:
                import yt_dlp  # noqa
                using_python_module = True
            except ImportError:
                self.finished_signal.emit(
                    False,
                    "❌ Không tìm thấy yt-dlp! Vui lòng cài đặt qua: pip install yt-dlp"
                )
                return

        ffmpeg_bin = find_tool_executable("ffmpeg")

        success_count = 0
        error_count = 0

        for index, task in enumerate(self.tasks, start=1):
            if self._is_cancelled:
                break

            prefix_tag = f"[{index}/{total_items}]" if total_items > 1 else ""
            self.log_signal.emit(f"\n🚀 {prefix_tag} Bắt đầu xử lý...")

            try:
                ok = self._download_single_item(
                    task,
                    index,
                    total_items,
                    yt_dlp_bin,
                    using_python_module,
                    ffmpeg_bin
                )
                if ok:
                    success_count += 1
                else:
                    error_count += 1
            except Exception as ex:
                error_count += 1
                self.log_signal.emit(f"❌ {prefix_tag} Lỗi: {ex}")

        if self._is_cancelled:
            self.finished_signal.emit(False, "⏸️ Đã dừng tải và dọn dẹp file tạm.")
        else:
            summary = f"🎉 Hoàn thành: {success_count}/{total_items} thành công"
            if error_count > 0:
                summary += f", {error_count} lỗi."
            else:
                summary += "!"
            self.finished_signal.emit(True, summary)

    def _download_single_item(
        self,
        task: DownloadTask,
        index: int,
        total_items: int,
        yt_dlp_bin: Optional[str],
        using_python_module: bool,
        ffmpeg_bin: Optional[str],
    ) -> bool:
        prefix_tag = f"[{index}/{total_items}]" if total_items > 1 else ""
        save_dir = Path(self.config.video_path)
        save_dir.mkdir(parents=True, exist_ok=True)

        # File name resolving
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        prefix = "video" if self.config.preferred_format in ("mp4", "mkv") else "audio"

        if task.custom_name:
            base_name = get_safe_filename(task.custom_name)
        elif self.config.video_name:
            if total_items > 1:
                base_name = f"{get_safe_filename(self.config.video_name)}_{index:02d}"
            else:
                base_name = get_safe_filename(self.config.video_name)
        else:
            if total_items > 1:
                base_name = f"{prefix}_{timestamp}_{index:02d}"
            else:
                base_name = f"{prefix}_{timestamp}"

        self._last_safe_name = base_name
        output_template = str(save_dir / f"{base_name}.%(ext)s")

        input_url = task.input_source

        # Handle raw/encrypted m3u8 mode
        if task.is_raw_m3u8:
            self.log_signal.emit(f"🔓 {prefix_tag} Đang xử lý / giải mã M3U8 content...")
            processed_content = process_m3u8_data(task.input_source)
            processed_content = normalize_m3u8_content(processed_content, self.config.m3u8_base_url)

            self._http_server = LocalHttpServer(processed_content)
            self._http_server.start()
            input_url = self._http_server.playlist_url
            self.log_signal.emit(f"🌐 {prefix_tag} Server nội bộ phát tại: {input_url}")

        fmt = (self.config.preferred_format or "mp4").lower()
        workers = max(1, int(self.config.max_worker or 16))
        headers_dict = parse_headers(self.config.headers)

        self.log_signal.emit(f"⚡ {prefix_tag} Đang tải về định dạng {fmt.upper()} (luồng: {workers})...")

        # -------------------------------------------------------------
        # Mode 1: Using yt-dlp binary (via Subprocess)
        # -------------------------------------------------------------
        if yt_dlp_bin and not using_python_module:
            cmd = [yt_dlp_bin, input_url, "-o", output_template]

            if fmt == "mp3":
                cmd.extend(["--format", "bestaudio/best", "--extract-audio", "--audio-format", "mp3", "--audio-quality", "0"])
            elif fmt == "m4a":
                cmd.extend(["--format", "bestaudio/best", "--extract-audio", "--audio-format", "m4a", "--audio-quality", "0"])
            elif fmt == "mkv":
                cmd.extend(["--format", "bestvideo+bestaudio/best", "--merge-output-format", "mkv"])
            else:
                cmd.extend(["--format", "best[ext=mp4]/best", "--merge-output-format", "mp4"])

            if ffmpeg_bin:
                cmd.extend(["--ffmpeg-location", ffmpeg_bin])

            cmd.extend([
                "--concurrent-fragments", str(workers),
                "--fragment-retries", "10",
                "--retries", "10",
                "--no-check-certificate",
                "--ignore-errors",
                "--no-continue",
                "--force-overwrites",
            ])

            for k, v in headers_dict.items():
                cmd.extend(["--add-header", f"{k}:{v}"])

            creationflags = subprocess.CREATE_NO_WINDOW if sys.platform == "win32" else 0

            try:
                self._current_process = subprocess.Popen(
                    cmd,
                    stdout=subprocess.PIPE,
                    stderr=subprocess.STDOUT,
                    text=True,
                    encoding="utf-8",
                    errors="replace",
                    creationflags=creationflags,
                    bufsize=1,
                )

                if self._current_process.stdout:
                    for raw_line in self._current_process.stdout:
                        if self._is_cancelled:
                            break
                        line = raw_line.strip()
                        if not line or any(x in line for x in ["[debug]", "Loaded", "Python version"]):
                            continue

                        if "[download]" in line or "ETA" in line or "%" in line:
                            self.progress_signal.emit(f"{prefix_tag} {line}")
                        else:
                            self.log_signal.emit(f"{prefix_tag} {line}")

                self._current_process.wait()
                returncode = self._current_process.returncode

                if self._is_cancelled:
                    return False

                if returncode == 0:
                    self.log_signal.emit(f"✅ {prefix_tag} Tải hoàn tất: {base_name}.{fmt}")
                    return True
                else:
                    self.log_signal.emit(f"❌ {prefix_tag} yt-dlp kết thúc với mã lỗi {returncode}")
                    return False
            finally:
                if self._http_server:
                    self._http_server.stop()
                    self._http_server = None
                self._current_process = None

        # -------------------------------------------------------------
        # Mode 2: Using Python yt_dlp library natively
        # -------------------------------------------------------------
        else:
            import yt_dlp

            class SignalLogger:
                def __init__(self, log_signal, prefix):
                    self.log_signal = log_signal
                    self.prefix = prefix

                def debug(self, msg):
                    if not any(x in msg for x in ["[debug]", "Loaded"]):
                        self.log_signal.emit(f"{self.prefix} {msg}")

                def info(self, msg):
                    self.log_signal.emit(f"{self.prefix} {msg}")

                def warning(self, msg):
                    self.log_signal.emit(f"{self.prefix} ⚠️ {msg}")

                def error(self, msg):
                    self.log_signal.emit(f"{self.prefix} ❌ {msg}")

            def progress_hook(d):
                if self._is_cancelled:
                    raise Exception("Tải bị dừng bởi người dùng.")
                if d.get("status") == "downloading":
                    pct = d.get("_percent_str", "").strip()
                    speed = d.get("_speed_str", "").strip()
                    eta = d.get("_eta_str", "").strip()
                    self.progress_signal.emit(f"{prefix_tag} [download] {pct} at {speed} ETA {eta}")

            ydl_opts = {
                "outtmpl": output_template,
                "concurrent_fragment_downloads": workers,
                "fragment_retries": 10,
                "retries": 10,
                "nocheckcertificate": True,
                "ignoreerrors": True,
                "overwrites": True,
                "http_headers": headers_dict,
                "logger": SignalLogger(self.log_signal, prefix_tag),
                "progress_hooks": [progress_hook],
            }

            if ffmpeg_bin:
                ydl_opts["ffmpeg_location"] = ffmpeg_bin

            if fmt == "mp3":
                ydl_opts["format"] = "bestaudio/best"
                ydl_opts["postprocessors"] = [{
                    "key": "FFmpegExtractAudio",
                    "preferredcodec": "mp3",
                    "preferredquality": "0",
                }]
            elif fmt == "m4a":
                ydl_opts["format"] = "bestaudio/best"
                ydl_opts["postprocessors"] = [{
                    "key": "FFmpegExtractAudio",
                    "preferredcodec": "m4a",
                    "preferredquality": "0",
                }]
            elif fmt == "mkv":
                ydl_opts["format"] = "bestvideo+bestaudio/best"
                ydl_opts["merge_output_format"] = "mkv"
            else:
                ydl_opts["format"] = "best[ext=mp4]/best"
                ydl_opts["merge_output_format"] = "mp4"

            try:
                with yt_dlp.YoutubeDL(ydl_opts) as ydl:
                    ret = ydl.download([input_url])
                    if ret == 0:
                        self.log_signal.emit(f"✅ {prefix_tag} Tải hoàn tất: {base_name}.{fmt}")
                        return True
                    return False
            except Exception as ex:
                if not self._is_cancelled:
                    self.log_signal.emit(f"❌ {prefix_tag} Lỗi: {ex}")
                return False
            finally:
                if self._http_server:
                    self._http_server.stop()
                    self._http_server = None

    def _cleanup_temp_files(self):
        """Remove leftover fragments / part files."""
        try:
            video_dir = Path(self.config.video_path)
            if not video_dir.exists():
                return

            patterns = ["*.part", "*.part-Frag*", "*.ytdl", "*.temp"]
            for pat in patterns:
                for f in video_dir.glob(pat):
                    try:
                        f.unlink()
                    except Exception:
                        pass
        except Exception:
            pass
