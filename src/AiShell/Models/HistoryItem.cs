using System;
using System.IO;

namespace AJTools.AiShell.Models
{
    public class HistoryItem
    {
        public string FilePath { get; set; }
        public string FileName => Path.GetFileNameWithoutExtension(FilePath);
        public string Prompt { get; set; }
        public string Provider { get; set; }
        public string Code { get; set; }
        public DateTime DateCreated { get; set; }

        public string DisplayName => $"[{DateCreated:MM/dd HH:mm}] {FileName}";

        public string ToolTipText => string.IsNullOrWhiteSpace(Provider)
            ? Prompt
            : $"{Prompt}\n\nGenerated with: {Provider}";
    }
}
