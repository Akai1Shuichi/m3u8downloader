using m3u8Downloader.Model;
using m3u8Downloader.MVVM;
using m3u8Downloader.Services;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows.Input;
using WpfUiMessageBox = Wpf.Ui.Controls.MessageBox;

namespace m3u8Downloader.ViewModel
{
    public class MainWindowViewModel : ViewModelBase
    {
        private Process? _downloadProcess;
        private CancellationTokenSource? _cancellationTokenSource;
        private Config _config = new();
        private readonly ConfigService _configService;
        private LocalHttpServer? _httpServer;
        private PlaywrightService? _playwrightService;

        private bool _isPaused = false;
        private bool _isDownloading = false;
        private string m3u8TextFromUrl = "";
        private string _extractedToken = "";
        private string _lastSafeBaseName = "";

        private string _url = "";
        public string Url
        {
            get => _url;
            set { _url = value; OnPropertyChanged(); }
        }

        private string _batchUrls = "";
        public string BatchUrls
        {
            get => _batchUrls;
            set { _batchUrls = value; OnPropertyChanged(); }
        }

        // Helper method to extract domain from URL
        private string ExtractDomain(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";

            try
            {
                var uri = new Uri(url);
                return uri.Host;
            }
            catch
            {
                return "";
            }
        }

        // Property to get current domain
        public string CurrentDomain => ExtractDomain(Url);

        // Input mode properties
        private bool _isUrlMode = true;
        public bool IsUrlMode
        {
            get => _isUrlMode;
            set
            {
                _isUrlMode = value;
                OnPropertyChanged();
                if (value)
                {
                    IsBatchUrlMode = false;
                    IsTextMode = false;
                }
            }
        }

        private bool _isBatchUrlMode = false;
        public bool IsBatchUrlMode
        {
            get => _isBatchUrlMode;
            set
            {
                _isBatchUrlMode = value;
                OnPropertyChanged();
                if (value)
                {
                    IsUrlMode = false;
                    IsTextMode = false;
                }
            }
        }

        private bool _isTextMode = false;
        public bool IsTextMode
        {
            get => _isTextMode;
            set
            {
                _isTextMode = value;
                OnPropertyChanged();
                if (value)
                {
                    IsUrlMode = false;
                    IsBatchUrlMode = false;
                }
            }
        }

        private string _m3u8Text = "";
        public string M3u8Text
        {
            get => _m3u8Text;
            set { _m3u8Text = value; OnPropertyChanged(); }
        }

        private string _m3u8BaseUrl = "";
        public string M3u8BaseUrl
        {
            get => _m3u8BaseUrl;
            set { _m3u8BaseUrl = value; OnPropertyChanged(); }
        }

        private string _videoPath = "";
        public string VideoPath
        {
            get => _videoPath;
            set { _videoPath = value; OnPropertyChanged(); }
        }

        private string _videoName = "";
        public string VideoName
        {
            get => _videoName;
            set { _videoName = value; OnPropertyChanged(); }
        }

        private double _maxWorker = 1;
        public double MaxWorker
        {
            get => _maxWorker;
            set { _maxWorker = value; OnPropertyChanged(); }
        }

        private int _batchSize = 10;
        public int BatchSize
        {
            get => _batchSize;
            set { _batchSize = value; OnPropertyChanged(); }
        }

        private bool _isAnimevietsub = false;
        public bool IsAnimevietsub
        {
            get => _isAnimevietsub;
            set { _isAnimevietsub = value; OnPropertyChanged(); }
        }

        private string _result = "";
        public string Result
        {
            get => _result;
            set { _result = value; OnPropertyChanged(); }
        }

        private string _headers = "";
        public string Headers
        {
            get => _headers;
            set { _headers = value; OnPropertyChanged(); }
        }

        private string _preferredFormat = "mp4";
        public string PreferredFormat
        {
            get => _preferredFormat;
            set { _preferredFormat = value; OnPropertyChanged(); }
        }

        public bool IsDownloading
        {
            get => _isDownloading;
            set { _isDownloading = value; OnPropertyChanged(); }
        }

        // Commands
        public ICommand DownloadCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand CheckSizeCommand { get; }
        public ICommand BrowseFolderCommand { get; }
        public ICommand OpenDonateCommand { get; }
        public ICommand FetchAnimevietsubApiCommand { get; }

