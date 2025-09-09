# M3U8 Downloader

A modern Windows desktop application for downloading M3U8 video streams with an intuitive WPF interface.

## Features

- 🎥 Download M3U8 video streams
- 🖥️ Modern WPF UI with WPF-UI framework
- ⚡ Built with .NET 8.0 for optimal performance
- 📦 Self-contained executable (no additional dependencies)
- 🛠️ Integrated with yt-dlp for enhanced compatibility
- 🎨 Beautiful and responsive user interface

## Screenshots

*Screenshots will be added here*

## System Requirements

- Windows 10/11 (64-bit)
- No additional dependencies required (self-contained)

## Installation

### Option 1: Download Pre-built Release
1. Go to the [Releases](https://github.com/yourusername/m3u8Downloader/releases) page
2. Download the latest `m3u8Downloader-vX.X.X.zip` file
3. Extract the ZIP file to your desired location
4. Run `m3u8Downloader.exe`

### Option 2: Build from Source
1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/m3u8Downloader.git
   cd m3u8Downloader
   ```

2. Restore dependencies:
   ```bash
   dotnet restore m3u8Downloader/m3u8Downloader.csproj
   ```

3. Build the application:
   ```bash
   dotnet build m3u8Downloader/m3u8Downloader.csproj --configuration Release
   ```

4. Run the application:
   ```bash
   dotnet run --project m3u8Downloader/m3u8Downloader.csproj
   ```

## Usage

1. Launch the application
2. Paste your M3U8 URL into the input field
3. Choose your download location
4. Click the download button
5. Wait for the download to complete

## Building for Release

To create a release build:

```bash
# Clean previous builds
dotnet clean m3u8Downloader/m3u8Downloader.csproj

# Restore packages
dotnet restore m3u8Downloader/m3u8Downloader.csproj

# Publish self-contained executable
dotnet publish m3u8Downloader/m3u8Downloader.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  --output ./publish/win-x64 \
  -p:PublishReadyToRun=true
```

## Project Structure

```
m3u8Downloader/
├── Models/              # Data models
├── MVVM/               # MVVM base classes
├── Services/           # Business logic services
├── ViewModel/          # ViewModels
├── Views/              # XAML views and controls
├── Resource/           # Application resources
├── Tools/              # External tools (yt-dlp)
└── Styles/             # UI styling
```

## Technologies Used

- **.NET 8.0** - Modern .NET framework
- **WPF** - Windows Presentation Foundation for UI
- **WPF-UI** - Modern UI components
- **MVVM Pattern** - Clean architecture
- **yt-dlp** - Video download engine

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [yt-dlp](https://github.com/yt-dlp/yt-dlp) for video downloading capabilities
- [WPF-UI](https://github.com/lepoco/wpfui) for modern UI components

## Support

If you encounter any issues or have questions, please:
1. Check the [Issues](https://github.com/yourusername/m3u8Downloader/issues) page
2. Create a new issue if your problem isn't already reported
3. Provide detailed information about your system and the issue

---

**Note**: Remember to replace `yourusername` with your actual GitHub username in the URLs above. 
