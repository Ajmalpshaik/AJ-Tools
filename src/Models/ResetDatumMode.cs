// Tool Name: Reset Datums Mode
// Description: Enumerates reset modes for grids and levels within Reset Datums commands.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: None

namespace AJTools.Models
{
    /// <summary>
    /// Specifies which datum types to reset during Reset Datum operations.
    /// </summary>
    internal enum ResetDatumMode
    {
        /// <summary>
        /// Reset both Grids and Levels.
        /// </summary>
        Combined,

        /// <summary>
        /// Reset only Grid elements.
        /// </summary>
        GridsOnly,

        /// <summary>
        /// Reset only Level elements.
        /// </summary>
        LevelsOnly
    }
}
