namespace Server
{
    public class ChatFile
    {
        public string Sender { get; }
        public string FileName { get; }
        public long Size { get; }
        public string TempPath { get; }
        public string DisplaySize => FormatBytes(Size);
        public ChatFile(string sender, string fileName, long size, string tempPath)
        {
            Sender = sender;
            FileName = fileName;
            Size = size;
            TempPath = tempPath;
        }
        private static string FormatBytes(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;
            if (bytes >= GB) return ($"{(double)bytes / GB:0.##} GB");
            if (bytes >= MB) return ($"{(double)bytes / MB:0.##} MB");
            if (bytes >= KB) return ($"{(double)bytes / KB:0.##} KB");
            return ($"{bytes} B");
        }
    }
}
