import base64
import gzip
import hashlib
import json
import re
import zlib
from Crypto.Cipher import AES
from Crypto.Util.Padding import unpad

DEFAULT_KEY = "ZG1fdGhhbmdfc3VjX3ZhdF9nZXRfbGlua19hbl9kYnQ="


def looks_like_gzip(data: bytes) -> bool:
    return len(data) > 2 and data[0] == 0x1F and data[1] == 0x8B


def looks_like_zlib(data: bytes) -> bool:
    return len(data) > 2 and data[0] == 0x78 and data[1] in (0x01, 0x9C, 0xDA)


def try_decompress(data: bytes) -> str:
    """Decompresses byte data using Gzip, Zlib, or Deflate, or falls back to UTF-8."""
    # 1. Gzip
    if looks_like_gzip(data):
        try:
            return gzip.decompress(data).decode("utf-8", errors="replace")
        except Exception:
            pass

    # 2. Deflate raw
    try:
        return zlib.decompress(data, -zlib.MAX_WBITS).decode("utf-8", errors="replace")
    except Exception:
        pass

    # 3. Zlib / Deflate with wrapper
    if looks_like_zlib(data):
        try:
            return zlib.decompress(data).decode("utf-8", errors="replace")
        except Exception:
            pass

    # 4. Skip first 2 bytes if zlib header failed
    if len(data) > 2:
        try:
            return zlib.decompress(data[2:], -zlib.MAX_WBITS).decode("utf-8", errors="replace")
        except Exception:
            pass

    # 5. Plain UTF-8
    return data.decode("utf-8", errors="replace")


def decrypt_m3u8_data(encrypted_data: str, base64_key: str = DEFAULT_KEY) -> str:
    """Decrypts base64 AES-CBC encrypted m3u8 data and decompresses it."""
    key_bytes = base64.b64decode(base64_key)
    hashed_key = hashlib.sha256(key_bytes).digest()

    encrypted_bytes = base64.b64decode(encrypted_data.strip())
    if len(encrypted_bytes) < 17:
        raise ValueError("Encrypted data is too short")

    iv = encrypted_bytes[:16]
    ciphertext = encrypted_bytes[16:]

    cipher = AES.new(hashed_key, AES.MODE_CBC, iv)
    decrypted_bytes = unpad(cipher.decrypt(ciphertext), AES.block_size)

    text = try_decompress(decrypted_bytes)

    # Check if text is a JSON payload
    try:
        parsed = json.loads(text)
        if isinstance(parsed, str):
            text = parsed
        else:
            return json.dumps(parsed, ensure_ascii=False)
    except Exception:
        # Fallback for quoted strings
        if len(text) >= 2 and text.startswith('"') and text.endswith('"'):
            try:
                text = json.loads(text)
            except Exception:
                pass

    return text.replace("\r\n", "\n").replace("\r", "\n")


def process_m3u8_data(raw_or_encrypted: str) -> str:
    """Decodes/decrypts m3u8 content if encrypted, strips byterange, and normalizes lines."""
    content = raw_or_encrypted.strip()

    # If it looks like base64 encrypted payload (doesn't start with #EXTM3U)
    if not content.startswith("#EXTM3U"):
        try:
            content = decrypt_m3u8_data(content)
        except Exception:
            # If not decryptable, keep as is
            pass

    # Remove EXT-X-BYTERANGE lines
    content = re.sub(r"#EXT-X-BYTERANGE:.*\n", "", content)
    # Normalize newlines
    lines = content.replace("\r\n", "\n").replace("\r", "\n")
    return lines
