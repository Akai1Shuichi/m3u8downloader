namespace m3u8Downloader.Model
{
    internal class Config
    {
        public string Url { get; set; } = "";
        public string M3u8Text { get; set; } = "";
        public string VideoPath { get; set; } = "";
        public double MaxWorker { get; set; } = 32;
        public int BatchSize { get; set; } = 50;
        public string Headers { get; set; } = "accept: */*\naccept-encoding: gzip, deflate, br, zstd\naccept-language: en-GB,en-US;q=0.9,en;q=0.8,vi;q=0.7\norigin: https://goatembed.com\nreferer: https://goatembed.com/\nsec-ch-ua: \"Chromium\";v=\"140\", \"Not=A?Brand\";v=\"24\", \"Google Chrome\";v=\"140\"\nsec-ch-ua-mobile: ?0\nsec-ch-ua-platform: \"Windows\"\nsec-fetch-dest: empty\nsec-fetch-mode: cors\nsec-fetch-site: cross-site\nuser-agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36";
        public string PreferredFormat { get; set; } = "mp4"; // mp4, mkv, mp3, m4a
    }
}
