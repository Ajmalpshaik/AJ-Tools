#region Metadata
/*
 * Tool Name     : Colorize
 * File Name     : CmdColorizeAvailability.cs
 * Purpose       : Controls ribbon button availability — enabled only in the same views where
 *                 Filter Pro can operate (Plan, Section, Elevation, 3D, with overrides allowed).
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-02
 * Last Updated  : 2026-07-02
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
 * - Colorize matches elements the same way Filter Pro does (category + parameter rules), so it
 *   reuses CmdFilterProAvailability.CanViewHaveFilters rather than duplicating the ViewType gate.
 * - Runs continuously while Revit is open; kept fast and exception-safe (returns false on any issue).
 *
 * Changelog     :
 * v1.0.0 (2026-07-02) - Initial release, built for the Colorize tool.
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
    /// Enables the Colorize ribbon button only when the active view supports graphics overrides
    /// the same way Filter Pro requires (Plan, Section, Elevation, 3D).
    /// </summary>
    public class CmdColorizeAvailability : IExternalCommandAvailability
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

                View activeView = uiDoc.ActiveView;
                if (activeView == null)
                    return false;

                return CmdFilterProAvailability.CanViewHaveFilters(activeView, out _);
            }
            catch
            {
                // Any unexpected issue keeps the button disabled rather than crashing the ribbon.
                return false;
            }
        }
    }
}
