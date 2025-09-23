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
        //Nạp 2 bín này vào constructor để connect tới server
        private readonly string _host;
        private readonly int _port;

        private TcpClient? _client;
        private CancellationTokenSource? _cts;

        //evnt của WPF khi có tn , connect , disconnect 
        public event Action<object>? MessageReceived; 
        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<string,long,long>? FileSendProgress; // name, sent, total
        public event Action<string,long,long>? FileReceiveProgress; // name, written, total

        //constructor
        public TcpChatClient(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public async Task ConnectAsync()
        {
            _client = new TcpClient();
            _client.NoDelay = true;
            _client.SendBufferSize = 1024 * 1024;
            _client.ReceiveBufferSize = 1024 * 1024;
            await _client.ConnectAsync(_host, _port); //method cung cấp bởi class TCPClient
            // Đợi 50ms để server xử lý handshake nhằm đảm bảo map tên trước khi user gửi tin đầu tiên.
            //await Task.Delay(50);

            Connected?.Invoke();
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ReceiveLoop(_cts.Token));

            // Gửi tên thiết bị ngay sau khi kết nối
            string deviceName = Environment.MachineName;
            await SendAsync($"__NAME__|{deviceName}");

            // ready
        }


        //Method nhận tin 
        private async Task ReceiveLoop(CancellationToken token)
        {
            var stream = _client!.GetStream();
            var buffer = new byte[4096];
            var sb = new StringBuilder();
            try
            {
                while (!token.IsCancellationRequested && _client.Connected)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (read == 0) break;
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, read));
                    while (true)
                    {
                        int nl = sb.ToString().IndexOf('\n');
                        if (nl < 0) break;
                        var raw = sb.ToString(0, nl).TrimEnd('\r');
                        sb.Remove(0, nl + 1);
                        if (raw.Length == 0) continue;
                        string display;
                        int sep = raw.IndexOf('|');
                        if (sep <= 0) { MessageReceived?.Invoke(raw); continue; }
                        var source = raw.Substring(0, sep);
                        var content = raw[(sep + 1)..];

                        if (content.StartsWith("__IMG__|"))
                        {
                            var parts = content.Split('|',3);
                            if (parts.Length==3)
                            {
                                var data = Convert.FromBase64String(parts[2]);
                                MessageReceived?.Invoke(new ChatImage(source, parts[1], data));
                            }
                            continue;
                        }
                        if (content.StartsWith("__FILE_"))
                        {
                            HandleFileProtocol(source, content);
                            continue;
                        }
                        display = $"[{source}] {content}";
                        MessageReceived?.Invoke(display);
                    }
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
            var data = Encoding.UTF8.GetBytes(message + "\n");
            return _client.GetStream().WriteAsync(data, 0, data.Length);
        }

        //Khi mất connect
        public void Dispose()
        {
            _cts?.Cancel();
            _client?.Close();
        }

        public Task SendImageAsync(string path)
        {
            var bytes = System.IO.File.ReadAllBytes(path);
            var b64 = Convert.ToBase64String(bytes);
            var name = System.IO.Path.GetFileName(path);
            return SendAsync($"__IMG__|{name}|{b64}");
        }

        public async Task SendFileAsync(string path, int chunkSize = 262144)
        {
            var fi = new System.IO.FileInfo(path);//Lấy info file 
            var name = fi.Name;//tên file

            //Gửi
            await SendAsync($"__FILE_START__|{name}|{fi.Length}");

            using var fs = fi.OpenRead();//Đọc file 

            var buffer = new byte[chunkSize];//chia ra từng đợt gửi , giống IDM 

            int read;
            long sent = 0;
            while ((read = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                var chunkB64 = Convert.ToBase64String(buffer, 0, read);
                await SendAsync($"__FILE_CHUNK__|{name}|{chunkB64}");
                sent += read;
                FileSendProgress?.Invoke(name, sent, fi.Length);
            }
            //tb khi done
            await SendAsync($"__FILE_END__|{name}");
            FileSendProgress?.Invoke(name, fi.Length, fi.Length);
        }

        private class FileReceiveContext
        {
            public long Size;
            public long Written;
            public string TempPath;
            public System.IO.FileStream Stream;
            public FileReceiveContext(long size)
            {
                Size = size;
                TempPath = System.IO.Path.GetTempFileName();
                Stream = new System.IO.FileStream(TempPath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.Read);
            }
        }

        //n file 1 lúc , n file từ n client 1 lúc 
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, FileReceiveContext> _fileReceive = new();
        
        private void HandleFileProtocol(string source, string content)
        {
            if (content.StartsWith("__FILE_START__|"))
            {
                var parts = content.Split('|');
                var name = parts[1];
                long size = long.Parse(parts[2]);
                var key = source + "|" + name;
                _fileReceive[key] = new FileReceiveContext(size);
            }
            else if (content.StartsWith("__FILE_CHUNK__|"))
            {
                var parts = content.Split('|',3);
                var name = parts[1];
                var key = source + "|" + name;
                if (!_fileReceive.TryGetValue(key, out var ctx)) return;
                var bytes = Convert.FromBase64String(parts[2]);
                ctx.Stream.Write(bytes,0,bytes.Length);
                ctx.Written += bytes.Length;
                FileReceiveProgress?.Invoke(name, ctx.Written, ctx.Size);
            }
            else if (content.StartsWith("__FILE_END__|"))
            {
                var name = content.Split('|')[1];
                var key = source + "|" + name;
                if (_fileReceive.TryRemove(key, out var ctx))
                {
                    ctx.Stream.Flush();
                    ctx.Stream.Dispose();
                    var chatFile = new ChatFile(source, name, ctx.Size, ctx.TempPath);
                    MessageReceived?.Invoke(chatFile);
                    FileReceiveProgress?.Invoke(name, ctx.Size, ctx.Size);
                }
            }
        }
    }
}
