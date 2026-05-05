# M3U8 Downloader

A modern Windows desktop application for downloading M3U8 video streams with an intuitive WPF interface.

## What's New in v1.2.0

- 📱 **Enhanced MP4 Compatibility:** Videos are now optimized for iPhone/Android playback using `yuv420p` pixel format and H.264/AAC codecs.
- 🏷️ **Full Metadata Preservation:** Fixed the issue where length, resolution, and bit rate were missing in Windows Explorer.
- 🚀 **Fast Start enabled:** Videos now support instant playback (faststart) on mobile devices.
- 🛠️ **FFmpeg Integration:** Native support for FFmpeg to ensure robust muxing and fragment repair.
- 🔧 **Automatic Fixup:** Uses `--fixup force` to automatically repair corrupted or missing video fragments.

## Features

- 🎥 Download M3U8/MP4/MP3/M4A/MKV video and audio streams
- 🤖 **Animevietsub Support:** Automatic link extraction and anti-bot bypass.
- 🖥️ Modern WPF UI with WPF-UI framework
- ⚡ Built with .NET 8.0 for optimal performance
- 📦 Self-contained executable
- 📝 Support for custom headers and download paths
- 📊 Real-time download progress tracking

## Screenshots
![GUI App](Resource/Image/appScreenshot.png)

## Installation & Setup
1. Download the latest release from the [Releases](https://github.com/Akai1Shuichi/m3u8downloader/releases) page.
2. Extract the ZIP file.
3. **Important:** To enable full MP4 compatibility and metadata, place `ffmpeg.exe` and `ffprobe.exe` inside the `Tools/ffmpeg/` directory.
4. Run `m3u8Downloader.exe`.

## Usage
1. Launch the application.
2. Paste your M3U8 URL or raw M3U8 content into the input field.
3. Choose your preferred format (MP4, MKV, MP3, etc.).
4. Click the download button.

For more details, see the [Changelog](CHANGELOG.md).
