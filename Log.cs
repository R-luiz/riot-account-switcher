using System;
using System.IO;

namespace RiotAccountSwitcher
{
    /// <summary>Plain-text trail of what the switcher did. Never receives session data or keys.</summary>
    public static class Log
    {
        public static readonly string AppDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RiotAccountSwitcher");

        static readonly string LogFile = Path.Combine(AppDir, "switcher.log");
        const long MaxBytes = 256 * 1024;

        public static void Write(string message)
        {
            try
            {
                Directory.CreateDirectory(AppDir);
                var info = new FileInfo(LogFile);
                if (info.Exists && info.Length > MaxBytes) info.Delete();
                File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
