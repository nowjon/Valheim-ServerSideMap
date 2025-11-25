using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ServerSideMap
{
    public class WebMapServer
    {
        private HttpListener _listener;
        private Thread _listenerThread;
        private bool _isRunning = false;
        private string _webMapDirectory;

        public bool IsRunning => _isRunning;

        public void Start(int port, string webMapDirectory)
        {
            if (_isRunning)
            {
                Utility.Log("WebMap server is already running");
                return;
            }

            _webMapDirectory = webMapDirectory;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://*:{port}/");
                _listener.Start();
                _isRunning = true;

                _listenerThread = new Thread(Listen);
                _listenerThread.IsBackground = true;
                _listenerThread.Start();

                Utility.Log($"WebMap server started on port {port}");
            }
            catch (Exception ex)
            {
                Utility.Log($"Failed to start WebMap server: {ex.Message}");
                _isRunning = false;
            }
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;

            try
            {
                _listener?.Stop();
                _listener?.Close();
                _listener = null;

                if (_listenerThread != null && _listenerThread.IsAlive)
                {
                    _listenerThread.Join(1000);
                }

                Utility.Log("WebMap server stopped");
            }
            catch (Exception ex)
            {
                Utility.Log($"Error stopping WebMap server: {ex.Message}");
            }
        }

        private void Listen()
        {
            while (_isRunning && _listener != null)
            {
                try
                {
                    var context = _listener.GetContext();
                    Task.Run(() => HandleRequest(context));
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Utility.Log($"WebMap server error: {ex.Message}");
                    }
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                var path = request.Url.AbsolutePath;

                // Handle API endpoints
                if (path.StartsWith("/api/"))
                {
                    WebMapApi.HandleApiRequest(context);
                    return;
                }

                // Handle static files
                if (path == "/" || path == "/index.html")
                {
                    ServeFile(response, Path.Combine(_webMapDirectory, "index.html"), "text/html");
                    return;
                }

                // Serve other static files
                var filePath = Path.Combine(_webMapDirectory, path.TrimStart('/'));
                if (File.Exists(filePath))
                {
                    var ext = Path.GetExtension(filePath).ToLower();
                    var contentType = GetContentType(ext);
                    ServeFile(response, filePath, contentType);
                }
                else
                {
                    response.StatusCode = 404;
                    response.Close();
                }
            }
            catch (Exception ex)
            {
                Utility.Log($"Error handling WebMap request: {ex.Message}");
                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch { }
            }
        }

        private void ServeFile(HttpListenerResponse response, string filePath, string contentType)
        {
            try
            {
                var content = File.ReadAllBytes(filePath);
                response.ContentType = contentType;
                response.ContentLength64 = content.Length;
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.OutputStream.Write(content, 0, content.Length);
                response.Close();
            }
            catch (Exception ex)
            {
                Utility.Log($"Error serving file {filePath}: {ex.Message}");
                response.StatusCode = 500;
                response.Close();
            }
        }

        private string GetContentType(string extension)
        {
            switch (extension)
            {
                case ".html": return "text/html";
                case ".css": return "text/css";
                case ".js": return "application/javascript";
                case ".json": return "application/json";
                case ".png": return "image/png";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".webp": return "image/webp";
                default: return "application/octet-stream";
            }
        }
    }
}

