using System.Windows;

namespace Server
{
    public partial class SaveProgressWindow : Window
    {
        public SaveProgressWindow()
        {
            InitializeComponent();
        }
        public void SetStatus(string text) => StatusText.Text = text;
        public void SetProgress(double percent) => Progress.Value = percent;
    }
}