        public MainWindowViewModel()
        {
            _configService = new ConfigService();
            _playwrightService = new PlaywrightService();
            _ = LoadSettingsAsync();

            DownloadCommand = new RelayCommand(async _ => await Download());
            CheckSizeCommand = new RelayCommand(_ => CheckSize());
            BrowseFolderCommand = new RelayCommand(_ => BrowseFolder());
            PauseCommand = new RelayCommand(async _ => await PauseDownloadAsync());
            OpenDonateCommand = new RelayCommand(_ => OpenDonate());
            FetchAnimevietsubApiCommand = new RelayCommand(async _ => await FetchAnimevietsubApi());

            // Đăng ký event handlers cho PlaywrightService
            if (_playwrightService != null)
            {
                _playwrightService.LogMessage += OnPlaywrightLogMessage;
                _playwrightService.ErrorOccurred += OnPlaywrightError;
            }
        }

        private async Task LoadSettingsAsync()
        {
            try
            {
                _config = await _configService.LoadSettingsAsync();

                // Áp dụng cài đặt vào properties
                Url = _config.Url;
                BatchUrls = _config.BatchUrls;
                M3u8Text = _config.M3u8Text;
                M3u8BaseUrl = _config.M3u8BaseUrl;
                VideoPath = _config.VideoPath;
                VideoName = _config.VideoName;
                MaxWorker = _config.MaxWorker;
                BatchSize = _config.BatchSize;
                Headers = _config.Headers;
                PreferredFormat = _config.PreferredFormat;
            }
            catch (Exception ex)
            {
                Result = $"Lỗi khi tải cài đặt: {ex.Message}";
            }
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                // Cập nhật model với các giá trị hiện tại
                _config.Url = Url;
                _config.BatchUrls = BatchUrls;
                _config.M3u8Text = M3u8Text;
                _config.M3u8BaseUrl = M3u8BaseUrl;
                _config.VideoPath = VideoPath;
                _config.VideoName = VideoName;
                _config.MaxWorker = MaxWorker;
                _config.BatchSize = BatchSize;
                _config.Headers = Headers;
                _config.PreferredFormat = PreferredFormat;

                await _configService.SaveSettingsAsync(_config);
            }
            catch (Exception ex)
            {
                Result += $"\nLỗi khi lưu cài đặt: {ex.Message}";
            }
        }

