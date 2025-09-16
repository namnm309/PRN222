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
            _server.ClientConnected += c => Dispatcher.Invoke(() => StatusLabel.Content = $"Client kết nối: {_serverConnected++}");
            _server.MessageReceived += (c, m) => Dispatcher.Invoke(() => LogList.Items.Add($"[{DateTime.Now:t}] {m}"));
            _server.Start();

            var ips = string.Join(", ", System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName())
                                        .AddressList
                                        .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                        .Select(a => a.ToString()));

            StatusLabel.Content = $"Server đang lắng nghe: {ips}:{_port}";
        }

        private readonly TcpChatServer _server;
        private readonly string _ip = "0.0.0.0";
        private readonly int _port = 13000;
        private int _serverConnected = 0;
    }
}