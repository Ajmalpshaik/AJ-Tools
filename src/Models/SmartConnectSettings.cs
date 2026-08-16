// Tool Name: Connect MEP Elements (Smart Connect) - Settings Model
// Description: Stores persisted Connect MEP Elements routing, batch, and quality preferences.
// Author: Ajmal P.S.
// Version: 2.0.0
// Last Updated: 2026-08-15
// Revit Version: 2020
// Dependencies: System.Collections.Generic

using System.Collections.Generic;

namespace AJTools.Models
{
    /// <summary>
    /// Routing modes supported by Connect MEP Elements.
    /// </summary>
    public enum SmartConnectRoutingMode
    {
        /// <summary>Trim both open ends back and bridge them with one middle segment and two elbows.</summary>
        SingleElbow = 0,

        /// <summary>Build a three segment offset run (rolling offset) between the two open ends.</summary>
        OffsetWithTwoElbows = 1,

        /// <summary>Try Single Elbow first, then fall back to the offset route.</summary>
        Auto = 2
    }

    /// <summary>
    /// Controls which of the two picked elements may have its end moved.
    /// </summary>
    public enum SmartConnectMoveMode
    {
        /// <summary>Share the required shift equally between both elements.</summary>
        Both = 0,

        /// <summary>Only the first picked element may move; the second stays put.</summary>
        FirstOnly = 1,

        /// <summary>Only the second picked element may move; the first stays put.</summary>
        SecondOnly = 2
    }

    /// <summary>
    /// Persisted Connect MEP Elements settings.
    /// </summary>
    public class SmartConnectSettings
    {
        // --- Routing ---

        public SmartConnectRoutingMode RoutingMode { get; set; } = SmartConnectRoutingMode.Auto;

        public double SelectedAngleDegrees { get; set; } = 90.0;

        public List<double> CustomAngles { get; set; } = new List<double>();

        /// <summary>
        /// When the chosen angle cannot be built, retry with the fallback angles instead of failing.
        /// </summary>
        public bool UseAngleFallback { get; set; } = true;

        public List<double> FallbackAngles { get; set; } = new List<double> { 45.0, 30.0, 60.0, 90.0 };

        // --- What may be connected ---

        /// <summary>Allow open ends that are not parallel (skewed or perpendicular runs).</summary>
        public bool AllowNonParallelEnds { get; set; } = true;

        /// <summary>Allow picking equipment, air terminals, fittings and accessories, not just straight runs.</summary>
        public bool AllowFamilyInstanceConnectors { get; set; } = true;

        /// <summary>Allow Conduit as a supported category.</summary>
        public bool AllowConduit { get; set; } = true;

        /// <summary>Allow Flex Duct and Flex Pipe as supported categories.</summary>
        public bool AllowFlexCurves { get; set; } = true;

        // --- Element movement ---

        public SmartConnectMoveMode MoveMode { get; set; } = SmartConnectMoveMode.Both;

        // --- Selection and reporting ---

        /// <summary>
        /// When exactly two supported elements are already selected, connect them directly instead
        /// of asking for picks on screen. No pairing or matching happens - the two selected elements
        /// are simply the pair.
        /// </summary>
        public bool BatchFromSelection { get; set; } = true;

        /// <summary>
        /// While picking pairs, carry a failed attempt into the next pick prompt rather than
        /// interrupting with a popup.
        /// </summary>
        public bool ShowSummaryReport { get; set; } = true;

        // --- Quality of the created run ---

        /// <summary>Copy insulation and lining from the source run onto the new segments.</summary>
        public bool CopyInsulationAndLining { get; set; } = true;

        /// <summary>Copy Comments, Mark and Workset from the source run onto the new segments.</summary>
        public bool CopyInstanceParameters { get; set; } = true;

        /// <summary>Insert a transition fitting when the two picked elements are different sizes.</summary>
        public bool AutoTransitionOnSizeMismatch { get; set; } = true;

        /// <summary>Report a warning when the created route overlaps other model geometry.</summary>
        public bool WarnOnClash { get; set; }
    }
}