        private async Task Download()
        {
            if (string.IsNullOrWhiteSpace(VideoPath))
            {
                var messageBox = new WpfUiMessageBox
                {
                    Title = "Thông báo",
                    Content = "❌ Vui lòng chọn thư mục lưu!"
                };
                await messageBox.ShowDialogAsync();
                return;
            }

            // Set trạng thái đang tải
            IsDownloading = true;
            _isPaused = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            // Lưu cài đặt trước khi tải
            await SaveSettingsAsync();

            try
            {
                if (IsUrlMode)
                {
                    if (string.IsNullOrWhiteSpace(Url))
                    {
                        var messageBox = new WpfUiMessageBox
                        {
                            Title = "Thông báo",
                            Content = "❌ Vui lòng nhập URL!"
                        };
                        await messageBox.ShowDialogAsync();
                        IsDownloading = false;
                        return;
                    }

                    Result = $"Bắt đầu tải video từ URL: {Url}\nThư mục: {VideoPath}\nSố luồng: {MaxWorker}";
                    await DownloadCoreAsync(Url, VideoName, 1, 1, isRawM3u8: false, _cancellationTokenSource.Token);
                }
                else if (IsTextMode)
                {
                    if (string.IsNullOrWhiteSpace(M3u8Text))
                    {
                        var messageBox = new WpfUiMessageBox
                        {
                            Title = "Thông báo",
                            Content = "❌ Vui lòng nhập nội dung M3U8!"
                        };
                        await messageBox.ShowDialogAsync();
                        IsDownloading = false;
                        return;
                    }

                    Result = $"Bắt đầu tải video từ M3U8 text\nThư mục: {VideoPath}\nSố luồng: {MaxWorker}\n📝 Độ dài nội dung: {M3u8Text.Length} ký tự";
                    await DownloadCoreAsync(M3u8Text, VideoName, 1, 1, isRawM3u8: true, _cancellationTokenSource.Token);
                }
                else if (IsBatchUrlMode)
                {
                    var lines = (BatchUrls ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    var batchUrls = new List<string>();

                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                            continue;

                        if (trimmed.Contains("|"))
                        {
                            var parts = trimmed.Split(new[] { '|' }, 2);
                            var itemUrl = parts[0].Trim();
                            if (!string.IsNullOrEmpty(itemUrl))
                            {
                                batchUrls.Add(itemUrl);
                            }
                        }
                        else
                        {
                            batchUrls.Add(trimmed);
                        }
                    }

                    if (batchUrls.Count == 0)
                    {
                        var messageBox = new WpfUiMessageBox
                        {
                            Title = "Thông báo",
                            Content = "❌ Vui lòng nhập ít nhất một URL hợp lệ!"
                        };
                        await messageBox.ShowDialogAsync();
                        IsDownloading = false;
                        return;
                    }

                    Result = $"🚀 Bắt đầu tải hàng loạt ({batchUrls.Count} video)\nThư mục: {VideoPath}\nSố luồng: {MaxWorker}";

                    int successCount = 0;
                    int failCount = 0;

                    for (int i = 0; i < batchUrls.Count; i++)
                    {
                        if (_isPaused || _cancellationTokenSource.Token.IsCancellationRequested)
                            break;

                        var itemUrl = batchUrls[i];
                        int currentIdx = i + 1;
                        int totalCount = batchUrls.Count;

                        Result = $"⏳ [{currentIdx}/{totalCount}] Đang bắt đầu tải: {itemUrl}";

                        bool ok = await DownloadCoreAsync(itemUrl, null, currentIdx, totalCount, isRawM3u8: false, _cancellationTokenSource.Token);

                        if (ok)
                        {
                            successCount++;
                        }
                        else
                        {
                            if (_isPaused || _cancellationTokenSource.Token.IsCancellationRequested)
                                break;
                            failCount++;
                        }

                        if (i < batchUrls.Count - 1 && !_isPaused && !_cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            await Task.Delay(500, _cancellationTokenSource.Token);
                        }
                    }

                    if (_isPaused || _cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        Result = $"⏸️ Đã dừng quá trình tải hàng loạt. (Đã hoàn thành: {successCount}/{batchUrls.Count})";
                    }
                    else
                    {
                        Result = $"🎉 Tải hàng loạt hoàn tất! Thành công: {successCount}/{batchUrls.Count}" + (failCount > 0 ? $", Thất bại: {failCount}" : "");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Xử lý khi bị hủy
            }
            catch (Exception ex)
            {
                if (!_isPaused)
                {
                    Result = $"❌ Lỗi: {ex.Message}";
                }
            }
            finally
            {
                IsDownloading = false;
            }
        }

        private async Task<bool> DownloadCoreAsync(string inputSource, string? customName, int itemIndex, int totalItems, bool isRawM3u8, CancellationToken cancellationToken)
        {
            try
            {
                string ytDlpPath = Path.Combine(AppContext.BaseDirectory, "Tools", "yt-dlp", "yt-dlp.exe");
                string ffmpegPath = Path.Combine(AppContext.BaseDirectory, "Tools", "ffmpeg", "ffmpeg.exe");

                if (!File.Exists(ytDlpPath))
                {
                    Result = "❌ Không tìm thấy file yt-dlp.exe!";
                    return false;
                }

                var headersDict = ParseHeaders(Headers);
                var headerArgs = new List<string>();
                foreach (var header in headersDict)
                {
                    headerArgs.Add($"--add-header \"{header.Key}:{header.Value}\"");
                }

                string prefix = (PreferredFormat == "mp4" || PreferredFormat == "mkv") ? "video" : "audio";
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                // Build a safe base name from user input / custom item name
                string safeBaseName;
                var invalidChars = Path.GetInvalidFileNameChars();

                if (!string.IsNullOrWhiteSpace(customName))
                {
                    safeBaseName = string.Concat(customName.Trim().Select(c => invalidChars.Contains(c) ? '_' : c));
                    if (string.IsNullOrWhiteSpace(safeBaseName))
                        safeBaseName = totalItems > 1 ? $"{prefix}_{timestamp}_{itemIndex:D2}" : $"{prefix}_{timestamp}";
                }
                else if (!string.IsNullOrWhiteSpace(VideoName))
                {
                    string baseName = totalItems > 1 ? $"{VideoName.Trim()}_{itemIndex:D2}" : VideoName.Trim();
                    safeBaseName = string.Concat(baseName.Select(c => invalidChars.Contains(c) ? '_' : c));
                    if (string.IsNullOrWhiteSpace(safeBaseName))
                        safeBaseName = totalItems > 1 ? $"{prefix}_{timestamp}_{itemIndex:D2}" : $"{prefix}_{timestamp}";
                }
                else
                {
                    safeBaseName = totalItems > 1 ? $"{prefix}_{timestamp}_{itemIndex:D2}" : $"{prefix}_{timestamp}";
                }

                string outputTemplate = Path.Combine(VideoPath, $"{safeBaseName}.%(ext)s");
                _lastSafeBaseName = safeBaseName;

                string inputArg;

                if (isRawM3u8)
                {
                    try
                    {
                        var domain = CurrentDomain;
                        string m3u8ContentToServe = inputSource;

                        if (!string.IsNullOrEmpty(domain) && domain.Contains("anime"))
                        {
                            if (_playwrightService == null)
                            {
                                _playwrightService = new PlaywrightService();
                                _playwrightService.LogMessage += OnPlaywrightLogMessage;
                                _playwrightService.ErrorOccurred += OnPlaywrightError;
                            }

                            _playwrightService.BatchSize = BatchSize;
                            _playwrightService.TargetDomain = domain;

                            bool isInstalled = await CheckPlaywrightInstallationAsync();
                            if (!isInstalled)
                            {
                                Result = "❌ Playwright chưa được cài đặt đúng cách";
                                return false;
                            }

                            bool initialized = await _playwrightService.InitializeAsync();
                            if (!initialized)
                            {
                                Result = "❌ Không thể khởi tạo Playwright";
                                return false;
                            }

                            var converted = await _playwrightService.ConvertM3U8ContentAsync(inputSource, cancellationToken);
                            if (string.IsNullOrWhiteSpace(converted))
                            {
                                Result = "❌ Không thể chuyển đổi nội dung M3U8";
                                return false;
                            }

                            m3u8ContentToServe = converted;
                            m3u8TextFromUrl = converted;
                        }
                        else
                        {
                            m3u8TextFromUrl = inputSource;
                        }

                        m3u8ContentToServe = NormalizeM3U8Content(m3u8ContentToServe, M3u8BaseUrl);
                        m3u8TextFromUrl = m3u8ContentToServe;

                        _httpServer = new LocalHttpServer(m3u8ContentToServe);
                        _httpServer.Start();

                        inputArg = $"\"{_httpServer.PlaylistUrl}\"";
                        Result += $"\n🌐 HTTP Server started at: {_httpServer.PlaylistUrl}";
                    }
                    catch (Exception ex)
                    {
                        Result = $"❌ Lỗi khởi động HTTP server: {ex.Message}";
                        return false;
                    }
                }
                else
                {
                    inputArg = $"\"{inputSource}\"";
                    var domain = ExtractDomain(inputSource);

                    if (!string.IsNullOrEmpty(domain) && domain.Contains("anime"))
                    {
                        try
                        {
                            var convertedM3u8 = await FetchAnimevietsubApiForUrl(inputSource, cancellationToken);
                            if (!string.IsNullOrEmpty(convertedM3u8))
                            {
                                m3u8TextFromUrl = convertedM3u8;
                                _httpServer = new LocalHttpServer(convertedM3u8);
                                _httpServer.Start();

                                inputArg = $"\"{_httpServer.PlaylistUrl}\"";
                                Result += $"\n🌐 HTTP Server started at: {_httpServer.PlaylistUrl}";
                            }
                            else
                            {
                                Result = $"⚠️ Không lấy được M3U8 từ API cho {inputSource}, sẽ thử tải trực tiếp";
                            }
                        }
                        catch (Exception ex)
                        {
                            Result = $"❌ Lỗi khởi động HTTP server cho anime: {ex.Message}";
                            return false;
                        }
                    }
                }

                // Build format-specific args
                string formatSelector = "best";
                string? mergeFormat = null;
                var postArgs = new List<string>();

                switch ((PreferredFormat ?? "mp4").ToLowerInvariant())
                {
                    case "mp3":
                        formatSelector = "bestaudio/best";
                        postArgs.Add("--extract-audio");
                        postArgs.Add("--audio-format mp3");
                        postArgs.Add("--audio-quality 0");
                        break;
                    case "m4a":
                        formatSelector = "bestaudio/best";
                        postArgs.Add("--extract-audio");
                        postArgs.Add("--audio-format m4a");
                        postArgs.Add("--audio-quality 0");
                        break;
                    case "mkv":
                        formatSelector = "bestvideo+bestaudio/best";
                        mergeFormat = "mkv";
                        break;
                    case "mp4":
                    default:
                        formatSelector = "best[ext=mp4]/best";
                        mergeFormat = "mp4";
                        break;
                }

                var argsList = new List<string>
                {
                    inputArg,
                    $"-o \"{outputTemplate}\"",
                    $"--format \"{formatSelector}\"",
                };

                if (File.Exists(ffmpegPath))
                {
                    argsList.Add($"--ffmpeg-location \"{ffmpegPath}\"");
                }

                if (!string.IsNullOrEmpty(mergeFormat))
                {
                    argsList.Add($"--merge-output-format {mergeFormat}");
                }

                // Common stability options
                argsList.AddRange(new[]
                {
                    $"--concurrent-fragments \"{MaxWorker}\"",
                    "--fragment-retries 10",
                    "--retries 10",
                    "--no-check-certificate",
                    "--ignore-errors",
                    "--no-continue",
                    "--force-overwrites"
                });

                argsList.AddRange(postArgs);
                argsList.AddRange(headerArgs);
                string args = string.Join(" ", argsList);

                var psi = new ProcessStartInfo
                {
                    FileName = ytDlpPath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                _downloadProcess = new Process { StartInfo = psi };

                _downloadProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        UpdateProgressFromOutput(e.Data, itemIndex, totalItems);
                    }
                };

                _downloadProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        UpdateProgressFromOutput(e.Data, itemIndex, totalItems);
                    }
                };

                _downloadProcess.Start();
                _downloadProcess.BeginOutputReadLine();
                _downloadProcess.BeginErrorReadLine();

                await Task.Run(async () =>
                {
                    while (!_downloadProcess.HasExited && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(100, cancellationToken);
                    }
                }, cancellationToken);

                if (_isPaused || cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                var downloadedFiles = Directory.GetFiles(VideoPath, $"{safeBaseName}.*")
                    .Where(f => !f.EndsWith(".part") && !f.Contains(".part-Frag") && !f.EndsWith(".ytdl"))
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .Take(1);

                string? downloadedFile = downloadedFiles.FirstOrDefault();

                if (downloadedFile != null)
                {
                    var fileInfo = new FileInfo(downloadedFile);
                    string itemPrefix = totalItems > 1 ? $"[{itemIndex}/{totalItems}] " : "";
                    Result = $"{itemPrefix}✅ Hoàn thành! File: {Path.GetFileName(downloadedFile)} ({(fileInfo.Length / 1024 / 1024):F1}MB)";
                    return true;
                }
                else if (_downloadProcess.HasExited && _downloadProcess.ExitCode == 0)
                {
                    string itemPrefix = totalItems > 1 ? $"[{itemIndex}/{totalItems}] " : "";
                    Result = $"{itemPrefix}✅ Đã tải xong!";
                    return true;
                }
                else if (!_isPaused)
                {
                    string itemPrefix = totalItems > 1 ? $"[{itemIndex}/{totalItems}] " : "";
                    Result = $"{itemPrefix}❌ Tải thất bại!";
                    return false;
                }

                return false;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                if (!_isPaused)
                {
                    string itemPrefix = totalItems > 1 ? $"[{itemIndex}/{totalItems}] " : "";
                    Result = $"{itemPrefix}❌ Lỗi: {ex.Message}";
                }
                return false;
            }
            finally
            {
                if (_httpServer != null)
                {
                    try
                    {
                        _httpServer.Stop();
                        _httpServer.Dispose();
                        _httpServer = null;
                    }
                    catch
                    {
                    }
                }
            }
        }

