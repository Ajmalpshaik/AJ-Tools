// Tool Name: Smart MEP Tag - Skip Reason Enum
// Description: Reasons an element was not tagged — used in the output report.
// Author: Ajmal P.S.
// Version: 1.0.0
// Revit Version: 2020

namespace AJTools.Models.SmartTag
{
    /// <summary>
    /// Reason why an element was skipped or failed during the tagging process.
    /// Each value maps to a line in the final output report.
    /// </summary>
    internal enum TagSkipReason
    {
        None,
        AlreadyTagged,
        FilteredOutSize,
        FilteredOutType,
        FilteredOutVisibility,
        OutsideCropRegion,
        DenseZoneSkipped,
        PartOfTaggedGroup,
        NoTagFamilyAvailable,
        NoCleanSpaceAvailable
    }
}
