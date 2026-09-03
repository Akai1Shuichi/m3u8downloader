# Cài đặt và chạy từ source

Yêu cầu:

- Python 3.10+
- FFmpeg

Package giao diện của dự án hỗ trợ Python từ 3.10 đến 3.14.

## Windows PowerShell

```powershell
py -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
python -m pip install -r requirements.txt
python main.py
```

Nếu đã từng cài dependencies và gặp lỗi `No matching distribution found for
pyqtdarktheme`, hãy cập nhật lại từ file requirements:

```powershell
python -m pip uninstall -y pyqtdarktheme
python -m pip install --upgrade -r requirements.txt
```

Nếu PowerShell chặn script kích hoạt, chạy lệnh sau trong phiên hiện tại rồi kích hoạt lại:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\.venv\Scripts\Activate.ps1
```

## Windows Command Prompt (CMD)

```bat
py -m venv .venv
.\.venv\Scripts\activate.bat
python -m pip install --upgrade pip
python -m pip install -r requirements.txt
python main.py
```

## macOS

```bash
python3 -m venv .venv
source .venv/bin/activate
python -m pip install --upgrade pip
python -m pip install -r requirements.txt
python main.py
```

## Linux

```bash
python3 -m venv .venv
source .venv/bin/activate
python -m pip install --upgrade pip
python -m pip install -r requirements.txt
python main.py
```

Trên Ubuntu/Debian, nếu chưa có module `venv`:

```bash
sudo apt update
sudo apt install python3-venv
```

## Thoát môi trường ảo

Áp dụng cho tất cả hệ điều hành:

```bash
deactivate
```
