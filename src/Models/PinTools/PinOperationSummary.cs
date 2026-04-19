// Tool Name: Pin Operation Summary Model
// Description: Captures pin/unpin execution counts for result reporting.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-15
// Revit Version: 2020

namespace AJTools.Models.PinTools
{
    /// <summary>
    /// Summary of a pin or unpin run.
    /// </summary>
    internal sealed class PinOperationSummary
    {
        public int TargetedCount { get; set; }

        public int UpdatedCount { get; set; }

        public int UnchangedCount { get; set; }

        public int SkippedCount { get; set; }
    }
}
