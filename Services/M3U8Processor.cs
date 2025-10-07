using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections.Generic;

namespace m3u8Downloader.Services
{
    public static class M3U8Processor
    {
        private const string DEFAULT_KEY = "ZG1fdGhhbmdfc3VjX3ZhdF9nZXRfbGlua19hbl9kYnQ="; // Base64 encoded default key

        public static async Task<string> DecryptDataAsync(string encryptedData, string? base64Key = null)
        {
            base64Key ??= DEFAULT_KEY;

            // Decode base64 key
            byte[] keyBytes = Convert.FromBase64String(base64Key);

            // SHA-256 of key
            byte[] hashedKey;
            using (var sha256 = SHA256.Create())
            {
                hashedKey = sha256.ComputeHash(keyBytes);
            }

            // Decode encrypted data
            byte[] encryptedBytes = Convert.FromBase64String(encryptedData);

            // Extract IV (first 16 bytes) and ciphertext
            if (encryptedBytes.Length < 17)
            {
                throw new InvalidDataException("Encrypted data too short");
            }

            byte[] iv = new byte[16];
            byte[] ciphertext = new byte[encryptedBytes.Length - 16];
            Array.Copy(encryptedBytes, 0, iv, 0, 16);
            Array.Copy(encryptedBytes, 16, ciphertext, 0, ciphertext.Length);

            // Decrypt using AES-CBC
            byte[] decryptedBytes;
            using (var aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = hashedKey;
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor())
                using (var msDecrypt = new MemoryStream(ciphertext))
                using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (var msOutput = new MemoryStream())
                {
                    await csDecrypt.CopyToAsync(msOutput);
                    decryptedBytes = msOutput.ToArray();
                }
            }

            // Try to decompress if compressed; support GZip/Deflate, otherwise treat as UTF8 text
            string text = await Decompression.TryDecompressToStringAsync(decryptedBytes);

            // If it's a JSON string (quoted, with escaped newlines), unescape to raw string.
            // If it's a JSON object/array, return normalized JSON.
            try
            {
                using var jsonDoc = JsonDocument.Parse(text);
                if (jsonDoc.RootElement.ValueKind == JsonValueKind.String)
                {
                    text = jsonDoc.RootElement.GetString() ?? string.Empty;
                }
                else
                {
                    return jsonDoc.RootElement.GetRawText();
                }
            }
            catch
            {
                // Not valid JSON – try to unescape if looks like a quoted JSON string
                if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"')
                {
                    try
                    {
                        text = JsonSerializer.Deserialize<string>(text) ?? text;
                    }
                    catch { }
                }
            }

            // Normalize line endings to CRLF
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            text = text.Replace("\n", "\r\n");
            return text;
        }

        public static async Task<M3U8Playlist> ProcessM3U8DataAsync(string encryptedData)
        {
            var decryptedData = await DecryptDataAsync(encryptedData);
            if (string.IsNullOrEmpty(decryptedData))
            {
                throw new InvalidDataException("Decryption returned empty content");
            }

            // decryptedData may already be the m3u8 content or a JSON string containing it.
            string m3u8Content = decryptedData;

            // Remove EXT-X-BYTERANGE lines if present
            m3u8Content = System.Text.RegularExpressions.Regex.Replace(
                m3u8Content,
                @"#EXT-X-BYTERANGE:.*\n",
                string.Empty
            );

            // Ensure CRLF endings
            m3u8Content = m3u8Content.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

            

            return new M3U8Playlist
            {
                Type = "application/vnd.apple.mpegurl",
                Content = m3u8Content,
                Base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(m3u8Content))
            };
        }

    }

    public sealed class M3U8Playlist
    {
        public string Type { get; set; }
        public string Content { get; set; }
        public string Base64Content { get; set; }

        public string ToDataUrl()
        {
            return $"data:{Type};base64,{Base64Content}";
        }
    }

    internal static class CompressionHelpers
    {
        public static bool LooksLikeGzip(byte[] data)
        {
            return data.Length > 2 && data[0] == 0x1F && data[1] == 0x8B;
        }

        public static bool LooksLikeZlib(byte[] data)
        {
            // Common zlib headers: 0x78 0x01, 0x78 0x9C, 0x78 0xDA
            return data.Length > 2 && data[0] == 0x78 && (data[1] == 0x01 || data[1] == 0x9C || data[1] == 0xDA);
        }
    }

    internal static class Decompression
    {
        public static async Task<string> TryDecompressToStringAsync(byte[] bytes)
        {
            // 1) GZip
            if (CompressionHelpers.LooksLikeGzip(bytes))
            {
                try
                {
                    using var ms = new MemoryStream(bytes);
                    using var gz = new GZipStream(ms, CompressionMode.Decompress);
                    using var outMs = new MemoryStream();
                    await gz.CopyToAsync(outMs);
                    return Encoding.UTF8.GetString(outMs.ToArray());
                }
                catch { /* fall through */ }
            }

            // 2) Deflate (raw)
            try
            {
                using var ms = new MemoryStream(bytes);
                using var df = new DeflateStream(ms, CompressionMode.Decompress);
                using var outMs = new MemoryStream();
                await df.CopyToAsync(outMs);
                return Encoding.UTF8.GetString(outMs.ToArray());
            }
            catch { /* fall through */ }

            // 3) Deflate skipping possible zlib header (2 bytes)
            if (CompressionHelpers.LooksLikeZlib(bytes) && bytes.Length > 2)
            {
                try
                {
                    using var ms = new MemoryStream(bytes, 2, bytes.Length - 2);
                    using var df = new DeflateStream(ms, CompressionMode.Decompress);
                    using var outMs = new MemoryStream();
                    await df.CopyToAsync(outMs);
                    return Encoding.UTF8.GetString(outMs.ToArray());
                }
                catch { /* fall through */ }
            }

            // 4) Not compressed or unsupported: return UTF8 text directly
            return Encoding.UTF8.GetString(bytes);
        }
    }
}


