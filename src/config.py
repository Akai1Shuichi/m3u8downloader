import json
import os
import sys
from dataclasses import dataclass, asdict
from pathlib import Path

DEFAULT_HEADERS = (
    "accept: */*\n"
    "accept-encoding: gzip, deflate, br, zstd\n"
    "accept-language: en-GB,en-US;q=0.9,en;q=0.8,vi;q=0.7\n"
    "origin: https://goatembed.com\n"
    "referer: https://goatembed.com/\n"
    'sec-ch-ua: "Chromium";v="140", "Not=A?Brand";v="24", "Google Chrome";v="140"\n'
    "sec-ch-ua-mobile: ?0\n"
    'sec-ch-ua-platform: "Windows"\n'
    "sec-fetch-dest: empty\n"
    "sec-fetch-mode: cors\n"
    "sec-fetch-site: cross-site\n"
    "user-agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36"
)


def get_default_download_dir() -> str:
    """Return standard user Downloads directory across OS."""
    downloads = Path.home() / "Downloads"
    if downloads.exists():
        return str(downloads)
    return str(Path.home())


def get_settings_path() -> Path:
    """Get cross-platform path for settings.json."""
    if sys.platform == "win32":
        base = os.environ.get("APPDATA")
        app_dir = Path(base) / "M3U8Downloader" if base else Path.home() / ".m3u8downloader"
    elif sys.platform == "darwin":
        app_dir = Path.home() / "Library" / "Application Support" / "M3U8Downloader"
    else:
        config_home = os.environ.get("XDG_CONFIG_HOME")
        app_dir = Path(config_home) / "M3U8Downloader" if config_home else Path.home() / ".config" / "M3U8Downloader"

    app_dir.mkdir(parents=True, exist_ok=True)
    return app_dir / "settings.json"


@dataclass
class Config:
    url: str = ""
    batch_urls: str = ""
    m3u8_text: str = ""
    m3u8_base_url: str = ""
    video_path: str = ""
    video_name: str = ""
    max_worker: int = 32
    headers: str = DEFAULT_HEADERS
    preferred_format: str = "mp4"

    def __post_init__(self):
        if not self.video_path:
            self.video_path = get_default_download_dir()


class ConfigService:
    def __init__(self, settings_path: Path | None = None):
        self.settings_path = settings_path or get_settings_path()

    def load_settings(self) -> Config:
        if not self.settings_path.exists():
            return Config()
        try:
            with open(self.settings_path, "r", encoding="utf-8") as f:
                data = json.load(f)
            return Config(**data)
        except Exception as ex:
            print(f"Lỗi khi đọc settings: {ex}")
            return Config()

    def save_settings(self, config: Config) -> None:
        try:
            with open(self.settings_path, "w", encoding="utf-8") as f:
                json.dump(asdict(config), f, indent=4, ensure_ascii=False)
        except Exception as ex:
            print(f"Lỗi khi lưu settings: {ex}")
