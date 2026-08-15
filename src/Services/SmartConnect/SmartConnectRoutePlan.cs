// Tool Name: Connect MEP Elements (Smart Connect) - Route Plan
// Description: Geometry plan and result types shared by the Connect MEP Elements route builder.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-08-15
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, AJTools.Models

using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Models;

namespace AJTools.Services.SmartConnect
{
    /// <summary>
    /// The geometric relationship between the two open ends being joined.
    /// </summary>
    internal enum SmartConnectPlanKind
    {
        /// <summary>Both ends sit on one straight line and face each other - a straight bridging piece.</summary>
        Inline = 0,

        /// <summary>The two run axes cross - both ends reach one corner point and share a single elbow.</summary>
        Corner = 1,

        /// <summary>The ends are parallel and facing, but offset sideways - the classic two elbow crank.</summary>
        ParallelOffset = 2,

        /// <summary>The axes neither meet nor run parallel - bridged on their common perpendicular.</summary>
        Skew = 3
    }

    /// <summary>
    /// A resolved geometric plan for one connection: where each end has to finish up,
    /// and how far each end travels along its own outward direction to get there.
    /// </summary>
    internal sealed class SmartConnectRoutePlan
    {
        public SmartConnectPlanKind Kind { get; set; }

        public Connector FirstConnector { get; set; }

        public Connector SecondConnector { get; set; }

        /// <summary>Outward direction of the first end (away from its own element).</summary>
        public XYZ FirstDirection { get; set; }

        public XYZ SecondDirection { get; set; }

        /// <summary>Where the first end has to finish up.</summary>
        public XYZ FirstPlanPoint { get; set; }

        public XYZ SecondPlanPoint { get; set; }

        /// <summary>
        /// Travel along <see cref="FirstDirection"/> to reach <see cref="FirstPlanPoint"/>.
        /// Positive extends the run, negative pulls it back.
        /// </summary>
        public double FirstShift { get; set; }

        public double SecondShift { get; set; }

        /// <summary>True when a bridging segment is needed between the two plan points.</summary>
        public bool NeedsMiddleSegment { get; set; }

        /// <summary>The bend angle this plan actually produces, in degrees.</summary>
        public double ResultingAngleDegrees { get; set; }

        /// <summary>Set when the plan could not honour the requested angle and used the geometry's own instead.</summary>
        public bool AngleFixedByGeometry { get; set; }
    }

    /// <summary>
    /// Outcome of one connection attempt.
    /// </summary>
    internal sealed class SmartConnectRouteResult
    {
        public bool Success { get; set; }

        public string ErrorMessage { get; set; }

        public List<string> Warnings { get; } = new List<string>();

        public SmartConnectRoutingMode ModeUsed { get; set; }

        public SmartConnectPlanKind PlanUsed { get; set; }

        public double AngleUsedDegrees { get; set; }

        /// <summary>
        /// True when the shape of the two runs dictated the bend, so the requested angle never
        /// applied. Only <see cref="SmartConnectPlanKind.ParallelOffset"/> is actually driven by the
        /// chosen angle; in-line, corner and skew routes take their angle from the geometry.
        /// </summary>
        public bool AngleFixedByGeometry { get; set; }

        /// <summary>True when the tool had to depart from the requested angle.</summary>
        public bool AngleWasSubstituted { get; set; }

        public static SmartConnectRouteResult Failed(string message)
        {
            return new SmartConnectRouteResult
            {
                Success = false,
                ErrorMessage = message
            };
        }

        public string DescribePlan()
        {
            switch (PlanUsed)
            {
                case SmartConnectPlanKind.Inline:
                    return "straight bridge";
                case SmartConnectPlanKind.Corner:
                    return "single corner elbow";
                case SmartConnectPlanKind.ParallelOffset:
                    return "offset crank";
                case SmartConnectPlanKind.Skew:
                    return "skewed bridge";
                default:
                    return "route";
            }
        }
    }
}
