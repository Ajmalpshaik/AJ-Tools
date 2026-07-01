#region Metadata
/*
 * Tool Name     : Filter Pro
 * File Name     : CmdFilterProAvailability.cs
 * Purpose       : Controls ribbon button availability — enabled only in filter-capable views.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-06-29
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Active View
 * Output        : Boolean availability state for the ribbon button
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - 2020 = .NET Fx 4.7.2; 2021-2024 = .NET Fx (verify 4.8 if required); 2025-2026 = .NET 8; 2027+ = verify Autodesk SDK.
 * - Verify the newest Revit version's required .NET target before building.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2025-12-10) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

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
                UIDocument uiDoc = applicationData?.ActiveUIDocument;
                if (uiDoc?.Document == null)
                    return false;

                Document doc = uiDoc.Document;
                if (doc.IsFamilyDocument)
                    return false;

                View activeView = uiDoc.ActiveView;
                if (activeView == null)
                    return false;

                return CanViewHaveFilters(activeView, out _);
            }
            catch
            {
                // If anything unexpected happens, keep the button disabled.
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
                reason = "Active view is null.";
                return false;
            }

            if (view.IsTemplate)
            {
                reason = "View templates do not host filters.";
                return false;
            }

            bool overridesAllowed;
            try
            {
                overridesAllowed = view.AreGraphicsOverridesAllowed();
            }
            catch (Exception ex)
            {
                // This call can throw on some unsupported views (schedules, sheets).
                reason = $"Could not check overrides: {ex.Message}";
                overridesAllowed = false;
            }

            // Explicitly allow Plan, Section, Elevation, and 3D views as requested.
            switch (view.ViewType)
            {
                case ViewType.FloorPlan:
                case ViewType.CeilingPlan:
                case ViewType.AreaPlan:
                case ViewType.Section:
                case ViewType.Elevation:
                case ViewType.ThreeD:
                    break;
                default:
                    reason = $"Filter Pro is only available in Plan, Section, Elevation, and 3D views.";
                    return false;
            }

            if (!overridesAllowed)
            {
                if (string.IsNullOrEmpty(reason))
                    reason = "Graphics overrides are disabled for this view.";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
