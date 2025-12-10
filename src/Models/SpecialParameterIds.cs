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
    internal static class SpecialParameterIds
    {
        public static readonly ElementId FamilyAndType =
            new ElementId(int.MinValue + 100);
    }
}
