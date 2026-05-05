# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] - 2026-05-05
ok giơ
### Fixed
- MP4 compatibility issue on mobile devices by enforcing `yuv420p` pixel format and H.264/AAC codecs.
- Missing video metadata (length, resolution, bit rate) in Windows Explorer.
- Slow start/buffering on mobile by enabling `faststart` (moving moov atom to the front).

### Added
- Native FFmpeg integration via `Tools/ffmpeg/` directory.
- Automatic fragment repair using `--fixup force`.
- Automatic metadata preservation using `--add-metadata`.

## [1.1.0] - 2025-10-07

### Added
- Support downloading animevietsub movies (auto link extraction, token handling, anti-bot bypass)
- Added feature to input raw M3U8 content for direct download
- Advanced setting: configurable M3U8 conversion batch size (number of parallel conversion threads)
- Minor improvements and dependency updates

### Changed
- Bumped application version to 1.1.0

## [1.0.0] - 2024-01-XX

### Added
- Initial release of M3U8 Downloader
- Modern WPF UI with WPF-UI framework
- M3U8 video stream downloading functionality
- Integration with yt-dlp for enhanced compatibility
- Self-contained executable (no additional dependencies required)
- Support for custom headers and download paths
- Real-time download progress tracking
- Error handling and user feedback
