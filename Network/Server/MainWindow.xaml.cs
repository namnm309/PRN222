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
            _server.ClientConnected += c => Dispatcher.Invoke(() => StatusLabel.Content = $"Client kết nối: {++_serverConnected}");
            _server.ClientDisconnected += c => Dispatcher.Invoke(() => StatusLabel.Content = $"Client kết nối: {--_serverConnected}");
            _server.MessageReceived += (c, m) =>
            {
                var ip = ((System.Net.IPEndPoint)c.Client.RemoteEndPoint).Address;
                Dispatcher.Invoke(() => LogList.Items.Add($"[{DateTime.Now:t}] {ip}: {m}"));
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
    }
}