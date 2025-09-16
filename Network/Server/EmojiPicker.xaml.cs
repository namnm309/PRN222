using System.Windows;

namespace Server
{
    public partial class EmojiPicker : Window
    {
        public string? SelectedEmoji { get; private set; }
        public EmojiPicker()
        {
            InitializeComponent();
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Content is string s)
            {
                SelectedEmoji = s;
                DialogResult = true;
            }
        }
    }
}
