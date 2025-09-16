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
using System;

namespace Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // ban đầu chưa kết nối
        }

        private async void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_client != null) _client.Dispose();
            if (!int.TryParse(PortBox.Text, out int port))
            {
                MessageBox.Show("Port không hợp lệ");
                return;
            }
            _client = new TcpChatClient(HostBox.Text, port);
            _client.MessageReceived += msg => Dispatcher.Invoke(() => Messages.Items.Add(msg));
            _client.Connected += () => Dispatcher.Invoke(() => Messages.Items.Add("Đã kết nối tới server"));
            _client.Disconnected += () => Dispatcher.Invoke(() => Messages.Items.Add("Ngắt kết nối"));

            try
            {
                await _client.ConnectAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không kết nối được: {ex.Message}");
            }
        }

        private async void SendBtn_Click(object sender, RoutedEventArgs e)
        {
            await SendCurrentAsync();
        }

        private async void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await SendCurrentAsync();
            }
        }

        private async Task SendCurrentAsync()
        {
            var text = InputBox.Text;
            if (string.IsNullOrWhiteSpace(text)) return;
            await _client.SendAsync(text);
            Messages.Items.Add($"[Me] {text}");
            InputBox.Clear();
        }

        private void EmojiBtn_Click(object sender, RoutedEventArgs e)
        {
            var picker = new EmojiPicker { Owner = this };
            if (picker.ShowDialog() == true && picker.SelectedEmoji != null)
            {
                InputBox.Text += picker.SelectedEmoji;
                InputBox.Focus();
                InputBox.CaretIndex = InputBox.Text.Length;
            }
        }

        private TcpChatClient? _client;

        private void HostBox_TextChanged()
        {

        }
    }
}