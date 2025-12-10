// Tool Name: Filter Pro Availability
// Description: Determines whether the Filter Pro command is available in the current Revit context.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI
using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.Commands
{
    /// <summary>
    /// Controls the availability of the Filter Pro command in the Revit UI.
    /// </summary>
    public class CmdFilterProAvailability : IExternalCommandAvailability
    {
        /// <summary>
        /// Returns true if Filter Pro is available in the current document context.
        /// </summary>
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            try
            {
                if (applicationData?.ActiveUIDocument?.Document == null)
                    return false;

                Document doc = applicationData.ActiveUIDocument.Document;
                if (doc.IsFamilyDocument)
                    return false;

                View activeView = applicationData.ActiveUIDocument.ActiveView;
                if (activeView == null)
                    return false;
                
                return CanViewHaveFilters(activeView, out _);
            }
            catch
            {
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
    }
}
