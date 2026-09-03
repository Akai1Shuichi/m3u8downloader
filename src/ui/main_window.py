import os
import sys
from pathlib import Path
from typing import Optional

from PyQt6.QtCore import Qt, QUrl
from PyQt6.QtGui import QDesktopServices, QIcon, QPixmap
from PyQt6.QtWidgets import (
    QButtonGroup,
    QComboBox,
    QFileDialog,
    QFrame,
    QGridLayout,
    QHBoxLayout,
    QLabel,
    QLineEdit,
    QMainWindow,
    QMessageBox,
    QPlainTextEdit,
    QPushButton,
    QRadioButton,
    QScrollArea,
    QSizePolicy,
    QSpinBox,
    QVBoxLayout,
    QWidget,
)

from src.config import Config, ConfigService
from src.downloader import DownloadTask, DownloaderThread
from src.size_checker import SizeCheckerThread
from src.ui.styles import MAIN_STYLESHEET


def get_resource_path(relative_path: str) -> str:
    """Get absolute path to resource, works for dev and for PyInstaller."""
    base_path = getattr(sys, "_MEIPASS", Path(__file__).resolve().parent.parent.parent)
    return str(Path(base_path) / relative_path)


class CollapsibleBox(QWidget):
    """Collapsible expander widget for advanced settings."""

    def __init__(self, title: str = "", parent: Optional[QWidget] = None):
        super().__init__(parent)
        self.toggle_button = QPushButton(f"▶ {title}")
        self.toggle_button.setStyleSheet(
            "QPushButton { text-align: left; background-color: #252525; border: 1px solid #383838; font-weight: bold; padding: 8px 12px; border-radius: 6px; } "
            "QPushButton:hover { background-color: #303030; }"
        )
        self.toggle_button.setCheckable(True)
        self.toggle_button.setChecked(False)
        self.toggle_button.clicked.connect(self.on_toggle)

        self.content_area = QWidget()
        self.content_area.setVisible(False)
        self.content_layout = QVBoxLayout(self.content_area)
        self.content_layout.setContentsMargins(10, 10, 10, 10)
        self.content_layout.setSpacing(10)

        layout = QVBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(4)
        layout.addWidget(self.toggle_button)
        layout.addWidget(self.content_area)

        self._title = title

    def on_toggle(self, checked: bool):
        arrow = "▼" if checked else "▶"
        self.toggle_button.setText(f"{arrow} {self._title}")
        self.content_area.setVisible(checked)

    def add_widget(self, widget: QWidget):
        self.content_layout.addWidget(widget)

    def add_layout(self, layout):
        self.content_layout.addLayout(layout)


class MainWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self.config_service = ConfigService()
        self.config: Config = self.config_service.load_settings()

        self.download_worker: Optional[DownloaderThread] = None
        self.size_worker: Optional[SizeCheckerThread] = None

        self._init_ui()
        self._load_config_to_ui()

    def _init_ui(self):
        self.setWindowTitle("MultiStream Downloader")
        self.resize(720, 780)
        self.setMinimumSize(600, 650)
        self.setStyleSheet(MAIN_STYLESHEET)

        # Set App Icon
        icon_path = get_resource_path("Resource/Image/app.ico")
        if os.path.exists(icon_path):
            self.setWindowIcon(QIcon(icon_path))

        # Central Scroll Area
        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        scroll.setFrameShape(QFrame.Shape.NoFrame)
        self.setCentralWidget(scroll)

        container = QWidget()
        scroll.setWidget(container)

        main_layout = QVBoxLayout(container)
        main_layout.setContentsMargins(20, 16, 20, 20)
        main_layout.setSpacing(14)

        # --- Section: Input Mode Selection ---
        mode_title = QLabel("Chọn cách nhập:")
        mode_title.setObjectName("SectionTitle")
        main_layout.addWidget(mode_title)

        mode_layout = QHBoxLayout()
        self.radio_url = QRadioButton("Nhập URL")
        self.radio_batch = QRadioButton("Tải hàng loạt URL")
        self.radio_text = QRadioButton("Nội dung m3u8")
        self.radio_url.setChecked(True)

        self.mode_group = QButtonGroup(self)
        self.mode_group.addButton(self.radio_url, 1)
        self.mode_group.addButton(self.radio_batch, 2)
        self.mode_group.addButton(self.radio_text, 3)
        self.mode_group.idToggled.connect(self._on_mode_changed)

        mode_layout.addWidget(self.radio_url)
        mode_layout.addWidget(self.radio_batch)
        mode_layout.addWidget(self.radio_text)
        mode_layout.addStretch()
        main_layout.addLayout(mode_layout)

        # --- Sub-view 1: Single URL ---
        self.panel_url = QWidget()
        panel_url_layout = QHBoxLayout(self.panel_url)
        panel_url_layout.setContentsMargins(0, 0, 0, 0)
        panel_url_layout.setSpacing(8)

        url_input_col = QVBoxLayout()
        url_lbl = QLabel("URL:")
        url_lbl.setStyleSheet("font-weight: 500;")
        self.input_url = QLineEdit()
        self.input_url.setPlaceholderText("https://...")
        url_input_col.addWidget(url_lbl)
        url_input_col.addWidget(self.input_url)
        panel_url_layout.addLayout(url_input_col, 1)

        self.btn_check_size_1 = QPushButton("Check size")
        self.btn_check_size_1.setObjectName("PrimaryBtn")
        self.btn_check_size_1.setFixedHeight(34)
        self.btn_check_size_1.clicked.connect(self._on_check_size)
        panel_url_layout.addWidget(self.btn_check_size_1, 0, Qt.AlignmentFlag.AlignBottom)

        main_layout.addWidget(self.panel_url)

        # --- Sub-view 2: Batch URLs ---
        self.panel_batch = QWidget()
        panel_batch_layout = QVBoxLayout(self.panel_batch)
        panel_batch_layout.setContentsMargins(0, 0, 0, 0)
        panel_batch_layout.setSpacing(6)

        batch_header = QHBoxLayout()
        batch_lbl = QLabel("Danh sách URL (mỗi dòng 1 URL):")
        batch_lbl.setStyleSheet("font-weight: 500;")
        self.btn_check_size_2 = QPushButton("Check size")
        self.btn_check_size_2.setObjectName("PrimaryBtn")
        self.btn_check_size_2.clicked.connect(self._on_check_size)
        batch_header.addWidget(batch_lbl)
        batch_header.addStretch()
        batch_header.addWidget(self.btn_check_size_2)
        panel_batch_layout.addLayout(batch_header)

        self.input_batch = QPlainTextEdit()
        self.input_batch.setFixedHeight(120)
        self.input_batch.setPlaceholderText(
            "https://example.com/tap1.m3u8\nhttps://example.com/tap2.m3u8\nhttps://example.com/tap3.m3u8"
        )
        panel_batch_layout.addWidget(self.input_batch)

        batch_hint = QLabel(
            "💡 Gợi ý: Mỗi dòng 1 URL. Nhập \"Tên file\" bên dưới để tự động đánh số thứ tự (ví dụ: TenPhim_01, TenPhim_02,...) hoặc để trống để tự đặt theo thời gian."
        )
        batch_hint.setObjectName("HintLabel")
        batch_hint.setWordWrap(True)
        panel_batch_layout.addWidget(batch_hint)

        self.panel_batch.setVisible(False)
        main_layout.addWidget(self.panel_batch)

        # --- Sub-view 3: Raw M3U8 Text ---
        self.panel_text = QWidget()
        panel_text_layout = QVBoxLayout(self.panel_text)
        panel_text_layout.setContentsMargins(0, 0, 0, 0)
        panel_text_layout.setSpacing(6)

        text_lbl = QLabel("Nội dung M3U8 (hoặc chuỗi mã hóa):")
        text_lbl.setStyleSheet("font-weight: 500;")
        panel_text_layout.addWidget(text_lbl)

        self.input_m3u8_text = QPlainTextEdit()
        self.input_m3u8_text.setFixedHeight(110)
        self.input_m3u8_text.setPlaceholderText("#EXTM3U...")
        panel_text_layout.addWidget(self.input_m3u8_text)

        base_url_lbl = QLabel("M3U8 Base URL (đối với đường dẫn tương đối):")
        self.input_base_url = QLineEdit()
        self.input_base_url.setPlaceholderText("https://example.com/path/")
        panel_text_layout.addWidget(base_url_lbl)
        panel_text_layout.addWidget(self.input_base_url)

        self.panel_text.setVisible(False)
        main_layout.addWidget(self.panel_text)

        # --- Section: Output Folder & File Name ---
        folder_title = QLabel("Thư mục lưu & Tên file")
        folder_title.setObjectName("SectionTitle")
        main_layout.addWidget(folder_title)

        folder_row = QHBoxLayout()
        self.input_folder = QLineEdit()
        self.input_folder.setPlaceholderText("Nhập đường dẫn lưu video")
        btn_browse = QPushButton("Chọn...")
        btn_browse.setObjectName("PrimaryBtn")
        btn_browse.clicked.connect(self._on_browse_folder)
        folder_row.addWidget(self.input_folder, 1)
        folder_row.addWidget(btn_browse, 0)
        main_layout.addLayout(folder_row)

        name_row = QHBoxLayout()
        name_lbl = QLabel("Tên file:")
        self.input_filename = QLineEdit()
        self.input_filename.setPlaceholderText("Để trống = tự đặt tên theo thời gian")
        name_row.addWidget(name_lbl)
        name_row.addWidget(self.input_filename, 1)
        main_layout.addLayout(name_row)

        # --- Section: Advanced Settings (Collapsible) ---
        self.expander = CollapsibleBox("Cài đặt nâng cao")

        # Format selector
        fmt_row = QHBoxLayout()
        fmt_lbl = QLabel("Định dạng đầu ra:")
        self.combo_format = QComboBox()
        self.combo_format.addItem("MP4 (Video)", "mp4")
        self.combo_format.addItem("MKV (Video)", "mkv")
        self.combo_format.addItem("MP3 (Âm thanh)", "mp3")
        self.combo_format.addItem("M4A/AAC (Âm thanh)", "m4a")
        fmt_row.addWidget(fmt_lbl)
        fmt_row.addWidget(self.combo_format)
        fmt_row.addStretch()
        self.expander.add_layout(fmt_row)

        # Max workers
        worker_row = QHBoxLayout()
        worker_lbl = QLabel("Số luồng chạy (concurrent fragments):")
        self.spin_worker = QSpinBox()
        self.spin_worker.setRange(1, 128)
        self.spin_worker.setValue(32)
        worker_row.addWidget(worker_lbl)
        worker_row.addWidget(self.spin_worker)
        worker_row.addStretch()
        self.expander.add_layout(worker_row)

        # Custom Headers
        header_lbl = QLabel("Headers tùy chỉnh:")
        self.input_headers = QPlainTextEdit()
        self.input_headers.setFixedHeight(120)
        self.expander.add_widget(header_lbl)
        self.expander.add_widget(self.input_headers)

        main_layout.addWidget(self.expander)

        # --- Section: Result / Output Terminal ---
        result_title = QLabel("Kết quả")
        result_title.setObjectName("SectionTitle")
        main_layout.addWidget(result_title)

        self.log_box = QPlainTextEdit()
        self.log_box.setObjectName("LogBox")
        self.log_box.setReadOnly(True)
        self.log_box.setFixedHeight(140)
        main_layout.addWidget(self.log_box)

        # --- Section: Download & Stop Buttons ---
        btn_row = QHBoxLayout()
        self.btn_download = QPushButton("Tải")
        self.btn_download.setObjectName("PrimaryBtn")
        self.btn_download.setFixedSize(110, 36)
        self.btn_download.clicked.connect(self._on_start_download)

        self.btn_stop = QPushButton("Dừng")
        self.btn_stop.setObjectName("SecondaryBtn")
        self.btn_stop.setFixedSize(110, 36)
        self.btn_stop.setEnabled(False)
        self.btn_stop.clicked.connect(self._on_stop_download)

        btn_row.addWidget(self.btn_download)
        btn_row.addWidget(self.btn_stop)
        btn_row.addStretch()
        main_layout.addLayout(btn_row)

        # --- Section: Footer ---
        footer_line = QFrame()
        footer_line.setFrameShape(QFrame.Shape.HLine)
        footer_line.setStyleSheet("color: #333333; margin-top: 15px;")
        main_layout.addWidget(footer_line)

        # Extension link
        ext_layout = QHBoxLayout()
        ext_hint = QLabel("💡 Cài extension ")
        ext_link = QLabel('<a href="https://chromewebstore.google.com/detail/b%E1%BA%AFt-link-phim/kjhnhjbmbfbocolepmagbbgnakbjlhcg" style="color: #1E90FF; text-decoration: none; font-weight: bold;">Bắt Link Phim</a>')
        ext_link.setOpenExternalLinks(True)
        ext_suffix = QLabel(" để tự động lấy link phim, headers")
        ext_layout.addWidget(ext_hint)
        ext_layout.addWidget(ext_link)
        ext_layout.addWidget(ext_suffix)
        ext_layout.addStretch()
        main_layout.addLayout(ext_layout)

        # Footer Actions & Socials
        bottom_row = QHBoxLayout()
        btn_donate = QPushButton("❤ Donate")
        btn_donate.setObjectName("DonateBtn")
        btn_donate.clicked.connect(lambda: QDesktopServices.openUrl(QUrl("https://donate-trtoan.vercel.app/")))
        bottom_row.addWidget(btn_donate)

        bottom_row.addStretch()

        # Facebook Link
        fb_icon_path = get_resource_path("Resource/Image/facebook.png")
        if os.path.exists(fb_icon_path):
            fb_img = QLabel()
            pix = QPixmap(fb_icon_path).scaled(18, 18, Qt.AspectRatioMode.KeepAspectRatio, Qt.TransformationMode.SmoothTransformation)
            fb_img.setPixmap(pix)
            bottom_row.addWidget(fb_img)

        fb_link = QLabel('<a href="https://www.facebook.com/profile.php?id=61567027726244" style="color: #1E90FF; text-decoration: none; font-weight: bold;">Fanpage : Thích Lập Trình</a>')
        fb_link.setOpenExternalLinks(True)
        bottom_row.addWidget(fb_link)

        # Spacing between socials
        bottom_row.addSpacing(16)

        # TikTok Link
        tt_icon_path = get_resource_path("Resource/Image/tiktok.png")
        if os.path.exists(tt_icon_path):
            tt_img = QLabel()
            pix = QPixmap(tt_icon_path).scaled(18, 18, Qt.AspectRatioMode.KeepAspectRatio, Qt.TransformationMode.SmoothTransformation)
            tt_img.setPixmap(pix)
            bottom_row.addWidget(tt_img)

        tt_link = QLabel('<a href="https://www.tiktok.com/@thich_it" style="color: #1E90FF; text-decoration: none; font-weight: bold;">Tiktok: @thich_it</a>')
        tt_link.setOpenExternalLinks(True)
        bottom_row.addWidget(tt_link)

        main_layout.addLayout(bottom_row)

        # Telegram / AI Link Row
        telegram_row = QHBoxLayout()
        telegram_row.addStretch()

        telegram_icon_path = get_resource_path("Resource/Image/telegram.png")
        if os.path.exists(telegram_icon_path):
            telegram_img = QLabel()
            pix = QPixmap(telegram_icon_path).scaled(
                18,
                18,
                Qt.AspectRatioMode.KeepAspectRatio,
                Qt.TransformationMode.SmoothTransformation,
            )
            telegram_img.setPixmap(pix)
            telegram_row.addWidget(telegram_img)

        ai_label = QLabel(
            'AI mình dùng để vibe code '
            '<a href="https://t.me/DichVuIT_bot" '
            'style="color: #0098ff; text-decoration: none; font-weight: bold;">'
            'tại đây: @DichVuIT_bot</a>'
        )
        ai_label.setStyleSheet("color: #cccccc; font-size: 12px;")
        ai_label.setOpenExternalLinks(True)
        telegram_row.addWidget(ai_label)

        main_layout.addLayout(telegram_row)

    def _on_mode_changed(self, button_id: int, checked: bool):
        if not checked:
            return
        self.panel_url.setVisible(button_id == 1)
        self.panel_batch.setVisible(button_id == 2)
        self.panel_text.setVisible(button_id == 3)

    def _load_config_to_ui(self):
        self.input_url.setText(self.config.url)
        self.input_batch.setPlainText(self.config.batch_urls)
        self.input_m3u8_text.setPlainText(self.config.m3u8_text)
        self.input_base_url.setText(self.config.m3u8_base_url)
        self.input_folder.setText(self.config.video_path)
        self.input_filename.setText(self.config.video_name)
        self.spin_worker.setValue(int(self.config.max_worker or 32))
        self.input_headers.setPlainText(self.config.headers)

        # Set format combo
        idx = self.combo_format.findData(self.config.preferred_format)
        if idx >= 0:
            self.combo_format.setCurrentIndex(idx)

    def _update_config_from_ui(self):
        self.config.url = self.input_url.text().strip()
        self.config.batch_urls = self.input_batch.toPlainText().strip()
        self.config.m3u8_text = self.input_m3u8_text.toPlainText().strip()
        self.config.m3u8_base_url = self.input_base_url.text().strip()
        self.config.video_path = self.input_folder.text().strip()
        self.config.video_name = self.input_filename.text().strip()
        self.config.max_worker = self.spin_worker.value()
        self.config.headers = self.input_headers.toPlainText()
        self.config.preferred_format = self.combo_format.currentData() or "mp4"

        self.config_service.save_settings(self.config)

    def _on_browse_folder(self):
        initial = self.input_folder.text().strip() or str(Path.home())
        selected = QFileDialog.getExistingDirectory(self, "Chọn thư mục lưu video", initial)
        if selected:
            self.input_folder.setText(selected)
            self._update_config_from_ui()

    def _append_log(self, text: str):
        self.log_box.appendPlainText(text)
        self.log_box.verticalScrollBar().setValue(self.log_box.verticalScrollBar().maximum())

    def _on_check_size(self):
        self._update_config_from_ui()

        target_url = ""
        if self.radio_url.isChecked():
            target_url = self.input_url.text().strip()
            if not target_url:
                QMessageBox.warning(self, "Thông báo", "❌ Vui lòng nhập URL!")
                return
        elif self.radio_batch.isChecked():
            lines = [l.strip() for l in self.input_batch.toPlainText().splitlines() if l.strip() and not l.startswith("#")]
            if not lines:
                QMessageBox.warning(self, "Thông báo", "❌ Vui lòng nhập ít nhất một URL trong danh sách!")
                return
            target_url = lines[0].split("|")[0].strip()
            self._append_log(f"🔍 Danh sách gồm {len(lines)} URL. Đang kiểm tra link đầu tiên...")
        else:
            QMessageBox.information(self, "Thông báo", "Chức năng Check size áp dụng cho chế độ URL hoặc Danh sách URL.")
            return

        self._append_log(f"🔍 Đang kiểm tra kích thước...")
        self.size_worker = SizeCheckerThread(target_url, self.input_headers.toPlainText())
        self.size_worker.progress_signal.connect(self._append_log)
        self.size_worker.finished_signal.connect(self._append_log)
        self.size_worker.start()

    def _on_start_download(self):
        self._update_config_from_ui()

        if not self.config.video_path:
            QMessageBox.warning(self, "Thông báo", "❌ Vui lòng chọn thư mục lưu video!")
            return

        tasks: list[DownloadTask] = []

        if self.radio_url.isChecked():
            url = self.config.url.strip()
            if not url:
                QMessageBox.warning(self, "Thông báo", "❌ Vui lòng nhập URL cần tải!")
                return
            tasks.append(DownloadTask(input_source=url, custom_name=self.config.video_name))

        elif self.radio_batch.isChecked():
            lines = [l.strip() for l in self.config.batch_urls.splitlines() if l.strip() and not l.startswith("#")]
            if not lines:
                QMessageBox.warning(self, "Thông báo", "❌ Vui lòng nhập ít nhất một URL trong danh sách!")
                return
            for line in lines:
                # Support "URL | CustomName" syntax if present
                if "|" in line:
                    parts = line.split("|", 1)
                    tasks.append(DownloadTask(input_source=parts[0].strip(), custom_name=parts[1].strip()))
                else:
                    tasks.append(DownloadTask(input_source=line))

        else:  # Text mode
            content = self.config.m3u8_text.strip()
            if not content:
                QMessageBox.warning(self, "Thông báo", "❌ Vui lòng nhập nội dung M3U8 hoặc chuỗi mã hóa!")
                return
            tasks.append(DownloadTask(input_source=content, custom_name=self.config.video_name, is_raw_m3u8=True))

        # Toggle UI state
        self.btn_download.setEnabled(False)
        self.btn_stop.setEnabled(True)
        self.log_box.clear()

        self.download_worker = DownloaderThread(tasks, self.config)
        self.download_worker.log_signal.connect(self._append_log)
        self.download_worker.progress_signal.connect(self._append_log)
        self.download_worker.finished_signal.connect(self._on_download_finished)
        self.download_worker.start()

    def _on_stop_download(self):
        if self.download_worker and self.download_worker.isRunning():
            self._append_log("⏹️ Đang gửi tín hiệu dừng tải...")
            self.download_worker.cancel()

    def _on_download_finished(self, success: bool, message: str):
        self.btn_download.setEnabled(True)
        self.btn_stop.setEnabled(False)
        self._append_log(message)

    def closeEvent(self, event):
        self._update_config_from_ui()
        if self.download_worker and self.download_worker.isRunning():
            self.download_worker.cancel()
            self.download_worker.wait(1500)
        event.accept()
