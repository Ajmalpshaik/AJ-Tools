// Tool Name: Connect MEP Elements (Smart Connect) - Settings Model
// Description: Stores persisted Connect MEP Elements angle, picking, movement and quality preferences.
// Author: Ajmal P.S.
// Version: 3.0.0
// Last Updated: 2026-08-16
// Revit Version: 2020
// Dependencies: System.Collections.Generic
//
// v3.0.0 - Three changes, all from Ajmal on 2026-08-16:
//   1. SmartConnectRoutingMode removed entirely ("instead of creating new pipe or duct, stretch the
//      elements like that we need"). There is no longer a choice between stretching the picked
//      elements and leaving them alone while a parallel set of new pieces is built beside them. The
//      tool ALWAYS stretches what was picked. A new piece is created only where there is nothing to
//      stretch: the bridging run across an offset crank (which has to exist - it IS the connection),
//      and the run up to a flex duct or piece of equipment, neither of which can ever be lengthened.
//   2. The two grouped "what may I pick" flags were split one-per-category, so each can be switched
//      independently. AllowEquipment is deliberately the catch-all for every remaining
//      connector-bearing family instance (plumbing fixtures, sprinklers, electrical equipment), so
//      splitting the old grouped flag could not silently drop a category that used to be pickable.
//   3. CopyInstanceParameters became CopyWorkset - Comments and Mark are no longer copied.

using System.Collections.Generic;

namespace AJTools.Models
{
    /// <summary>
    /// Controls which of the two picked elements may have its end stretched or trimmed.
    /// </summary>
    /// <remarks>
    /// There is no "neither" member on purpose. An element that genuinely cannot be stretched -
    /// flex, equipment - is detected from the element itself, not chosen here, so a "neither" option
    /// would only ever duplicate that and let the two disagree.
    /// </remarks>
    public enum SmartConnectMoveMode
    {
        /// <summary>Share the required travel equally between both elements.</summary>
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
        // --- Bend angle ---

        /// <summary>The bend angle to build an offset crank at. Clamped to 5-90 by the settings service.</summary>
        public double SelectedAngleDegrees { get; set; } = 90.0;

        /// <summary>Angles the user added by hand, shown as a list beside the 45 and 90 presets.</summary>
        public List<double> CustomAngles { get; set; } = new List<double>();

        /// <summary>
        /// When the chosen angle cannot be built, retry with the fallback angles instead of failing.
        /// </summary>
        public bool UseAngleFallback { get; set; } = true;

        /// <summary>
        /// Try-order, not a display list - the settings service deliberately does not sort this.
        /// </summary>
        public List<double> FallbackAngles { get; set; } = new List<double> { 45.0, 30.0, 60.0, 90.0 };

        // --- What may be picked. Pipe, Duct and Cable Tray are always allowed and have no flag. ---

        /// <summary>Allow Conduit.</summary>
        public bool AllowConduit { get; set; } = true;

        /// <summary>Allow Flex Duct. Connected to, never stretched - a piece is run up to it.</summary>
        public bool AllowFlexDuct { get; set; } = true;

        /// <summary>Allow Flex Pipe. Connected to, never stretched - a piece is run up to it.</summary>
        public bool AllowFlexPipe { get; set; } = true;

        /// <summary>Allow Air Terminals - grilles and diffusers.</summary>
        public bool AllowAirTerminals { get; set; } = true;

        /// <summary>
        /// Allow equipment. This is the catch-all for every connector-bearing family instance that
        /// is not an air terminal, fitting or accessory - mechanical equipment, plumbing fixtures,
        /// sprinklers, electrical equipment and anything else with a spare connector.
        /// </summary>
        public bool AllowEquipment { get; set; } = true;

        /// <summary>Allow pipe, duct, cable tray and conduit fittings that still have a spare connector.</summary>
        public bool AllowFittings { get; set; } = true;

        /// <summary>Allow pipe and duct accessories - valves, dampers and similar.</summary>
        public bool AllowAccessories { get; set; } = true;

        // --- Shapes it will accept ---

        /// <summary>Allow open ends that are not parallel - runs that cross at an angle, or pass by each other.</summary>
        public bool AllowNonParallelEnds { get; set; } = true;

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
        /// Show a popup naming the reason when a connection fails. Off means the failure is silent
        /// and the user simply tries again.
        /// </summary>
        public bool ShowFailedReport { get; set; } = true;

        // --- Quality of the created run ---

        /// <summary>Copy insulation and lining from the source run onto any piece the tool creates.</summary>
        public bool CopyInsulationAndLining { get; set; } = true;

        /// <summary>Put any piece the tool creates on the same workset as the run it joins.</summary>
        public bool CopyWorkset { get; set; } = true;

        /// <summary>Insert a transition fitting when the two picked elements are different sizes.</summary>
        public bool AutoTransitionOnSizeMismatch { get; set; } = true;

        /// <summary>Report a warning when the created route overlaps other model geometry.</summary>
        public bool WarnOnClash { get; set; }
    }
}
