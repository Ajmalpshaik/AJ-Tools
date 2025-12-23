// Tool Name: Copy View Range
// Description: Implements the Copy View Range command and supporting model snapshot logic.
// Author: Ajmal P.S.
// Version: 1.0.1
// Last Updated: 2025-12-11
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, System.Windows.Forms

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.CopyViewRange;
using AJTools.UI;

namespace AJTools.Commands
{
    using ViewRangeSnapshot = AJTools.Services.CopyViewRange.CopyViewRangeModel;

    [Transaction(TransactionMode.Manual)]
    internal class CmdCopyViewRange : IExternalCommand
    {
        private const string Title = "AJ Tools - Copy View Range";
        private const string EnvPrefix = "AJTOOLS_CVR_";

        // Memory cache per document (fastest).
        private static readonly Dictionary<string, ViewRangeSnapshot> _memoryCache
            = new Dictionary<string, ViewRangeSnapshot>(StringComparer.OrdinalIgnoreCase);

        // Disk cache for recovery across command reloads in the same session.
        private static readonly string CacheFolder = Path.Combine(Path.GetTempPath(), "AJTools", "CopyViewRange");

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application?.ActiveUIDocument;
            if (uiDoc == null)
                return Result.Failed;

            Document doc = uiDoc.Document;
            if (!(doc.ActiveView is ViewPlan activePlan) || activePlan.IsTemplate)
            {
                TaskDialog.Show(Title, "Please activate a Floor/Ceiling/Engineering plan view first.");
                return Result.Failed;
            }

            bool hasCache = HasValidCache(doc);
            TaskDialogResult choice = ShowActionDialog(hasCache);

            if (choice == TaskDialogResult.CommandLink1)
                return DoCopy(activePlan);

            if (choice == TaskDialogResult.CommandLink2)
                return DoPasteActive(doc, activePlan);

            if (choice == TaskDialogResult.CommandLink3)
                return DoPasteMultiple(doc, activePlan);

            return Result.Cancelled;
        }

        private static TaskDialogResult ShowActionDialog(bool hasCache)
        {
            var dialog = new TaskDialog(Title)
            {
                MainInstruction = hasCache ? "Copy or Paste View Range" : "Copy View Range",
                MainContent = hasCache
                    ? "Clipboard contains a view range for this project. Select an action:"
                    : "Copy the active view's range to clipboard.",
                CommonButtons = TaskDialogCommonButtons.Cancel,
                AllowCancellation = true
            };

            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Copy from Active View");

            if (hasCache)
            {
                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Paste to Active View");
                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "Paste to Multiple Views");
            }

