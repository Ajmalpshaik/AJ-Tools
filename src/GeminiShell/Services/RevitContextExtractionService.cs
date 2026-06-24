using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;

namespace AJTools.GeminiShell.Services
{
    public class RevitContextExtractionService : IExternalEventHandler
    {
        private readonly ExternalEvent _externalEvent;
        private readonly object _lock = new object();
        private TaskCompletionSource<string> _tcs;

        public RevitContextExtractionService()
        {
            _externalEvent = ExternalEvent.Create(this);
        }

        public Task<string> ExtractContextAsync()
        {
            lock (_lock)
            {
                _tcs = new TaskCompletionSource<string>();
            }
            
            _externalEvent.Raise();
            return _tcs.Task;
        }

        public void Execute(UIApplication app)
        {
            TaskCompletionSource<string> tcs;
            lock (_lock)
            {
                tcs = _tcs;
            }

            try
            {
                var uidoc = app.ActiveUIDocument;
                if (uidoc == null || uidoc.Document == null)
                {
                    tcs.TrySetResult("No active document.");
                    return;
                }

                var selectedIds = uidoc.Selection.GetElementIds();
                if (selectedIds.Count == 0)
                {
                    tcs.TrySetResult("No elements currently selected.");
                    return;
                }

                var doc = uidoc.Document;
                var categoryCounts = new Dictionary<string, int>();

                foreach (var id in selectedIds)
                {
                    var elem = doc.GetElement(id);
                    if (elem != null && elem.Category != null)
                    {
                        string catName = elem.Category.Name;
                        if (categoryCounts.ContainsKey(catName))
                            categoryCounts[catName]++;
                        else
                            categoryCounts[catName] = 1;
                    }
                }

                string summary = string.Join(", ", categoryCounts.Select(kv => $"{kv.Value} {kv.Key}"));
                string idList = string.Join(", ", selectedIds.Select(id => id.IntegerValue));

                string result = $"Selected Elements: {summary}. IDs: [{idList}]";
                tcs.TrySetResult(result);
            }
            catch (Exception ex)
            {
                tcs.TrySetResult($"Failed to extract context: {ex.Message}");
            }
        }

        public string GetName() => "Gemini Shell Context Extraction";
    }
}
