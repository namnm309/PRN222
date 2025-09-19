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
using System.IO;

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
            _client.MessageReceived += obj => Dispatcher.Invoke(() =>
            {
                if (obj is string s) Messages.Items.Add(s);
                else if (obj is ChatImage ci) Messages.Items.Add(ci);
                else if (obj is ChatFile cf) Messages.Items.Add(cf);
            });
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

        private async void ImageBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Hình ảnh|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Tất cả|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                try { await _client!.SendImageAsync(dlg.FileName); }
                catch (Exception ex) { MessageBox.Show(ex.Message); return; }
                try
                {
                    var bytes = File.ReadAllBytes(dlg.FileName);
                    Messages.Items.Add(new ChatImage("Me", System.IO.Path.GetFileName(dlg.FileName), bytes));
                }
                catch { }
            }
        }

        private async void FileBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Tất cả tập tin|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var fi = new FileInfo(dlg.FileName);
                    if (fi.Length > 1_000_000_000)
                    {
                        MessageBox.Show($"File quá lớn: {fi.Length / (1024*1024.0):0.##} MB (> 1024 MB)", "Không thể gửi", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    await _client!.SendFileAsync(dlg.FileName);
                    Messages.Items.Add(new ChatFile("Me", fi.Name, fi.Length, fi.FullName));
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        private void SaveReceivedFile_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not ChatFile cf) return;
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

        private void OpenSaveForImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Documents.Hyperlink hl && hl.DataContext is ChatImage ci)
            {
                var dlg = new Microsoft.Win32.SaveFileDialog { FileName = ci.FileName, Filter = "Hình ảnh|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Tất cả|*.*" };
                if (dlg.ShowDialog() == true)
                {
                    try
                    {
                        System.IO.File.WriteAllBytes(dlg.FileName, ci.Data);
                        MessageBox.Show("Lưu ảnh thành công", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private TcpChatClient? _client;

        private void HostBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}