            return dialog.Show();
        }

        private static Result DoCopy(ViewPlan view)
        {
            try
            {
                ViewRangeSnapshot snapshot = ViewRangeSnapshot.From(view);
                SaveCache(view.Document, snapshot);

                TaskDialog.Show(
                    Title,
                    $"Copied view range from '{view.Name}'.\n\nYou can now paste it into other views in this project.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(Title, "Error copying view range:\n" + ex.Message);
                return Result.Failed;
            }
        }

        private static Result DoPasteActive(Document doc, ViewPlan view)
        {
            ViewRangeSnapshot snapshot = GetSnapshot(doc);
            if (snapshot == null)
            {
                TaskDialog.Show(Title, "Nothing copied yet in this document. Copy a view range first.");
                return Result.Cancelled;
            }

            using (Transaction t = new Transaction(doc, "Paste View Range"))
            {
                t.Start();
                if (snapshot.TryApplyTo(view, out string reason))
                {
                    t.Commit();
                    TaskDialog.Show(Title, "Successfully pasted view range.");
                    return Result.Succeeded;
                }

                t.RollBack();
                TaskDialog.Show(Title, $"Failed to paste.\nReason: {reason}");
                return Result.Cancelled;
            }
        }

        private static Result DoPasteMultiple(Document doc, ViewPlan sourceView)
        {
            ViewRangeSnapshot snapshot = GetSnapshot(doc);
            if (snapshot == null)
            {
                TaskDialog.Show(Title, "Nothing copied yet in this document. Copy a view range first.");
                return Result.Cancelled;
            }

            IList<ViewPlan> plans = GetEligibleViews(doc);
            if (plans.Count == 0)
            {
                TaskDialog.Show(Title, "No plan views are available to receive the copied view range.");
                return Result.Cancelled;
            }

            using (var form = new ViewSelectionForm(plans, sourceView))
            {
                if (form.ShowDialog() != DialogResult.OK || form.SelectedViews.Count == 0)
                    return Result.Cancelled;

                IList<ViewPlan> targets = FilterTargets(form.SelectedViews);
                if (targets.Count == 0)
                    return Result.Cancelled;

                int successCount = 0;
                List<string> errorLog = new List<string>();

                using (Transaction t = new Transaction(doc, "Paste View Range - Multiple"))
                {
                    t.Start();

                    foreach (ViewPlan target in targets)
                    {
                        if (snapshot.TryApplyTo(target, out string reason))
                            successCount++;
                        else
                            errorLog.Add($"- {target.Name}: {reason}");
                    }

                    t.Commit();
                }

                var summary = new StringBuilder();
                summary.AppendLine($"Updated {successCount} view{(successCount == 1 ? string.Empty : "s")} successfully.");

                if (errorLog.Count > 0)
                {
                    summary.AppendLine();
                    summary.AppendLine($"Skipped {errorLog.Count} view{(errorLog.Count == 1 ? string.Empty : "s")}:");
                    foreach (string err in errorLog.Take(10))
                        summary.AppendLine(err);
                    if (errorLog.Count > 10)
                        summary.AppendLine("...and others.");
                }

                TaskDialog.Show(Title, summary.ToString());
                return successCount > 0 ? Result.Succeeded : Result.Cancelled;
            }
        }

        private static IList<ViewPlan> GetEligibleViews(Document doc)
        {
            var allowed = new HashSet<ViewType>
            {
                ViewType.FloorPlan,
                ViewType.CeilingPlan,
                ViewType.EngineeringPlan
            };

            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .Where(v => !v.IsTemplate && allowed.Contains(v.ViewType))
                .OrderBy(v => v.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static IList<ViewPlan> FilterTargets(IEnumerable<ViewPlan> selectedViews)
        {
            var result = new List<ViewPlan>();
            var seenIds = new HashSet<int>();

            foreach (ViewPlan view in selectedViews)
            {
                if (view == null)
                    continue;

                if (seenIds.Add(view.Id.IntegerValue))
                    result.Add(view);
            }

            return result;
        }

        private static ViewRangeSnapshot GetSnapshot(Document doc)
        {
            string documentKey = GetDocumentKey(doc);
            if (string.IsNullOrWhiteSpace(documentKey))
                return null;

            if (_memoryCache.TryGetValue(documentKey, out ViewRangeSnapshot memorySnapshot))
                return memorySnapshot;

            if (TryLoadCacheExternal(documentKey, out ViewRangeSnapshot snapshot))
            {
                _memoryCache[documentKey] = snapshot;
                return snapshot;
            }

            return null;
        }

        private static bool HasValidCache(Document doc)
        {
            return GetSnapshot(doc) != null;
        }

        private static void SaveCache(Document doc, ViewRangeSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            string documentKey = GetDocumentKey(doc);
            if (string.IsNullOrWhiteSpace(documentKey))
                return;

            _memoryCache[documentKey] = snapshot;
            SaveCacheExternal(documentKey, snapshot);
        }

        private static string GetDocumentKey(Document doc)
        {
            if (doc == null)
                return null;

            string projectId = doc.ProjectInformation?.UniqueId;
            if (!string.IsNullOrWhiteSpace(projectId))
                return projectId;

            string path = doc.PathName;
            if (!string.IsNullOrWhiteSpace(path))
                return path;

            string title = doc.Title ?? string.Empty;
            title = title.Trim();
            if (title.EndsWith("*", StringComparison.Ordinal))
                title = title.TrimEnd('*').TrimEnd();

            return title;
        }

        private static void SaveCacheExternal(string documentKey, ViewRangeSnapshot snapshot)
        {
            string payload = snapshot.SerializeCache();
            string cacheKey = ComputeHash(documentKey);

            try
            {
                Environment.SetEnvironmentVariable(EnvPrefix + cacheKey, payload, EnvironmentVariableTarget.Process);
            }
            catch
            {
                // Best-effort cache; failures should not block the command.
            }

            try
            {
                Directory.CreateDirectory(CacheFolder);
                string path = Path.Combine(CacheFolder, $"clipboard_{cacheKey}.txt");
                File.WriteAllText(path, payload, Encoding.UTF8);
            }
            catch
            {
                // Best-effort cache; failures should not block the command.
            }
        }

        private static bool TryLoadCacheExternal(string documentKey, out ViewRangeSnapshot snapshot)
        {
            snapshot = null;
            string cacheKey = ComputeHash(documentKey);

            try
            {
                string payload = Environment.GetEnvironmentVariable(EnvPrefix + cacheKey, EnvironmentVariableTarget.Process);
                if (ViewRangeSnapshot.TryDeserializeCache(payload, out snapshot))
                    return true;
            }
            catch
            {
                // Ignore cache failures.
            }

            try
            {
                string path = Path.Combine(CacheFolder, $"clipboard_{cacheKey}.txt");
                if (!File.Exists(path))
                    return false;

                string payload = File.ReadAllText(path, Encoding.UTF8);
                return ViewRangeSnapshot.TryDeserializeCache(payload, out snapshot);
            }
            catch
            {
                return false;
            }
        }

        private static string ComputeHash(string input)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input);
                byte[] hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
