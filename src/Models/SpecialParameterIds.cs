// Tool Name: Filter Pro - Special Parameter Ids
// Description: Holds sentinel ElementId values for virtual parameters used in filter UI.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB

using Autodesk.Revit.DB;

namespace AJTools.Models
{
    /// <summary>
    /// Defines special virtual parameter identifiers that do not exist in Revit.
    /// These are used only inside the Filter Pro UI for combined or synthetic parameters,
    /// such as "Family + Type". Values must never collide with built-in or real parameter IDs.
    /// </summary>
    internal static class SpecialParameterIds
    {
        /// <summary>
        /// Sentinel ID used to represent the combined "Family + Type" virtual parameter.
        /// Uses a very low negative number to avoid collision with real ElementIds.
        /// </summary>
        public static readonly ElementId FamilyAndType =
            new ElementId(int.MinValue + 100);
    }
}
