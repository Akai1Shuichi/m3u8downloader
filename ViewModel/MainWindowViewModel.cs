using m3u8Downloader.Model;
using m3u8Downloader.MVVM;
using m3u8Downloader.Service;
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
        private bool _isPaused = false;
        private bool _isDownloading = false;
        private CancellationTokenSource _cancellationTokenSource;
        private Config _config;
        private readonly ConfigService _configService;


        private string _url;

        public string Url
        {
            get { return _url; }
            set { _url = value; OnPropertyChanged(); }
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

        public MainWindowViewModel()
        {
            _configService = new ConfigService();
            _ = LoadSettingsAsync();

            DownloadCommand = new RelayCommand(async _ => await Download());
            CheckSizeCommand = new RelayCommand(_ => CheckSize());
            BrowseFolderCommand = new RelayCommand(_ => BrowseFolder());
            PauseCommand = new RelayCommand(_ => PauseDownload());

        }

        private async Task LoadSettingsAsync()
        {
            try
            {
                _config = await _configService.LoadSettingsAsync();

                // Áp dụng cài đặt vào properties
                Url = _config.Url;
                VideoPath = _config.VideoPath;
                MaxWorker = _config.MaxWorker;
                Headers = _config.Headers;

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
                _config.VideoPath = VideoPath;
                _config.MaxWorker = MaxWorker;
                _config.Headers = Headers;

                await _configService.SaveSettingsAsync(_config);
            }
            catch (Exception ex)
            {
                Result += $"\nLỗi khi lưu cài đặt: {ex.Message}";
            }
        }

        private async Task Download()
        {
            if (string.IsNullOrWhiteSpace(Url) || string.IsNullOrWhiteSpace(VideoPath))
            {
                var messageBox = new WpfUiMessageBox
                {
                    Title = "Thông báo",
                    Content = "❌ Vui lòng nhập URL và chọn thư mục lưu!"
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

            Result = $"Bắt đầu tải video từ: {Url}\nThư mục: {VideoPath}\nSố luồng: {MaxWorker}";

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

                string outputTemplate = Path.Combine(VideoPath, $"video_{DateTime.Now:yyyyMMdd_HHmmss}.%(ext)s");

                var argsList = new List<string> {
            $"\"{Url}\"",
            $"-o \"{outputTemplate}\"",
            "--format \"best[ext=mp4]/best\"",
            "--merge-output-format mp4",
            $"--concurrent-fragments \"{MaxWorker}\"",
            "--fragment-retries 10",
            "--retries 10",
            "--no-check-certificate",
            "--ignore-errors",
        };

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
        }

        private void PauseDownload()
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
            if (string.IsNullOrEmpty(Url)) {
                var messageBox = new WpfUiMessageBox
                {
                    Title = "Thông báo",
                    Content = "❌ Vui lòng nhập URL!"
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

    }
}
