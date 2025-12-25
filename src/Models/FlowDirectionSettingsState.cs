// Tool Name: Duct Flow - Settings State
// Description: Stores last-used duct flow annotation settings per document.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-21
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB

using Autodesk.Revit.DB;

namespace AJTools.Models
{
    /// <summary>
    /// Snapshot of duct flow annotation settings.
    /// </summary>
    public class FlowDirectionSettingsState
    {
        public ElementId SymbolId { get; set; }

        public double SpacingInternal { get; set; }
    }
}
