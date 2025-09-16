namespace Server
{
    public class ChatFile
    {
        public string Sender { get; }
        public string FileName { get; }
        public long Size { get; }
        public string TempPath { get; }
        public ChatFile(string sender, string fileName, long size, string tempPath)
        {
            Sender = sender;
            FileName = fileName;
            Size = size;
            TempPath = tempPath;
        }
    }
}
