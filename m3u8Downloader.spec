# -*- mode: python ; coding: utf-8 -*-
import sys
import os
from pathlib import Path

block_cipher = None

datas = [
    ('Resource/Image', 'Resource/Image'),
]

# If local Tools folder exists, bundle it
if os.path.exists('Tools'):
    datas.append(('Tools', 'Tools'))

hiddenimports = [
    'PyQt6',
    'PyQt6.QtCore',
    'PyQt6.QtGui',
    'PyQt6.QtWidgets',
    'qdarktheme',
    'Crypto',
    'Crypto.Cipher.AES',
    'Crypto.Util.Padding',
    'requests',
    'yt_dlp',
]

a = Analysis(
    ['main.py'],
    pathex=[],
    binaries=[],
    datas=datas,
    hiddenimports=hiddenimports,
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    win_no_prefer_redirects=False,
    win_private_assemblies=False,
    cipher=block_cipher,
    noarchive=False,
)

pyz = PYZ(a.pure, a.zipped_data, cipher=block_cipher)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name='M3U8Downloader',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    console=False,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
    icon='Resource/Image/app.ico' if sys.platform == 'win32' else None,
)

coll = COLLECT(
    exe,
    a.binaries,
    a.zipfiles,
    a.datas,
    strip=False,
    upx=True,
    upx_exclude=[],
    name='M3U8Downloader',
)

if sys.platform == 'darwin':
    app = BUNDLE(
        coll,
        name='M3U8Downloader.app',
        icon='Resource/Image/app.ico',
        bundle_identifier='com.m3u8downloader.app',
    )