        private async Task PauseDownloadAsync()
        {
            try
            {
                _isPaused = true;
                IsDownloading = false;

                _cancellationTokenSource?.Cancel();

                if (_downloadProcess != null && !_downloadProcess.HasExited)
                {
                    _downloadProcess.Kill(true);
                    _downloadProcess.Dispose();
                    _downloadProcess = null;
                }

                if (_httpServer != null)
                {
                    _httpServer.Stop();
                    _httpServer.Dispose();
                    _httpServer = null;
                }

                if (_playwrightService != null)
                {
                    try
                    {
                        await Task.Run(() => _playwrightService.Dispose());
                    }
                    catch { }
                    _playwrightService = null;
                }

                if (!string.IsNullOrWhiteSpace(VideoPath))
                {
                    CleanupTempFiles();
                }

                Result = "⏸️ Đã dừng tải và xóa file tạm!";
            }
            catch (Exception ex)
            {
                Result = $"❌ Lỗi khi dừng: {ex.Message}";
            }
        }

        private void OpenDonate()
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://donate-trtoan.vercel.app/") { UseShellExecute = true });
            }
            catch
            {
            }
        }

        private void CleanupTempFiles()
        {
            try
            {
                var patterns = new[] { "*.part", "*.part-Frag*", "*.ytdl", "*.temp" };

                foreach (var pattern in patterns)
                {
                    var tempFiles = Directory.GetFiles(VideoPath, pattern, SearchOption.TopDirectoryOnly);
                    foreach (var file in tempFiles)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch
                        {
                        }
                    }
                }

                string namePattern = !string.IsNullOrEmpty(_lastSafeBaseName) ? $"{_lastSafeBaseName}.*" : "video_*.*";
                var videoFiles = Directory.GetFiles(VideoPath, namePattern, SearchOption.TopDirectoryOnly)
                    .Where(f => !Path.GetExtension(f).Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
                                new FileInfo(f).Length < 1024);

                foreach (var file in videoFiles)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        private void UpdateProgressFromOutput(string output, int itemIndex = 1, int totalItems = 1)
        {
            try
            {
                if (output.Contains("[debug]") || output.Contains("Loaded") || output.Contains("Python"))
                {
                    return;
                }

                if (totalItems > 1)
                {
                    Result = $"[{itemIndex}/{totalItems}] {output}";
                }
                else
                {
                    Result = output;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Parse error: {ex.Message}");
            }
        }

        private Dictionary<string, string> ParseHeaders(string headersText)
        {
            var headersDict = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(headersText))
            {
                return new Dictionary<string, string>
                {
                    { "accept", "*/*" },
                    { "accept-language", "en-US,en;q=0.9,vi;q=0.8" },
                    { "cache-control", "no-cache" },
                    { "pragma", "no-cache" },
                    { "sec-ch-ua", "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"133\", \"Chromium\";v=\"133\"" },
                    { "sec-ch-ua-mobile", "?0" },
                    { "sec-fetch-dest", "empty" },
                    { "sec-fetch-mode", "cors" },
                    { "sec-fetch-site", "cross-site" },
                    { "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36" }
                };
            }

            var lines = headersText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (trimmedLine.Contains(":"))
                {
                    var parts = trimmedLine.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();

                        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                        {
                            headersDict[key] = value;
                        }
                    }
                }
            }

            return headersDict;
        }

        private string NormalizeM3U8Content(string content, string? baseUrl)
        {
            if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(baseUrl))
            {
                return content;
            }

            if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var parsedBaseUri))
            {
                return content;
            }

            Uri resolvedBaseUri = parsedBaseUri;
            string lastSegment = parsedBaseUri.Segments.LastOrDefault() ?? string.Empty;
            if (!parsedBaseUri.AbsolutePath.EndsWith("/") && !lastSegment.Contains('.'))
            {
                resolvedBaseUri = new Uri($"{parsedBaseUri.AbsoluteUri}/");
            }

            var lines = content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmedLine = lines[i].Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                {
                    continue;
                }

                if (Uri.TryCreate(trimmedLine, UriKind.Absolute, out _))
                {
                    continue;
                }

                lines[i] = new Uri(resolvedBaseUri, trimmedLine).ToString();
            }

            return string.Join("\r\n", lines);
        }

        private async void CheckSize()
        {
            string targetUrl = "";

            if (IsUrlMode)
            {
                if (string.IsNullOrWhiteSpace(Url))
                {
                    var messageBox = new WpfUiMessageBox
                    {
                        Title = "Thông báo",
                        Content = "❌ Vui lòng nhập URL!"
                    };
                    await messageBox.ShowDialogAsync();
                    return;
                }
                targetUrl = Url;
            }
            else if (IsBatchUrlMode)
            {
                var lines = (BatchUrls ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                var validUrls = lines.Select(l => l.Contains('|') ? l.Split('|')[0].Trim() : l.Trim())
                                     .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith("#"))
                                     .ToList();

                if (validUrls.Count == 0)
                {
                    var messageBox = new WpfUiMessageBox
                    {
                        Title = "Thông báo",
                        Content = "❌ Vui lòng nhập ít nhất một URL hợp lệ trong danh sách!"
                    };
                    await messageBox.ShowDialogAsync();
                    return;
                }

                targetUrl = validUrls.First();
                Result = $"🔍 Danh sách gồm {validUrls.Count} URL. Đang kiểm tra link đầu tiên...";
            }
            else
            {
                var messageBox = new WpfUiMessageBox
                {
                    Title = "Thông báo",
                    Content = "❌ Chức năng kiểm tra kích thước chỉ khả dụng cho chế độ URL hoặc Danh sách URL!"
                };
                await messageBox.ShowDialogAsync();
                return;
            }

            await SaveSettingsAsync();

            Result = $"🔍 Đang kiểm tra kích thước của: {targetUrl}";

            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(60);

                    var headersDict = ParseHeaders(Headers);
                    foreach (var header in headersDict)
                    {
                        try
                        {
                            httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                        }
                        catch { }
                    }

                    if (targetUrl.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
                    {
                        await CheckM3U8Size(httpClient, targetUrl);
                    }
                    else
                    {
                        await CheckNormalFileSize(httpClient, targetUrl);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                Result = "⏱️ Timeout! Vui lòng thử lại.";
            }
            catch (Exception ex)
            {
                Result = $"❌ Lỗi: {ex.Message}";
            }
        }

        private async Task CheckM3U8Size(HttpClient httpClient, string m3u8Url)
        {
            Result = "📋 Đang parse M3U8 playlist...";

            var m3u8Content = await httpClient.GetStringAsync(m3u8Url);
            var lines = m3u8Content.Split('\n');
            var segmentUrls = new List<string>();
            var baseUri = new Uri(m3u8Url);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (!trimmedLine.StartsWith("#") && !string.IsNullOrEmpty(trimmedLine))
                {
                    string segmentUrl = trimmedLine.StartsWith("http")
                        ? trimmedLine
                        : new Uri(baseUri, trimmedLine).ToString();
                    segmentUrls.Add(segmentUrl);
                }
            }

            if (segmentUrls.Count == 0)
            {
                Result = "❌ Không tìm thấy segment nào trong M3U8";
                return;
            }

            Result = $"📊 Tìm thấy {segmentUrls.Count} segments. Đang kiểm tra...";

            int maxCheck = segmentUrls.Count == 1 ? 1 : Math.Min(3, segmentUrls.Count);
            long totalSize = 0;
            int checkedCount = 0;

            for (int i = 0; i < maxCheck; i++)
            {
                try
                {
                    var headResponse = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, segmentUrls[i]));
                    if (headResponse.IsSuccessStatusCode && headResponse.Content.Headers.ContentLength.HasValue)
                    {
                        totalSize += headResponse.Content.Headers.ContentLength.Value;
                        checkedCount++;
                    }
                }
                catch { }
            }

            if (checkedCount > 0)
            {
                if (segmentUrls.Count == 1)
                {
                    Result = $"📊 Ước tính kích thước: 5MB (1 segments)";
                }
                else
                {
                    double avgSegmentSize = (double)totalSize / checkedCount;
                    double estimatedTotalSize = segmentUrls.Count == 1 ? totalSize : avgSegmentSize * segmentUrls.Count;

                    double sizeMB = estimatedTotalSize / (1024.0 * 1024.0);
                    string sizeText = sizeMB >= 1024
                        ? $"{sizeMB / 1024:F2} GB"
                        : $"{sizeMB:F2} MB";

                    string prefix = "Ước tính kích thước";
                    Result = $"📊 {prefix}: {sizeText} ({segmentUrls.Count} segments)";
                }
            }
            else if (segmentUrls.Count > 0)
            {
                Result = $"{segmentUrls.Count} segments";
            }
            else
            {
                Result = $"⚠️ Không thể xác định kích thước ({segmentUrls.Count} segments)";
            }
        }

        private async Task CheckNormalFileSize(HttpClient httpClient, string url)
        {
            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));

            if (response.IsSuccessStatusCode)
            {
                if (response.Content.Headers.ContentLength.HasValue)
                {
                    long sizeBytes = response.Content.Headers.ContentLength.Value;
                    double sizeMB = sizeBytes / (1024.0 * 1024.0);
                    double sizeGB = sizeMB / 1024.0;

                    string sizeText = sizeGB >= 1
                        ? $"{sizeGB:F2} GB"
                        : $"{sizeMB:F2} MB";

                    Result = $"📊 Kích thước file: {sizeText} ({sizeBytes:N0} bytes)";
                }
                else
                {
                    Result = "⚠️ Không thể xác định kích thước - Server không cung cấp thông tin Content-Length";
                }
            }
            else
            {
                Result = $"❌ Không thể truy cập URL! Mã lỗi: {response.StatusCode}";
            }
        }

        private async Task BrowseFolder()
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Chọn thư mục lưu video";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    VideoPath = dialog.SelectedPath;
                    await SaveSettingsAsync();
                }
            }
        }

        private static int? ExtractAnimevietsubIdFromUrl(string url)
        {
            try
            {
                var match = System.Text.RegularExpressions.Regex.Match(url, @"-a(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var id))
                {
                    return id;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<string?> FetchAnimevietsubApiForUrl(string targetUrl, CancellationToken cancellationToken)
        {
            var domain = ExtractDomain(targetUrl);
            if (string.IsNullOrEmpty(domain) || !domain.Contains("anime"))
            {
                return null;
            }

            try
            {
                if (_playwrightService == null)
                {
                    _playwrightService = new PlaywrightService();
                    _playwrightService.LogMessage += OnPlaywrightLogMessage;
                    _playwrightService.ErrorOccurred += OnPlaywrightError;
                }

                _playwrightService.BatchSize = BatchSize;
                _playwrightService.TargetDomain = domain;

                bool isInstalled = await CheckPlaywrightInstallationAsync();
                if (!isInstalled)
                {
                    Result = "❌ Playwright chưa được cài đặt đúng cách";
                    return null;
                }

                bool initialized = await _playwrightService.InitializeAsync();
                if (!initialized)
                {
                    Result = "❌ Không thể khởi tạo Playwright";
                    return null;
                }

                var id = ExtractAnimevietsubIdFromUrl(targetUrl);
                if (id == null)
                {
                    Result = "❌ Không thể parse ID từ URL";
                    return null;
                }

                string token = "";
                var html = await _playwrightService.DownloadHtmlFromUrlAsync(targetUrl);
                if (!string.IsNullOrEmpty(html))
                {
                    token = ExtractTokenFromHtml(html) ?? "";
                }

                if (string.IsNullOrEmpty(token))
                {
                    token = _extractedToken;
                }

                if (string.IsNullOrEmpty(token))
                {
                    Result = "❌ Không có token để gọi API";
                    return null;
                }

                string apiUrl = $"https://{domain}/ajax/player";

                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(30);

                    httpClient.DefaultRequestHeaders.Accept.Clear();
                    httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/javascript, */*; q=0.01");
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-requested-with", "XMLHttpRequest");
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("origin", $"https://{domain}");
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("referer", targetUrl);
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36");

                    var cookies = await _playwrightService.GetCookiesHeaderForUrlAsync($"https://{domain}/");
                    if (!string.IsNullOrEmpty(cookies))
                    {
                        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookies);
                    }

                    var content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("link", token),
                        new KeyValuePair<string, string>("id", id.Value.ToString()),
                    });

                    var response = await httpClient.PostAsync(apiUrl, content, cancellationToken);
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        Result = $"❌ API lỗi: {(int)response.StatusCode} - {response.ReasonPhrase}\n{body}";
                        return null;
                    }

                    string? fileValue = null;
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("link", out var linkArray) && linkArray.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            if (linkArray.GetArrayLength() > 0)
                            {
                                var first = linkArray[0];
                                if (first.TryGetProperty("file", out var fileProp))
                                {
                                    fileValue = fileProp.GetString();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Result = $"❌ Lỗi parse JSON: {ex.Message}\n{body}";
                        return null;
                    }

                    if (!string.IsNullOrEmpty(fileValue))
                    {
                        Result = $"✅ file: {fileValue}";

                        try
                        {
                            var playlist = await m3u8Downloader.Services.M3U8Processor.ProcessM3U8DataAsync(fileValue);
                            if (playlist != null && !string.IsNullOrEmpty(playlist.Content))
                            {
                                var converted = await _playwrightService.ConvertM3U8ContentAsync(playlist.Content, cancellationToken);
                                return converted;
                            }
                            else
                            {
                                Result = "⚠️ Không thể xử lý M3U8 từ file";
                            }
                        }
                        catch (Exception ex)
                        {
                            Result = $"❌ Lỗi xử lý M3U8: {ex.Message}";
                        }
                    }
                    else
                    {
                        Result = $"⚠️ Không tìm thấy field 'file'\n{body}";
                    }
                }
            }
            catch (Exception ex)
            {
                Result = $"❌ Lỗi gọi API: {ex.Message}";
            }

            return null;
        }

        private async Task FetchAnimevietsubApi()
        {
            if (string.IsNullOrWhiteSpace(Url))
            {
                Result = "❌ Vui lòng nhập URL";
                return;
            }

            var converted = await FetchAnimevietsubApiForUrl(Url, _cancellationTokenSource?.Token ?? CancellationToken.None);
            if (!string.IsNullOrEmpty(converted))
            {
                m3u8TextFromUrl = converted;
                Result = "✅ Đã xử lý và chuyển đổi M3U8 thành công!";
            }
        }

        private async Task<bool> CheckPlaywrightInstallationAsync()
        {
            try
            {
                using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
                return true;
            }
            catch (Microsoft.Playwright.PlaywrightException ex)
            {
                OnPlaywrightError(this, $"Playwright Error: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                OnPlaywrightError(this, $"Installation Check Error: {ex.Message}");
                return false;
            }
        }

        private string? ExtractTokenFromHtml(string html)
        {
            try
            {
                var scriptPattern = @"AnimeVsub\('([^']+)'";
                var match = System.Text.RegularExpressions.Regex.Match(html, scriptPattern);

                if (match.Success && match.Groups.Count > 1)
                {
                    string token = match.Groups[1].Value;
                    OnPlaywrightLogMessage(this, $"🔍 Tìm thấy token: {token.Substring(0, Math.Min(20, token.Length))}...");
                    return token;
                }

                var alternativePattern = @"AnimeVsub\(""([^""]+)""";
                var altMatch = System.Text.RegularExpressions.Regex.Match(html, alternativePattern);

                if (altMatch.Success && altMatch.Groups.Count > 1)
                {
                    string token = altMatch.Groups[1].Value;
                    OnPlaywrightLogMessage(this, $"🔍 Tìm thấy token (pattern 2): {token.Substring(0, Math.Min(20, token.Length))}...");
                    return token;
                }

                OnPlaywrightLogMessage(this, "⚠️ Không tìm thấy token trong script");
                return null;
            }
            catch (Exception ex)
            {
                OnPlaywrightError(this, $"Lỗi trích xuất token: {ex.Message}");
                return null;
            }
        }

        private void OnPlaywrightLogMessage(object? sender, string message)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Result = message;
            });
        }

        private void OnPlaywrightError(object? sender, string error)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Result = error;
            });
        }

        public void Dispose()
        {
            _playwrightService?.Dispose();
        }
    }
}
