using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading.Tasks;

namespace Server
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            _server = new TcpChatServer("0.0.0.0", _port);
            _server.ClientConnected += c => Dispatcher.Invoke(UpdateClientDisplay);
            _server.ClientDisconnected += c => Dispatcher.Invoke(UpdateClientDisplay);
            _server.ClientsChanged += () => Dispatcher.Invoke(UpdateClientDisplay);
            _server.MessageReceived += (c, m) =>
            {
                var label = _server.GetClientLabel(c);
                if (m.StartsWith("__IMG__|"))
                {
                    var parts = m.Split('|',3);
                    if (parts.Length==3)
                    {
                        try
                        {
                            var data = Convert.FromBase64String(parts[2]);
                            var chatImg = new ChatImage(label, parts[1], data);
                            Dispatcher.Invoke(() => LogList.Items.Add(chatImg));
                        }
                        catch { Dispatcher.Invoke(() => LogList.Items.Add($"[{DateTime.Now:t}] {label}: (ảnh lỗi)")); }
                    }
                    return;
                }
                if (m.StartsWith("__FILE_START__|"))
                {
                    var parts = m.Split('|');
                    var name = parts[1];
                    long size = long.Parse(parts[2]);
                    _rxFiles[label + "|" + name] = new FileRxCtx(size);
                    return;
                }
                if (m.StartsWith("__FILE_CHUNK__|"))
                {
                    var parts = m.Split('|',3);
                    var name = parts[1];
                    var key = label + "|" + name;
                    if (_rxFiles.TryGetValue(key, out var ctx))
                    {
                        var bytes = Convert.FromBase64String(parts[2]);
                        ctx.Stream.Write(bytes,0,bytes.Length);
                        ctx.Written += bytes.Length;
                    }
                    return; // không log từng chunk
                }
                if (m.StartsWith("__FILE_END__|"))
                {
                    var name = m.Split('|')[1];
                    var key = label + "|" + name;
                    if (_rxFiles.TryGetValue(key, out var ctx))
                    {
                        ctx.Stream.Flush(); ctx.Stream.Dispose();
                        _rxFiles.Remove(key);
                        var cf = new ChatFile(label, name, ctx.Size, ctx.Temp);
                        Dispatcher.Invoke(() => LogList.Items.Add(cf));
                    }
                    return;
                }

                Dispatcher.Invoke(() => LogList.Items.Add($"[{DateTime.Now:t}] {label}: {m}"));
            };
            _server.Start();

            var ips = string.Join(", ", System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName())
                                        .AddressList
                                        .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                        .Select(a => a.ToString()));

            StatusLabel.Content = $"Server đang lắng nghe: {ips}:{_port}";
        }

        private async void ServerSend_Click(object sender, RoutedEventArgs e) => await SendServerMsg();
        private async void ServerInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                await SendServerMsg();
        }

        private async Task SendServerMsg()
        {
            var text = ServerInput.Text;
            if (string.IsNullOrWhiteSpace(text)) return;
            await _server.SendToAllAsync(text);
            LogList.Items.Add($"[{DateTime.Now:t}] [Server]: {text}");
            ServerInput.Clear();
        }

        private async void ServerImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Hình ảnh|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Tất cả|*.*" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var bytes = System.IO.File.ReadAllBytes(dlg.FileName);
                    var b64 = Convert.ToBase64String(bytes);
                    var name = System.IO.Path.GetFileName(dlg.FileName);
                    await _server.SendToAllAsync($"__IMG__|{name}|{b64}");
                    LogList.Items.Add(new ChatImage("Server", name, bytes));
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        private async void ServerFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Tất cả tập tin|*.*" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var fi = new System.IO.FileInfo(dlg.FileName);
                    if (fi.Length > 1_000_000_000)
                    {
                        MessageBox.Show($"File quá lớn: {fi.Length / (1024*1024.0):0.##} MB (> 1024 MB)", "Không thể gửi", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    await _server.SendToAllAsync($"__FILE_START__|{fi.Name}|{fi.Length}");
                    using var fs = fi.OpenRead();
                    var buffer = new byte[30000];
                    int read;
                    while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        var b64 = Convert.ToBase64String(buffer, 0, read);
                        await _server.SendToAllAsync($"__FILE_CHUNK__|{fi.Name}|{b64}");
                    }
                    await _server.SendToAllAsync($"__FILE_END__|{fi.Name}");
                    LogList.Items.Add(new ChatFile("Server", fi.Name, fi.Length, fi.FullName));
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        private void ServerEmoji_Click(object sender, RoutedEventArgs e)
        {
            var picker = new EmojiPicker { Owner = this };
            if (picker.ShowDialog() == true && picker.SelectedEmoji != null)
            {
                ServerInput.Text += picker.SelectedEmoji;
                ServerInput.Focus();
                ServerInput.CaretIndex = ServerInput.Text.Length;
            }
        }

        private readonly TcpChatServer _server;
        private readonly string _ip = "0.0.0.0";
        private readonly int _port = 13000;
        private int _serverConnected = 0;

        private void UpdateClientDisplay()
        {
            var labels = _server.GetClientLabels().ToList();
            _serverConnected = labels.Count;
            StatusLabel.Content = $"Client kết nối: {_serverConnected}";
            ClientList.ItemsSource = labels;
        }

        private class FileRxCtx
        {
            public long Size; public long Written; public string Temp; public System.IO.FileStream Stream;
            public FileRxCtx(long size)
            {
                Size = size; Temp = System.IO.Path.GetTempFileName();
                Stream = new System.IO.FileStream(Temp, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.Read);
            }
        }
        private readonly System.Collections.Generic.Dictionary<string, FileRxCtx> _rxFiles = new();

        private void OpenSaveForFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Documents.Hyperlink hl && hl.DataContext is ChatFile cf)
            {
                var dlg = new Microsoft.Win32.SaveFileDialog { FileName = cf.FileName };
                if (dlg.ShowDialog() == true)
                {
                    try
                    {
                        System.IO.File.Copy(cf.TempPath, dlg.FileName, true);
                        MessageBox.Show("Lưu tập tin thành công", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}