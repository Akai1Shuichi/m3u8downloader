MAIN_STYLESHEET = """
QMainWindow {
    background-color: #1e1e1e;
    color: #ffffff;
    font-family: "Segoe UI", -apple-system, BlinkMacSystemFont, "Roboto", "Helvetica Neue", sans-serif;
    font-size: 13px;
}

QWidget {
    color: #f0f0f0;
    font-size: 13px;
}

QGroupBox {
    border: 1px solid #333333;
    border-radius: 8px;
    margin-top: 10px;
    padding-top: 12px;
    font-weight: bold;
    color: #e0e0e0;
}

QGroupBox::title {
    subcontrol-origin: margin;
    left: 10px;
    padding: 0 5px;
}

QLineEdit, QTextEdit, QPlainTextEdit, QSpinBox, QComboBox {
    background-color: #2b2b2b;
    border: 1px solid #3e3e3e;
    border-radius: 6px;
    padding: 6px 10px;
    color: #ffffff;
    selection-background-color: #0078d4;
}

QLineEdit:focus, QTextEdit:focus, QPlainTextEdit:focus, QSpinBox:focus, QComboBox:focus {
    border: 1px solid #0078d4;
    background-color: #323232;
}

QPushButton {
    background-color: #2d2d2d;
    border: 1px solid #444444;
    border-radius: 6px;
    padding: 6px 16px;
    font-weight: 600;
    color: #ffffff;
}

QPushButton:hover {
    background-color: #3d3d3d;
    border-color: #555555;
}

QPushButton:pressed {
    background-color: #222222;
}

QPushButton:disabled {
    background-color: #1f1f1f;
    border-color: #2a2a2a;
    color: #666666;
}

/* Primary Accent Button */
QPushButton#PrimaryBtn {
    background-color: #0078d4;
    border: 1px solid #1084d8;
    color: #ffffff;
}

QPushButton#PrimaryBtn:hover {
    background-color: #1a88e0;
}

QPushButton#PrimaryBtn:pressed {
    background-color: #006abc;
}

/* Secondary Cancel/Stop Button */
QPushButton#SecondaryBtn {
    background-color: #c42b1c;
    border: 1px solid #d13438;
    color: #ffffff;
}

QPushButton#SecondaryBtn:hover {
    background-color: #d13438;
}

QPushButton#SecondaryBtn:pressed {
    background-color: #a82315;
}

/* Donate Button */
QPushButton#DonateBtn {
    background: qlineargradient(x1:0, y1:0, x2:1, y2:1, stop:0 #fe6a7e, stop:1 #8a2be2);
    border: 1px solid #8a2be2;
    color: #ffffff;
    font-weight: bold;
    padding: 6px 16px;
}

QPushButton#DonateBtn:hover {
    background: qlineargradient(x1:0, y1:0, x2:1, y2:1, stop:0 #ff7e90, stop:1 #9b3df0);
}

QRadioButton {
    spacing: 8px;
    font-weight: 500;
}

QRadioButton::indicator {
    width: 16px;
    height: 16px;
    border-radius: 8px;
    border: 1px solid #666666;
    background-color: #2b2b2b;
}

QRadioButton::indicator:checked {
    background-color: #0078d4;
    border: 3px solid #1e1e1e;
}

QScrollBar:vertical {
    border: none;
    background: #1e1e1e;
    width: 10px;
    margin: 0px;
}

QScrollBar::handle:vertical {
    background: #444444;
    min-height: 25px;
    border-radius: 5px;
}

QScrollBar::handle:vertical:hover {
    background: #555555;
}

QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical {
    height: 0px;
}

QScrollBar:horizontal {
    border: none;
    background: #1e1e1e;
    height: 10px;
    margin: 0px;
}

QScrollBar::handle:horizontal {
    background: #444444;
    min-width: 25px;
    border-radius: 5px;
}

QScrollBar::handle:horizontal:hover {
    background: #555555;
}

QScrollBar::add-line:horizontal, QScrollBar::sub-line:horizontal {
    width: 0px;
}

QLabel#HintLabel {
    color: #aaaaaa;
    font-size: 11px;
}

QLabel#SectionTitle {
    font-weight: bold;
    font-size: 13px;
    color: #ffffff;
}

QPlainTextEdit#LogBox {
    background-color: #181818;
    border: 1px solid #333333;
    border-radius: 6px;
    font-family: "Consolas", "Courier New", monospace;
    font-size: 12px;
    color: #d4d4d4;
}
"""
