using System.Windows.Media.Imaging;

namespace Server
{
    public class ChatImage
    {
        public string Sender { get; }
        public string FileName { get; }
        public byte[] Data { get; }
        public BitmapImage Image { get; }
        public ChatImage(string sender, string fileName, byte[] data)
        {
            Sender = sender;
            FileName = fileName;
            Data = data;
            using var ms = new System.IO.MemoryStream(data);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            Image = bmp;
        }
    }
}
