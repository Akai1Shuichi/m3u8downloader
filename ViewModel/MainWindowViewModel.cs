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
        private CancellationTokenSource _cancellationTokenSource;
        private Config _config;
        private readonly ConfigService _configService;
        private LocalHttpServer? _httpServer;
        private PlaywrightService? _playwrightService;

        private bool _isPaused = false;
        private bool _isDownloading = false;
        private string m3u8TextFromUrl = "";
        private string _extractedToken = "";


        private string _url;

        public string Url
        {
            get { return _url; }
            set { _url = value; OnPropertyChanged(); }
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
            get { return _isUrlMode; }
            set 
            { 
                _isUrlMode = value; 
                OnPropertyChanged();
                if (value)
                {
                    IsTextMode = false;
                }
            }
        }

        private bool _isTextMode = false;
        public bool IsTextMode
        {
            get { return _isTextMode; }
            set 
            { 
                _isTextMode = value; 
                OnPropertyChanged();
                if (value)
                {
                    IsUrlMode = false;
                }
            }
        }


        private string _m3u8Text;
        public string M3u8Text
        {
            get { return _m3u8Text; }
            set { _m3u8Text = value; OnPropertyChanged(); }
        }



        private string _videoPath;
        public string VideoPath
        {
            get => _videoPath;
            set { _videoPath = value; OnPropertyChanged(); }
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

        private string _result;
        public string Result
        {
            get => _result;
            set { _result = value; OnPropertyChanged(); }
        }

        private string _headers;

        public string Headers
        {
            get { return _headers; }
            set { _headers = value; OnPropertyChanged(); }
        }

        private string _preferredFormat = "mp4";
        public string PreferredFormat
        {
            get { return _preferredFormat; }
            set { _preferredFormat = value; OnPropertyChanged(); }
        }


        public bool IsDownloading
        {
            get { return _isDownloading; }
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
                M3u8Text = _config.M3u8Text;
                VideoPath = _config.VideoPath;
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
                _config.M3u8Text = M3u8Text;
                _config.VideoPath = VideoPath;
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
            // Validate input based on selected mode
            string inputSource = "";
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
                inputSource = Url;
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
                    return;
                }
                inputSource = M3u8Text;
            }
         

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
            
            // Reset trạng thái
            _isPaused = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            // Lưu cài đặt trước khi tải
            await SaveSettingsAsync();

            string inputType = IsUrlMode ? "URL" : "M3U8 Text";
            Result = $"Bắt đầu tải video từ: {inputType}\nThư mục: {VideoPath}\nSố luồng: {MaxWorker}";
            
            if (!IsUrlMode)
            {
                Result += $"\n📝 M3U8 content length: {inputSource.Length} characters";
            }
            try
            {
                string ytDlpPath = Path.Combine(AppContext.BaseDirectory, "Tools", "yt-dlp", "yt-dlp.exe");

                if (!File.Exists(ytDlpPath))
                {
                    Result = "❌ Không tìm thấy file .exe!";
                    IsDownloading = false;
                    return;
                }

                var headersDict = ParseHeaders(Headers);

                var headerArgs = new List<string>();
                foreach (var header in headersDict)
                {
                    headerArgs.Add($"--add-header \"{header.Key}:{header.Value}\"");
                }

                string prefix = (PreferredFormat == "mp4" || PreferredFormat == "mkv") ? "video" : "audio";
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string outputTemplate = Path.Combine(VideoPath, $"{prefix}_{timestamp}.%(ext)s");

                // Handle different input types
                string inputArg;
                if (IsUrlMode)
                {
                    inputArg = $"\"{inputSource}\"";
                    var domain = CurrentDomain;
                    if (!string.IsNullOrEmpty(domain) && domain.Contains("anime")) {
                        // Start HTTP server
                        try
                        {
                            await FetchAnimevietsubApi();
                            _httpServer = new LocalHttpServer(m3u8TextFromUrl);
                            _httpServer.Start();

                            // Use the HTTP server URL
                            inputArg = $"\"{_httpServer.PlaylistUrl}\"";

                            // Add debug info
                            Result += $"\n🌐 HTTP Server started at: {_httpServer.PlaylistUrl}";
                        }
                        catch (Exception ex)
                        {
                            Result = $"❌ Lỗi khởi động HTTP server: {ex.Message}";
                            IsDownloading = false;
                            return;
                        }
                    }
                }
                else
                {
                    // Start HTTP server
                    try
                    {
                        if (_playwrightService == null)
                        {
                            _playwrightService = new PlaywrightService();
                            _playwrightService.LogMessage += OnPlaywrightLogMessage;
                            _playwrightService.ErrorOccurred += OnPlaywrightError;
                        }
                        var domain = CurrentDomain;
                        _playwrightService.BatchSize = BatchSize;
                        _playwrightService.TargetDomain = domain;
                        bool isInstalled = await CheckPlaywrightInstallationAsync();
                        if (!isInstalled)
                        {
                            Result = "❌ Playwright chưa được cài đặt đúng cách";
                            return;
                        }

                        bool initialized = await _playwrightService.InitializeAsync();
                        if (!initialized)
                        {
                            Result = "❌ Không thể khởi tạo Playwright";
                            return;
                        }

                        if (_playwrightService != null)
                        {
                            _playwrightService.BatchSize = BatchSize;
                        }
                        var converted = await _playwrightService.ConvertM3U8ContentAsync(inputSource, _cancellationTokenSource.Token);
                        m3u8TextFromUrl = converted;
                        _httpServer = new LocalHttpServer(converted);
                        _httpServer.Start();
                        
                        // Use the HTTP server URL
                        inputArg = $"\"{_httpServer.PlaylistUrl}\"";
                        
                        // Add debug info
                        Result += $"\n🌐 HTTP Server started at: {_httpServer.PlaylistUrl}";
                    }
                    catch (Exception ex)
                    {
                        Result = $"❌ Lỗi khởi động HTTP server: {ex.Message}";
                        IsDownloading = false;
                        return;
                    }
                }

                // Build format-specific args
                string formatSelector = "best";
                string? mergeFormat = null;
                var postArgs = new List<string>();

                switch ((PreferredFormat ?? "mp4").ToLowerInvariant())
                {
                    case "mp3":
                        // Extract audio as mp3
                        formatSelector = "bestaudio/best";
                        postArgs.Add("--extract-audio");
                        postArgs.Add("--audio-format mp3");
                        postArgs.Add("--audio-quality 0");
                        break;
                    case "m4a":
                        // Extract audio as m4a (aac)
                        formatSelector = "bestaudio/best";
                        postArgs.Add("--extract-audio");
                        postArgs.Add("--audio-format m4a");
                        postArgs.Add("--audio-quality 0");
                        break;
                    case "mkv":
                        // Prefer MP4 streams if possible, else best, then merge to MKV
                        formatSelector = "bestvideo+bestaudio/best";
                        mergeFormat = "mkv";
                        break;
                    case "mp4":
                    default:
                        // Prefer mp4 output
                        formatSelector = "best[ext=mp4]/best";
                        mergeFormat = "mp4";
                        break;
                }

                var argsList = new List<string> {
            inputArg,
            $"-o \"{outputTemplate}\"",
            $"--format \"{formatSelector}\"",
        };

                if (!string.IsNullOrEmpty(mergeFormat))
                {
                    argsList.Add($"--merge-output-format {mergeFormat}");
                }

                // Common stability options
                argsList.AddRange(new []{
                    $"--concurrent-fragments \"{MaxWorker}\"",
                    "--fragment-retries 10",
                    "--retries 10",
                    "--no-check-certificate",
                    "--ignore-errors"
                });

                // Add post-processing args (audio extraction)
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
                        UpdateProgressFromOutput(e.Data);
                    }
                };

                _downloadProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        UpdateProgressFromOutput(e.Data);
                    }
                };

                _downloadProcess.Start();
                _downloadProcess.BeginOutputReadLine();
                _downloadProcess.BeginErrorReadLine();

                // Sử dụng Task.Run với CancellationToken thay vì WaitForExitAsync
                await Task.Run(async () =>
                {
                    while (!_downloadProcess.HasExited && !_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        await Task.Delay(100, _cancellationTokenSource.Token);
                    }
                }, _cancellationTokenSource.Token);

                // Kiểm tra nếu bị pause thì không hiển thị hoàn thành
                if (_isPaused || _cancellationTokenSource.Token.IsCancellationRequested)
                {
                    IsDownloading = false;
                    return; // Đã được xử lý trong PauseDownload()
                }

                // Kiểm tra file đã được tải về chưa
                var downloadedFiles = Directory.GetFiles(VideoPath, "video_*.*")
                            .Where(f => !f.EndsWith(".part") && !f.Contains(".part-Frag"))
                            .OrderByDescending(f => File.GetCreationTime(f))
                            .Take(1);

                string downloadedFile = downloadedFiles.FirstOrDefault();

                // Nếu có file được tải về thì coi như thành công
                if (downloadedFile != null)
                {
                    var fileInfo = new FileInfo(downloadedFile);
                    Result = $"✅ Hoàn thành! File: {Path.GetFileName(downloadedFile)} ({(fileInfo.Length / 1024 / 1024):F1}MB)";
                }
                else if (_downloadProcess.HasExited && _downloadProcess.ExitCode == 0)
                {
                    Result = "✅ Video đã tải xong!";
                }
                else if (!_isPaused)
                {
                    Result = "❌ Tải video thất bại!";
                }
                
                // Kết thúc tải
                IsDownloading = false;
            }
            catch (OperationCanceledException)
            {
                // Không làm gì, đã được xử lý trong PauseDownload
                IsDownloading = false;
            }
            catch (Exception ex)
            {
                if (!_isPaused)
                {
                    Result = $"❌ Lỗi: {ex.Message}";
                }
                IsDownloading = false;
            }
            finally
            {
                // Cleanup HTTP server if created
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
                        // Ignore cleanup errors
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

                // Cancel token trước
                _cancellationTokenSource?.Cancel();

                if (_downloadProcess != null && !_downloadProcess.HasExited)
                {
                    // Kill process
                    _downloadProcess.Kill(true);
                    _downloadProcess.Dispose();
                    _downloadProcess = null;
                }

                // Stop HTTP server if running
                if (_httpServer != null)
                {
                    _httpServer.Stop();
                    _httpServer.Dispose();
                    _httpServer = null;
                }

                // Dispose Playwright if initialized (await async close to avoid UI freeze)
                if (_playwrightService != null)
                {
                    try
                    {
                        await Task.Run(() => _playwrightService.Dispose());
                    }
                    catch { }
                    _playwrightService = null;
                }

                // Xóa tất cả các file tạm (.part, .part-Frag, .ytdl)
                if (!string.IsNullOrWhiteSpace(VideoPath))
                {
                    CleanupTempFiles();
                }

                Result = "⏸️ Đã dừng tải và xóa file tạm!";
            }
            catch (Exception ex)
            {
                Result = $"❌ Lỗi khi pause: {ex.Message}";
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
                // Ignore open url errors
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
                            // Ignore nếu file đang được sử dụng
                        }
                    }
                }

                // Xóa các file video chưa hoàn thành (có thể detect bằng size hoặc tên)
                var videoFiles = Directory.GetFiles(VideoPath, "video_*.*", SearchOption.TopDirectoryOnly)
                                         .Where(f => !Path.GetExtension(f).Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
                                                   new FileInfo(f).Length < 1024); // File < 1KB coi như chưa hoàn thành

                foreach (var file in videoFiles)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // Ignore
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private void UpdateProgressFromOutput(string output)
        {
            try
            {
                if (output.Contains("[debug]") || output.Contains("Loaded") || output.Contains("Python"))
                {
                    return;
                }

                Result = output;

            }
            catch (Exception ex)
            {
                // Bỏ qua lỗi parse để không làm gián đoạn quá trình
                Console.WriteLine($"Parse error: {ex.Message}");
            }
        }

        private Dictionary<string, string> ParseHeaders(string headersText)
        {
            var headersDict = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(headersText))
            {
                // Default headers nếu không có
                return new Dictionary<string, string> {
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

            // Split theo \r\n hoặc \n
            var lines = headersText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (trimmedLine.Contains(":"))
                {
                    var parts = trimmedLine.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        // Bỏ khoảng trắng, không cần bỏ quotes vì format mới không có
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

        private async void CheckSize()
        {
            // Only allow size check for URL mode
            if (!IsUrlMode || string.IsNullOrEmpty(Url)) {
                var messageBox = new WpfUiMessageBox
                {
                    Title = "Thông báo",
                    Content = "❌ Chức năng kiểm tra kích thước chỉ khả dụng cho chế độ URL!"
                };
                await messageBox.ShowDialogAsync();
                return;
            }

            await SaveSettingsAsync();

            Result = $"🔍 Đang kiểm tra kích thước của: {Url}";

            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(60);

                    var headersDict = ParseHeaders(Headers);

                    // Add headers
                    foreach (var header in headersDict)
                    {
                        try
                        {
                            httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                        }
                        catch { }
                    }

                    if (Url.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
                    {
                        // Đây là HLS playlist
                        await CheckM3U8Size(httpClient, Url);
                    }
                    else
                    {
                        // File thông thường
                        await CheckNormalFileSize(httpClient, Url);
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

            // Tối ưu: chỉ check 1 segment nếu chỉ có 1, hoặc tối đa 3 segment
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
                // Sử dụng regex để tìm phần -a<digits> trong URL
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


        private async Task FetchAnimevietsubApi()
        {
            if (string.IsNullOrWhiteSpace(Url))
            {
                Result = "❌ Vui lòng nhập URL";
                return;
            }

            var domain = CurrentDomain;
            if (string.IsNullOrEmpty(domain) || !domain.Contains("anime"))
            {
                Result = $"❌ URL phải thuộc domain anime (hiện tại: {domain})";
                return;
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
                    return;
                }

                bool initialized = await _playwrightService.InitializeAsync();
                if (!initialized)
                {
                    Result = "❌ Không thể khởi tạo Playwright";
                    return;
                }

                var id = ExtractAnimevietsubIdFromUrl(Url);
                if (id == null)
                {
                    Result = "❌ Không thể parse ID từ URL";
                    return;
                }

                // Ensure token exists (try to extract if empty)
                if (string.IsNullOrEmpty(_extractedToken))
                {
                    var html = await _playwrightService.DownloadHtmlFromUrlAsync(Url);
                    if (!string.IsNullOrEmpty(html))
                    {
                        var token = ExtractTokenFromHtml(html);
                        if (!string.IsNullOrEmpty(token))
                        {
                            _extractedToken = token;
                        }
                    }
                }

                if (string.IsNullOrEmpty(_extractedToken))
                {
                    Result = "❌ Không có token để gọi API";
                    return;
                }

                string apiUrl = $"https://{domain}/ajax/player";

                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(30);

                    // Build headers similar to the curl
                    httpClient.DefaultRequestHeaders.Accept.Clear();
                    httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/javascript, */*; q=0.01");
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-requested-with", "XMLHttpRequest");
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("origin", $"https://{domain}");
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("referer", Url);
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36");

                    // Try attach cookies from Playwright session
                    var cookies = await _playwrightService.GetCookiesHeaderForUrlAsync($"https://{domain}/");
                    if (!string.IsNullOrEmpty(cookies))
                    {
                        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookies);
                    }

                    var content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("link", _extractedToken),
                        new KeyValuePair<string, string>("id", id.Value.ToString()),
                    });

                    var response = await httpClient.PostAsync(apiUrl, content);
                    var body = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        Result = $"❌ API lỗi: {(int)response.StatusCode} - {response.ReasonPhrase}\n{body}";
                        return;
                    }

                    // Parse minimal JSON to get link[0].file
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
                        return;
                    }

                    if (!string.IsNullOrEmpty(fileValue))
                    {
                        Result = $"✅ file: {fileValue}";

                        try
                        {
                            // Process encrypted file value to m3u8
                            var playlist = await m3u8Downloader.Services.M3U8Processor.ProcessM3U8DataAsync(fileValue);
                            if (playlist != null && !string.IsNullOrEmpty(playlist.Content))
                            {
                                // Optional: convert redirecting googleapis URLs to final URLs
                                var converted = await _playwrightService.ConvertM3U8ContentAsync(playlist.Content, _cancellationTokenSource.Token);
                                m3u8TextFromUrl = converted;
                                Result = "✅ Đã xử lý và chuyển đổi M3U8 thành công!";
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
        }

        private async Task<bool> CheckPlaywrightInstallationAsync()
        {
            try
            {
                // Thử tạo Playwright instance để kiểm tra
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
                // Tìm script chứa AnimeVsub function
                var scriptPattern = @"AnimeVsub\('([^']+)'";
                var match = System.Text.RegularExpressions.Regex.Match(html, scriptPattern);
                
                if (match.Success && match.Groups.Count > 1)
                {
                    string token = match.Groups[1].Value;
                    OnPlaywrightLogMessage(this, $"🔍 Tìm thấy token: {token.Substring(0, Math.Min(20, token.Length))}...");
                    return token;
                }

                // Thử pattern khác nếu không tìm thấy
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
            // Cập nhật UI thread-safe
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Result = message;
            });
        }

        private void OnPlaywrightError(object? sender, string error)
        {
            // Cập nhật UI thread-safe
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Result = error;
            });
        }

        // Dispose method để cleanup PlaywrightService
        public void Dispose()
        {
            _playwrightService?.Dispose();
        }

    }
}
