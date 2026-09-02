import socket
import threading
from http.server import BaseHTTPRequestHandler, HTTPServer
from typing import Optional


class M3U8RequestHandler(BaseHTTPRequestHandler):
    m3u8_content: str = ""

    def log_message(self, format, *args):
        # Suppress default noisy console logs
        pass

    def do_GET(self):
        if self.path.split("?")[0] in ("/playlist.m3u8", "/playlist.m3u"):
            content_bytes = self.m3u8_content.encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "application/vnd.apple.mpegurl; charset=utf-8")
            self.send_header("Content-Length", str(len(content_bytes)))
            self.send_header("Access-Control-Allow-Origin", "*")
            self.end_headers()
            self.wfile.write(content_bytes)
        else:
            self.send_response(404)
            self.end_headers()
            self.wfile.write(b"Not Found")


class LocalHttpServer:
    def __init__(self, m3u8_content: str, starting_port: int = 8000):
        self.m3u8_content = m3u8_content
        self.port = self._find_available_port(starting_port)
        self._server: Optional[HTTPServer] = None
        self._thread: Optional[threading.Thread] = None
        self._is_running = False

    @property
    def base_url(self) -> str:
        return f"http://127.0.0.1:{self.port}"

    @property
    def playlist_url(self) -> str:
        return f"{self.base_url}/playlist.m3u8"

    @staticmethod
    def _find_available_port(starting_port: int = 8000) -> int:
        for port in range(starting_port, starting_port + 200):
            with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
                try:
                    s.bind(("127.0.0.1", port))
                    return port
                except OSError:
                    continue
        return starting_port

    def start(self) -> None:
        if self._is_running:
            return

        handler_cls = type(
            "ConfiguredM3U8Handler",
            (M3U8RequestHandler,),
            {"m3u8_content": self.m3u8_content}
        )

        self._server = HTTPServer(("127.0.0.1", self.port), handler_cls)
        self._is_running = True
        self._thread = threading.Thread(target=self._server.serve_forever, daemon=True)
        self._thread.start()

    def stop(self) -> None:
        if not self._is_running:
            return

        self._is_running = False
        if self._server:
            try:
                self._server.shutdown()
                self._server.server_close()
            except Exception:
                pass
            self._server = None

        if self._thread and self._thread.is_alive():
            self._thread.join(timeout=1.0)
            self._thread = None
