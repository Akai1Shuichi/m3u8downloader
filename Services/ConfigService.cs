using m3u8Downloader.Model;
using System.IO;
using System.Text.Json;

namespace m3u8Downloader.Services
{
    internal class ConfigService
    {
        private readonly string _settingFilePath;

        public ConfigService()
        {
            // Lưu file cài đặt trong thư mục AppData
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "M3U8Downloader");

            if (!Directory.Exists(appFolder))
                Directory.CreateDirectory(appFolder);

            _settingFilePath = Path.Combine(appFolder, "settings.json");
        }

        public async Task<Config> LoadSettingsAsync()
        {
            try
            {
                if (!File.Exists(_settingFilePath))
                {
                    // Nếu file không tồn tại, trả về cài đặt mặc định
                    return new Config();
                }

                var json = await File.ReadAllTextAsync(_settingFilePath);
                var settings = JsonSerializer.Deserialize<Config>(json);
                return settings ?? new Config();
            }
            catch (Exception ex)
            {
                // Log lỗi và trả về cài đặt mặc định
                System.Diagnostics.Debug.WriteLine($"Lỗi khi tải cài đặt: {ex.Message}");
                return new Config();
            }
        }

        public async Task SaveSettingsAsync(Config config)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var json = JsonSerializer.Serialize(config, options);
                await File.WriteAllTextAsync(_settingFilePath, json);
            }
            catch (Exception ex)
            {
                // Log lỗi
                System.Diagnostics.Debug.WriteLine($"Lỗi khi lưu cài đặt: {ex.Message}");
                throw;
            }
        }
    }
}
