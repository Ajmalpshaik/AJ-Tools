#region Metadata
/*
 * Tool Name     : Plan View Availability
 * File Name     : CmdPlanViewAvailability.cs
 * Purpose       : Ribbon availability filter — enables a button only in plan-family views
 *                 (Floor Plan, Ceiling Plan, Area Plan, Structural Plan). Used by tools that
 *                 only work in plan views (View Crop, Section Mark Visibility).
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
 * - Plan family matches the views the host tools actually accept (View Crop, Section Mark Visibility).
 * - Greyed out in section, elevation, 3D, drafting, legend, schedule, sheet, template, and family views.
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
    /// Enables a ribbon button only when the active view is a plan-family view.
    /// </summary>
    public class CmdPlanViewAvailability : IExternalCommandAvailability
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

                return IsPlanFamilyView(uiDoc.ActiveView);
            }
            catch
            {
                // Any unexpected issue keeps the button disabled rather than crashing the ribbon.
                return false;
            }
        }

        /// <summary>
        /// True for Floor Plan, Ceiling Plan, Area Plan, and Structural (Engineering) Plan views.
        /// </summary>
        internal static bool IsPlanFamilyView(View view)
        {
            if (view == null || !view.IsValidObject || view.IsTemplate)
                return false;

            switch (view.ViewType)
            {
                case ViewType.FloorPlan:
                case ViewType.CeilingPlan:
                case ViewType.AreaPlan:
                case ViewType.EngineeringPlan:
                    return true;
                default:
                    return false;
            }
        }
    }
}
