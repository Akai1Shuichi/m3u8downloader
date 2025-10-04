using System.Net;
using System.Text;

namespace m3u8Downloader.Services
{
    public class LocalHttpServer : IDisposable
    {
        private HttpListener? _listener;
        private Thread? _serverThread;
        private string _m3u8Content;
        private int _port;
        private bool _isRunning = false;

        public string BaseUrl => $"http://127.0.0.1:{_port}";
        public string PlaylistUrl => $"{BaseUrl}/playlist.m3u8";

        public LocalHttpServer(string m3u8Content, int port = 8000)
        {
            _m3u8Content = m3u8Content;
            _port = port;
        }

        public void Start()
        {
            if (_isRunning) return;

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            
            try
            {
                _listener.Start();
                _isRunning = true;

                _serverThread = new Thread(HandleRequests)
                {
                    IsBackground = true
                };
                _serverThread.Start();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to start HTTP server on port {_port}: {ex.Message}", ex);
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _listener?.Stop();
            _serverThread?.Join(1000); // Wait up to 1 second for thread to finish
        }

        private void HandleRequests()
        {
            while (_isRunning && _listener != null)
            {
                try
                {
                    var context = _listener.GetContext();
                    ProcessRequest(context);
                }
                catch (HttpListenerException)
                {
                    // Server was stopped
                    break;
                }
                catch (Exception)
                {
                    // Ignore other errors to keep server running
                }
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                if (request.Url?.AbsolutePath == "/playlist.m3u8")
                {
                    // Serve the M3U8 content
                    response.ContentType = "application/vnd.apple.mpegurl";
                    response.ContentEncoding = Encoding.UTF8;
                    
                    var buffer = Encoding.UTF8.GetBytes(_m3u8Content);
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
                else
                {
                    // Return 404 for other paths
                    response.StatusCode = 404;
                }
            }
            catch (Exception)
            {
                response.StatusCode = 500;
            }
            finally
            {
                response.OutputStream.Close();
            }
        }

        public void Dispose()
        {
            Stop();
            _listener?.Close();
        }
    }
}
