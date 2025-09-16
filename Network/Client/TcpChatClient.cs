using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Client
{
    /// <summary>
    /// Lớp client TCP đơn giản:
    ///  - Kết nối tới server
    ///  - Nhận dữ liệu bất đồng bộ qua Task riêng
    ///  - Gửi tin nhắn dưới dạng UTF8
    /// UI chỉ cần subscribe sự kiện MessageReceived để hiển thị.
    /// </summary>
    public class TcpChatClient : IDisposable
    {
        private readonly string _host;
        private readonly int _port;

        private TcpClient? _client;
        private CancellationTokenSource? _cts;

        public event Action<string>? MessageReceived;
        public event Action? Connected;
        public event Action? Disconnected;

        public TcpChatClient(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public async Task ConnectAsync()
        {
            _client = new TcpClient();
            await _client.ConnectAsync(_host, _port);
            Connected?.Invoke();
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ReceiveLoop(_cts.Token));

            // Gửi tên thiết bị ngay sau khi kết nối
            string deviceName = Environment.MachineName;
            await SendAsync($"__NAME__|{deviceName}");
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            var stream = _client!.GetStream();
            var buffer = new byte[1024];
            try
            {
                while (!token.IsCancellationRequested && _client.Connected)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (read == 0) break;
                    var raw = Encoding.UTF8.GetString(buffer, 0, read);
                    // Chuẩn "source|content". Nếu không khớp, để toàn bộ.
                    string display;
                    int sep = raw.IndexOf('|');
                    if (sep > 0)
                    {
                        var source = raw.Substring(0, sep);
                        var content = raw[(sep + 1)..];
                        display = $"[{source}] {content}";
                    }
                    else
                    {
                        display = raw;
                    }
                    MessageReceived?.Invoke(display);
                }
            }
            catch (Exception) when (token.IsCancellationRequested) { }
            catch (Exception) { }
            finally
            {
                Disconnected?.Invoke();
            }
        }

        public Task SendAsync(string message)
        {
            if (_client == null || !_client.Connected) return Task.CompletedTask;
            var data = Encoding.UTF8.GetBytes(message);
            return _client.GetStream().WriteAsync(data, 0, data.Length);
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _client?.Close();
        }
    }
}
