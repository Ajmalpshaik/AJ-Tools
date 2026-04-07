// Tool Name: Smart MEP Tag - Element Orientation Enum
// Description: Orientation of MEP elements — drives preferred tag placement direction.
// Author: Ajmal P.S.
// Version: 1.0.0
// Revit Version: 2020

namespace AJTools.Models.SmartTag
{
    /// <summary>
    /// Orientation of an MEP element in the active view.
    /// Horizontal elements prefer tags above/below; vertical elements prefer left/right.
    /// </summary>
    internal enum ElementOrientation
    {
        Horizontal,
        Vertical,
        Other
    }
}
