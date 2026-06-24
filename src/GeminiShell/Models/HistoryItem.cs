using System;
using System.IO;

namespace AJTools.GeminiShell.Models
{
    public class HistoryItem
    {
        public string FilePath { get; set; }
        public string FileName => Path.GetFileNameWithoutExtension(FilePath);
        public string Prompt { get; set; }
        public string Code { get; set; }
        public DateTime DateCreated { get; set; }
        
        public string DisplayName => $"[{DateCreated:MM/dd HH:mm}] {FileName}";
    }
}
