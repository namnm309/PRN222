using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    /// <summary>
    /// Lớp gói gọn logic Server TCP đơn giản cho ứng dụng chat.
    ///  - Lắng nghe kết nối mới
    ///  - Nhận dữ liệu từ mỗi client trên luồng riêng
    ///  - Phát (broadcast) tin nhắn tới tất cả client khác
    /// </summary>
    public class TcpChatServer : IDisposable
    {
        private readonly IPAddress _ipAddress;
        private readonly int _port;
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentDictionary<TcpClient, Task> _clients = new();
        public event Action<TcpClient, string>? MessageReceived;
        public event Action<TcpClient>? ClientConnected;
        public event Action<TcpClient>? ClientDisconnected;

        public TcpChatServer(string host, int port)
        {
            _ipAddress = IPAddress.Parse(host);
            _port = port;
            _listener = new TcpListener(_ipAddress, _port);
        }

        // Bắt đầu lắng nghe (non-blocking). Gọi một lần khi khởi động.
        public void Start()
        {
            _listener.Start();
            Task.Run(ListenLoop, _cts.Token);
        }

       // Vòng lặp chấp nhận client. Chạy nền tới khi Cancel.
        private async Task ListenLoop()
        {
            var token = _cts.Token;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(token);
                    if (token.IsCancellationRequested) break;

                    ClientConnected?.Invoke(client);
                    // Start processing client
                    var task = Task.Run(() => HandleClientAsync(client, token), token);
                    _clients[client] = task;
                }
            }
            catch (OperationCanceledException) { }
        }

        
        // Xử lý 1 client: đọc tin & broadcast. Mỗi client 1 Task riêng.       
        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            var stream = client.GetStream();
            var buffer = new byte[1024];
            try
            {
                while (!token.IsCancellationRequested && client.Connected)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (read == 0) break; // disconnected
                    string rawMessage = Encoding.UTF8.GetString(buffer, 0, read);
                    // Lấy IP của client gửi
                    var ip = ((System.Net.IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

                    // Gửi tới UI server (giữ nguyên raw message)
                    MessageReceived?.Invoke(client, rawMessage);

                    // Thêm tiền tố IP để các client khác phân biệt nguồn gửi
                    string messageForClients = $"{ip}|{rawMessage}";
                    await BroadcastAsync(messageForClients, client, token);
                }
            }
            catch (Exception) when (token.IsCancellationRequested) { }
            catch (Exception)
            {
                // ignore
            }
            finally
            {
                ClientDisconnected?.Invoke(client);
                _clients.TryRemove(client, out _);
            }
        }

        // Tin nhắn chủ động từ server được gắn nhãn "Server" để client hiển thị dễ.
        public Task SendToAllAsync(string message) => BroadcastAsync($"Server|{message}", null, _cts.Token);

        private async Task BroadcastAsync(string message, TcpClient? exclude, CancellationToken token)
        {
            var data = Encoding.UTF8.GetBytes(message);
            foreach (var kvp in _clients)
            {
                var cli = kvp.Key;
                if (cli == exclude) continue;
                try
                {
                    await cli.GetStream().WriteAsync(data, 0, data.Length, token);
                }
                catch { }
            }
        }

        public void Stop()
        {
            _cts.Cancel();
            _listener.Stop();
        }

        public void Dispose()
        {
            Stop();
            _cts.Dispose();
        }
    }
}
