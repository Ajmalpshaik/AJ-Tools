#region Metadata
/*
 * Tool Name     : Graphical View Availability
 * File Name     : CmdGraphicalViewAvailability.cs
 * Purpose       : Ribbon availability filter — enables a button only in views where element
 *                 visibility and graphics overrides can be edited (AreGraphicsOverridesAllowed).
 *                 Used by the Graphics panel tools and Unhide All.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-06-30
 * Last Updated  : 2026-06-30
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
 * - Runs continuously while Revit is open; kept fast and exception-safe (returns false on any issue).
 * - Matches the actual gate used by the Graphics tools (View.AreGraphicsOverridesAllowed) and
 *   covers the same graphical views where Unhide All can hide/unhide elements.
 * - Enabled in plan, section, elevation, 3D, drafting, detail, and legend views.
 * - Greyed out in schedule, sheet, template, and family views.
 *
 * Changelog     :
 * v1.0.0 (2026-06-30) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.Commands
{
    /// <summary>
    /// Enables a ribbon button only when the active view allows graphics/visibility edits.
    /// </summary>
    public class CmdGraphicalViewAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            try
            {
                UIDocument uiDoc = applicationData?.ActiveUIDocument;
                if (uiDoc?.Document == null)
                    return false;

                if (uiDoc.Document.IsFamilyDocument)
                    return false;

                View view = uiDoc.ActiveView;
                if (view == null || !view.IsValidObject || view.IsTemplate)
                    return false;

                return view.AreGraphicsOverridesAllowed();
            }
            catch
            {
                // Any unexpected issue keeps the button disabled rather than crashing the ribbon.
                return false;
            }
        }
    }
}
