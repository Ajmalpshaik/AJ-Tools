// Tool Name: Smart MEP Tag - Priority Enum
// Description: Priority levels that control tagging order — high-priority elements claim space first.
// Author: Ajmal P.S.
// Version: 1.0.0
// Revit Version: 2020

namespace AJTools.Models.SmartTag
{
    /// <summary>
    /// Tagging priority that determines processing order.
    /// HIGH-priority elements are tagged first so they claim the best annotation space.
    /// </summary>
    internal enum TagPriority
    {
        High,
        Medium,
        Low
    }
}
