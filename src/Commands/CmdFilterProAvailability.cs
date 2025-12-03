using System;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.Commands
{
    /// <summary>
    /// Controls the availability of the Filter Pro command in the Revit UI.
    /// DIAGNOSTIC VERSION - Logs to help debug availability issues.
    /// </summary>
    public class CmdFilterProAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            string logPath = Path.Combine(Path.GetTempPath(), "FilterPro_Availability_Debug.txt");

            try
            {
                // Log start
                SafeLog(logPath, $"\n\n=== Availability Check at {DateTime.Now:HH:mm:ss} ===\n");

                // Basic null checks
                if (applicationData == null)
                {
                    SafeLog(logPath, "FAIL: applicationData is null\n");
                    return false;
                }
                SafeLog(logPath, "PASS: applicationData exists\n");

                UIDocument uidoc = applicationData.ActiveUIDocument;
                if (uidoc == null)
                {
                    SafeLog(logPath, "FAIL: ActiveUIDocument is null\n");
                    return false;
                }
                SafeLog(logPath, "PASS: ActiveUIDocument exists\n");

                Document doc = uidoc.Document;
                if (doc == null)
                {
                    SafeLog(logPath, "FAIL: Document is null\n");
                    return false;
                }
                SafeLog(logPath, $"PASS: Document exists: {doc.Title}\n");

                // Disable for family documents
                if (doc.IsFamilyDocument)
                {
                    SafeLog(logPath, "FAIL: Document is a family document\n");
                    return false;
                }
                SafeLog(logPath, "PASS: Not a family document\n");

                // Warn for read-only documents but allow the button so the command can show a clearer message
                if (doc.IsReadOnly)
                {
                    SafeLog(logPath, "WARN: Document is read-only (command will show a blocking message)\n");
                }
                else
                {
                    SafeLog(logPath, "PASS: Document is not read-only\n");
                }

                View activeView = uidoc.ActiveView;
                if (activeView == null)
                {
                    SafeLog(logPath, "FAIL: Active view is null\n");
                    return false;
                }
                SafeLog(logPath, $"PASS: Active view exists: {activeView.Name}\n");
                SafeLog(logPath, $"      View Type: {activeView.ViewType}\n");
                SafeLog(logPath, $"      Is Template: {activeView.IsTemplate}\n");

                // Only enable for views that support filters
                bool canUse = CanViewHaveFilters(activeView, out string reason);

                if (canUse)
                {
                    SafeLog(logPath, "SUCCESS: All checks passed - BUTTON ENABLED\n");
                }
                else
                {
                    SafeLog(logPath, $"FAIL: {reason}\n");
                }

                return canUse;
            }
            catch (Exception ex)
            {
                SafeLog(logPath, $"EXCEPTION: {ex.Message}\n");
                SafeLog(logPath, $"Stack: {ex.StackTrace}\n");
                // On error, default to disabled
                return false;
            }
        }

        /// <summary>
        /// Checks if the view supports parameter filters.
        /// </summary>
        internal static bool CanViewHaveFilters(View view, out string reason)
        {
            reason = string.Empty;

            if (view == null)
            {
                reason = "Active view is null";
                return false;
            }

            if (view.IsTemplate)
            {
                reason = "View templates do not host filters";
                return false;
            }

            bool overridesAllowed = true;
            try
            {
                overridesAllowed = view.AreGraphicsOverridesAllowed();
            }
            catch (Exception ex)
            {
                // This call can throw on some unsupported views (schedules, sheets)
                reason = $"Could not check overrides: {ex.Message}";
            }

            // Explicitly block known unsupported view types
            switch (view.ViewType)
            {
                case ViewType.ProjectBrowser:
                case ViewType.SystemBrowser:
                case ViewType.DrawingSheet:
                case ViewType.Schedule:
                case ViewType.ColumnSchedule:
                case ViewType.PanelSchedule:
                case ViewType.Report:
                case ViewType.Legend:
                case ViewType.Rendering:
                case ViewType.Walkthrough:
                    reason = $"Filters are not available in {view.ViewType} views";
                    return false;
            }

            if (!overridesAllowed)
            {
                if (string.IsNullOrEmpty(reason))
                    reason = "Graphics overrides are disabled for this view";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static void SafeLog(string path, string text)
        {
            try
            {
                File.AppendAllText(path, text);
            }
            catch
            {
                // Logging must not block availability
            }
        }
    }
}
