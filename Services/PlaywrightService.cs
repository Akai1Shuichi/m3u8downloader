using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace m3u8Downloader.Services
{
    public class PlaywrightService
    {
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private IBrowserContext? _context;
        private IPage? _sharedPage;

        public event EventHandler<string>? LogMessage;
        public event EventHandler<string>? ErrorOccurred;

        // Configuration
        private const int REQUEST_DELAY_MS = 500;
        public int BatchSize { get; set; } = 10;
        public string TargetDomain { get; set; } = "animevietsub.show";
        private const int RETRY_ATTEMPTS = 2;

        // Progress tracking
        private int _processedCount = 0;
        private int _totalCount = 0;
        private int _successCount = 0;
        private int _errorCount = 0;

        private void OnLogMessage(string message)
        {
            LogMessage?.Invoke(this, message);
            Console.WriteLine(message);
        }

        private void OnErrorOccurred(string error)
        {
            ErrorOccurred?.Invoke(this, error);
            Console.WriteLine(error);
        }

        private void UpdateProgress()
        {
            if (_totalCount == 0) return;
            double percent = ((double)_processedCount / _totalCount) * 100;
            OnLogMessage($"Progress: {_processedCount}/{_totalCount} ({percent:F1}%) | Success: {_successCount} | Errors: {_errorCount}");
        }


        public async Task<bool> InitializeAsync()
        {
            try
            {
                OnLogMessage("🚀 Khởi tạo Playwright...");

                _playwright = await Playwright.CreateAsync();
                OnLogMessage("✅ Playwright instance created");

                string? chromePath = FindChromeExecutable();
                if (chromePath == null)
                {
                    OnErrorOccurred("❌ Không tìm thấy Chrome browser");
                    return false;
                }

                OnLogMessage($"🌐 Tìm thấy Chrome tại: {chromePath}");
                OnLogMessage("🌐 Đang khởi động Chrome browser...");

                _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true,
                    ExecutablePath = chromePath,
                    Timeout = 30000,
                    // ⭐ QUAN TRỌNG: Tắt automation detection
                    IgnoreDefaultArgs = new[] { "--enable-automation" },
                    Args = new[]
                    {
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage",
                "--disable-web-security",
                "--disable-features=IsolateOrigins,site-per-process",
                "--disable-blink-features=AutomationControlled",
                "--disable-background-networking",
                "--disable-renderer-backgrounding",
                "--disable-extensions",
                "--no-first-run",
                "--no-default-browser-check",
                "--disable-gpu",
                "--disable-software-rasterizer",
                "--disable-background-timer-throttling",
                "--disable-backgrounding-occluded-windows",
                "--disable-infobars",
                "--disable-notifications",
                "--disable-popup-blocking",
                "--disable-features=TranslateUI",
                "--disable-ipc-flooding-protection",
                "--window-size=1920,1080",
                "--password-store=basic",
                "--use-mock-keychain"
            },
                });

                _context = await _browser.NewContextAsync(new BrowserNewContextOptions
                {
                    IgnoreHTTPSErrors = true,
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                                "AppleWebKit/537.36 (KHTML, like Gecko) " +
                                "Chrome/120.0.0.0 Safari/537.36",
                    ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                    // ⭐ Thêm extra HTTP headers
                    ExtraHTTPHeaders = new Dictionary<string, string>
                    {
                        ["Accept-Language"] = "en-US,en;q=0.9",
                        ["sec-ch-ua"] = "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\"",
                        ["sec-ch-ua-mobile"] = "?0",
                        ["sec-ch-ua-platform"] = "\"Windows\""
                    }
                });

                _sharedPage = await _context.NewPageAsync();
                _sharedPage.SetDefaultTimeout(30000);
                _sharedPage.SetDefaultNavigationTimeout(30000);

                // ⭐ QUAN TRỌNG: Inject script để ẩn WebDriver
                await _sharedPage.AddInitScriptAsync(@"
            // Ẩn webdriver property
            Object.defineProperty(navigator, 'webdriver', {
                get: () => undefined
            });
            
            // Thêm chrome object
            window.navigator.chrome = {
                runtime: {}
            };
            
            // Fake plugins
            Object.defineProperty(navigator, 'plugins', {
                get: () => [1, 2, 3, 4, 5]
            });
            
            // Fake languages
            Object.defineProperty(navigator, 'languages', {
                get: () => ['en-US', 'en']
            });
            
            // Fake permissions
            const originalQuery = window.navigator.permissions.query;
            window.navigator.permissions.query = (parameters) => (
                parameters.name === 'notifications' ?
                    Promise.resolve({ state: Notification.permission }) :
                    originalQuery(parameters)
            );
        ");

                // ⭐ Setup request interception để thêm headers
                await _sharedPage.RouteAsync("**/*", async route =>
                {
                    var request = route.Request;
                    var headers = new Dictionary<string, string>(request.Headers);

                    // Chỉ modify headers cho requests đến stream.googleapiscdn.com
                    if (request.Url.Contains("stream.googleapiscdn.com"))
                    {
                        headers["Accept"] = "*/*";
                        headers["Accept-Language"] = "en-US,en;q=0.9";
                        headers["Origin"] = $"https://{TargetDomain}";
                        headers["Referer"] = $"https://{TargetDomain}/";
                        headers["Sec-Fetch-Dest"] = "empty";
                        headers["Sec-Fetch-Mode"] = "cors";
                        headers["Sec-Fetch-Site"] = "cross-site";

                        await route.ContinueAsync(new RouteContinueOptions { Headers = headers });
                    }
                    else
                    {
                        await route.ContinueAsync();
                    }
                });

                // ⭐ Establish browser context như Node.js
                OnLogMessage("🌍 Establishing browser context...");
                try
                {
                    await _sharedPage.GotoAsync($"https://{TargetDomain}/", new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded,
                        Timeout = 30000
                    });
                    await Task.Delay(3000); // Đợi như Node.js
                    OnLogMessage("✅ Browser context established");
                }
                catch (Exception ex)
                {
                    OnLogMessage($"⚠️ Warning: Could not establish context, continuing anyway... ({ex.Message})");
                }

                OnLogMessage("✅ Playwright đã được khởi tạo thành công với Chrome");
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"❌ Lỗi khởi tạo Playwright: {ex.Message}");
                return false;
            }
        }

        private string? FindChromeExecutable()
        {
            string[] possiblePaths = {
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe")
            };

            foreach (string path in possiblePaths)
                if (File.Exists(path))
                    return path;

            return null;
        }

        /// <summary>
        /// Nhận trực tiếp M3U8 content, convert toàn bộ các URL dạng stream.googleapiscdn.com
        /// Có retry lần 2 cho các URL failed
        /// </summary>
        public async Task<string?> ConvertM3U8ContentAsync(string m3u8Content, System.Threading.CancellationToken cancellationToken = default)
        {
            if (_sharedPage == null)
            {
                OnErrorOccurred("❌ Playwright chưa được khởi tạo");
                return null;
            }

            try
            {
                // Helper: extract target URLs from content
                List<string> ExtractTargetUrls(string content)
                {
                    return content.Split('\n')
                        .Select(l => l.Trim())
                        .Where(l => l.StartsWith("https://stream.googleapiscdn.com/") && l.EndsWith(".html"))
                        .Distinct()
                        .ToList();
                }

                var allResults = new List<UrlResult>();
                string convertedContent = m3u8Content;

                // Up to 3 attempts, avoiding duplicated code between attempts
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    var targetUrls = attempt == 1
                        ? ExtractTargetUrls(m3u8Content)
                        : ExtractTargetUrls(convertedContent);

                    if (attempt == 1)
                        OnLogMessage("🔄 RETRY LẦN 1: Bắt đầu convert M3U8 content...");
                    else
                        OnLogMessage($"\n🔄 RETRY LẦN {attempt}: Kiểm tra các URL failed để retry...");

                    if (targetUrls.Count == 0)
                    {
                        if (attempt == 1)
                            OnLogMessage("✅ Không có URL nào cần convert, trả lại nội dung gốc");
                        else
                            OnLogMessage("✅ Không có URL nào cần retry");
                        if (attempt == 1)
                            return m3u8Content;
                        break;
                    }

                    // Reset counters per attempt
                    _processedCount = 0;
                    _totalCount = targetUrls.Count;
                    _successCount = 0;
                    _errorCount = 0;

                    var batches = SplitIntoBatches(targetUrls, BatchSize);
                    if (attempt == 1)
                        OnLogMessage($"📦 Chia thành {batches.Count} batches với {BatchSize} URL mỗi batch");
                    else
                        OnLogMessage($"📦 Chia thành {batches.Count} batches cho retry");

                    var attemptResults = new List<UrlResult>();
                    for (int i = 0; i < batches.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var batchResults = await ProcessBatchAsync(batches[i], i);
                        attemptResults.AddRange(batchResults);
                        if (i < batches.Count - 1)
                            await Task.Delay(REQUEST_DELAY_MS, cancellationToken);
                    }

                    // Update mapping for this attempt
                    var attemptMapping = attemptResults
                        .Where(r => r.Success && !string.IsNullOrEmpty(r.FinalUrl))
                        .ToDictionary(r => r.SourceUrl, r => r.FinalUrl!);

                    if (attempt == 1)
                        OnLogMessage($"📊 Lần 1: Đã convert {attemptMapping.Count}/{_totalCount} URL");
                    else
                        OnLogMessage($"📊 Lần {attempt}: Đã convert thêm {attemptMapping.Count}/{_totalCount} URL");

                    // Replace content and merge results
                    convertedContent = ReplaceUrlsInContent(convertedContent, attemptMapping);
                    allResults.AddRange(attemptResults);

                    // If nothing failed this attempt, stop early
                    var remainingAfterAttempt = ExtractTargetUrls(convertedContent).Count;
                    if (remainingAfterAttempt == 0)
                    {
                        OnLogMessage("✅ Tất cả URL đã được convert, dừng retry sớm");
                        break;
                    }
                }

                // ==================== SUMMARY ====================
                var totalSuccess = allResults.Count(r => r.Success);
                var totalProcessed = allResults.Count;
                var successRate = totalProcessed > 0 ? (totalSuccess * 100.0 / totalProcessed) : 0;

                OnLogMessage("\n📊 Tổng kết:");
                OnLogMessage($"🎯 Tổng số URL đã xử lý: {totalProcessed}");
                OnLogMessage($"✅ Thành công: {totalSuccess}");
                OnLogMessage($"❌ Thất bại: {totalProcessed - totalSuccess}");
                OnLogMessage($"📈 Tỷ lệ thành công: {successRate:F1}%");

                OnLogMessage("✅ Convert hoàn tất");
                return convertedContent;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"❌ Lỗi convert M3U8: {ex.Message}");
                return null;
            }
        }

        private async Task<List<UrlResult>> ProcessBatchAsync(List<string> urls, int batchIndex)
        {
            OnLogMessage($"\n⚙️ Xử lý batch {batchIndex + 1} ({urls.Count} URL)");

            var tasks = urls.Select(async url =>
            {
                try
                {
                    var response = await MakeBrowserRequestAsync(url);

                    var result = new UrlResult
                    {
                        SourceUrl = url,
                        FinalUrl = response.FinalUrl,
                        Status = response.Status,
                        Success = response.Success,
                        Error = response.Error,
                        Timestamp = DateTime.UtcNow
                    };

                    _processedCount++;
                    //if (result.Success) _successCount++; else _errorCount++;
                    if (url != response.FinalUrl)
                    {
                        _successCount++;
                    }
                    else if (url == response.FinalUrl)
                    {
                        _errorCount++;
                    }
                    UpdateProgress();
                    return result;
                }
                catch (Exception ex)
                {
                    _processedCount++;
                    _errorCount++;
                    UpdateProgress();
                    return new UrlResult { SourceUrl = url, Error = ex.Message, Success = false };
                }
            });

            return (await Task.WhenAll(tasks)).ToList();
        }

        private async Task<BrowserResponse> MakeBrowserRequestAsync(string url, int retryCount = 0)
        {
            if (_sharedPage == null)
                throw new InvalidOperationException("Page not initialized");
            try
            {
                var result = await _sharedPage.EvaluateAsync<BrowserResponse>($@"
                async (targetUrl) => {{
                    try {{
                        const fetchOptions = [
                          {{
                            method: 'GET',
                            mode: 'cors',
                            credentials: 'omit',
                            headers: {{
                              'Accept': '*/*',
                              'Accept-Language': 'en-US,en;q=0.9',
                              'Origin': 'https://{TargetDomain}',
                              'Referer': 'https://{TargetDomain}/',
                              'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
                            }}
                          }},
                          {{
                            method: 'GET',
                            mode: 'no-cors',
                            credentials: 'omit'
                          }}
                        ];
                        for (let i = 0; i < fetchOptions.length; i++) {{
                          try {{
                            const response = await fetch(targetUrl, fetchOptions[i]);
                            let responseText = '';
                            try {{
                              responseText = await response.text();
                            }} catch (e) {{
                              responseText = `[Cannot read response body in ${{response.type}} mode]`;
                            }}
                            return {{
                              url: targetUrl,
                              finalUrl: response.url,
                              status: response.status,
                              data: responseText,
                              redirected: response.redirected,
                              type: response.type,
                              success: true
                            }};
                          }} catch (error) {{
                            continue;
                          }}
                        }}
                        throw new Error('All fetch options failed');
                    }} catch (error) {{
                        return {{ url: targetUrl, error: error.message, success: false }};
                    }}
                }}
                ", url);

                // Retry 429 với exponential backoff
                if (result.Status == 429 && retryCount < 2)
                {
                    int delay = (int)Math.Pow(2, retryCount) * 2000;
                    OnLogMessage($"🔄 Got 429 for {url}, retrying in {delay}ms (attempt {retryCount + 1}/2)");
                    await Task.Delay(delay);
                    return await MakeBrowserRequestAsync(url, retryCount + 1);
                }

                // Retry cho các lỗi khác
                if (!result.Success && retryCount < RETRY_ATTEMPTS)
                {
                    await Task.Delay(1000);
                    return await MakeBrowserRequestAsync(url, retryCount + 1);
                }

                return result;
            }
            catch (Exception ex)
            {
                if (retryCount < RETRY_ATTEMPTS)
                {
                    await Task.Delay(1000);
                    return await MakeBrowserRequestAsync(url, retryCount + 1);
                }
                return new BrowserResponse { Url = url, Error = ex.Message, Success = false };
            }
        }

        private List<List<string>> SplitIntoBatches(List<string> list, int batchSize)
        {
            var result = new List<List<string>>();
            for (int i = 0; i < list.Count; i += batchSize)
                result.Add(list.Skip(i).Take(batchSize).ToList());
            return result;
        }

        private string ReplaceUrlsInContent(string content, Dictionary<string, string> urlMapping)
        {
            var lines = content.Split('\n');
            var result = new List<string>();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (urlMapping.TryGetValue(trimmed, out var newUrl))
                {
                    OnLogMessage($"🔁 Thay {trimmed} → {newUrl}");
                    result.Add(newUrl);
                }
                else
                    result.Add(line);
            }

            return string.Join("\n", result);
        }

        public async Task<string?> GetCookiesHeaderForUrlAsync(string url)
        {
            if (_context == null)
            {
                OnErrorOccurred("❌ Playwright chưa được khởi tạo");
                return null;
            }

            try
            {
                var cookies = await _context.CookiesAsync(new[] { url });
                if (cookies == null || cookies.Count == 0)
                {
                    return null;
                }

                var cookieHeader = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
                return cookieHeader;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"❌ Lỗi lấy cookie: {ex.Message}");
                return null;
            }
        }

        public async Task<string?> DownloadHtmlFromUrlAsync(string url)
        {
            if (_context == null)
            {
                OnErrorOccurred("❌ Playwright chưa được khởi tạo");
                return null;
            }

            if (!url.Contains(TargetDomain))
            {
                OnLogMessage($"⛔ URL không thuộc {TargetDomain}, bỏ qua tải HTML.");
                return null;
            }

            try
            {
                OnLogMessage($"🌐 Đang xử lý URL: {url}");
                var page = await _context.NewPageAsync();

                await page.RouteAsync("**/*", async route =>
                {
                    var type = route.Request.ResourceType;
                    if (type is "image" or "media" or "font" or "stylesheet")
                        await route.AbortAsync();
                    else
                        await route.ContinueAsync();
                });

                OnLogMessage("🌍 Đang tải trang...");
                var response = await page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 30000
                });

                if (response == null || !response.Ok)
                {
                    OnErrorOccurred($"⚠️ Response lỗi: {response?.Status}");
                    return null;
                }

                OnLogMessage("⏳ Đang đợi trang load hoàn tất...");
                await Task.Delay(3000);

                string html = await page.ContentAsync();
                await page.CloseAsync();

                OnLogMessage("✅ Đã tải HTML thành công");
                return html;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"❌ Lỗi tải HTML: {ex.Message}");
                return null;
            }
        }

        public async Task DisposeAsync()
        {
            try
            {
                if (_sharedPage != null)
                    await _sharedPage.CloseAsync();

                if (_browser != null)
                {
                    await _browser.CloseAsync();
                    OnLogMessage("🔒 Browser đã được đóng");
                }

                _playwright?.Dispose();
                OnLogMessage("🧹 Playwright đã được giải phóng");
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"❌ Lỗi khi đóng Playwright: {ex.Message}");
            }
        }

        public void Dispose()
        {
            DisposeAsync().GetAwaiter().GetResult();
        }
    }

    // Models
    public class BrowserResponse
    {
        public string Url { get; set; } = "";
        public string? FinalUrl { get; set; }
        public int Status { get; set; }
        public string? Data { get; set; }
        public bool Redirected { get; set; }
        public string? Type { get; set; }
        public bool Success { get; set; }
        public string? Error { get; set; }
    }

    public class UrlResult
    {
        public string SourceUrl { get; set; } = "";
        public string? FinalUrl { get; set; }
        public int Status { get; set; }
        public bool Success { get; set; }
        public string? Error { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
