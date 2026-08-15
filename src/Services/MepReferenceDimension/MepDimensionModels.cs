#region Metadata
/*
 * Tool Name     : Auto MEP Dimension
 * File Name     : MepDimensionModels.cs
 * Purpose       : Data objects for the Auto MEP Dimension workflow - the measuring axis, one candidate
 *                 reference, a planned dimension, and the record used to recognise a dimension that
 *                 already exists.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-15
 * Last Updated  : 2026-08-15
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models.Dimensioning (DimensionEnums)
 *
 * Input         : Built by MepDimensionGeometry and MepDimensionCollector.
 * Output        : Consumed by MepReferenceDimensionService.
 *
 * Notes         :
 * - Replaces DuctReferenceDimensionModels. Two changes carry all the weight:
 *   1. Identity is ElementKey (a "linkInstanceId:elementId" string), never a bare ElementId. Element
 *      ids repeat across documents, so a linked wall could silently displace a host duct in any
 *      dictionary keyed on the id alone.
 *   2. Every candidate holds a HOST-space Reference, already converted out of its link if it came
 *      from one, so nothing downstream has to know whether a reference was linked.
 * - All coordinates in these objects are HOST coordinates.
 *
 * Changelog     :
 * v1.0.0 (2026-08-15) - Initial release, replacing DuctReferenceDimensionModels.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Models.Dimensioning;

namespace AJTools.Services.MepReferenceDimension
{
    /// <summary>
    /// The measuring line for one run: where it starts, which way it measures, and how far it looks.
    /// All vectors are unit length and lie in the view plane; all coordinates are host coordinates.
    /// </summary>
    internal sealed class DimensionAxis
    {
        /// <summary>Midpoint of the measured run, projected onto the view plane.</summary>
        public XYZ Origin { get; set; }

        /// <summary>Across the run - the direction the dimension measures along.</summary>
        public XYZ DimensionDirection { get; set; }

        /// <summary>Along the run.</summary>
        public XYZ RunDirection { get; set; }

        /// <summary>The view's normal.</summary>
        public XYZ ViewNormal { get; set; }

        /// <summary>Origin projected onto DimensionDirection.</summary>
        public double OriginDimensionCoord { get; set; }

        /// <summary>Origin projected onto RunDirection.</summary>
        public double OriginRunCoord { get; set; }

        /// <summary>How far along DimensionDirection to look for references, in feet.</summary>
        public double SearchHalfLength { get; set; }

        /// <summary>How far off the measuring line a face may sit and still count, in feet.</summary>
        public double AxisBandTolerance { get; set; }
    }

    /// <summary>
    /// One thing the dimension could point at, already resolved into host space.
    /// </summary>
    internal sealed class DimensionReferenceCandidate
    {
        /// <summary>"linkInstanceId:elementId" - unique across the host and every link.</summary>
        public string ElementKey { get; set; }

        /// <summary>The element's own id, for reporting only. Not unique across documents.</summary>
        public ElementId ElementId { get; set; }

        /// <summary>A reference the HOST document can dimension to.</summary>
        public Reference HostReference { get; set; }

        /// <summary>Stable representation of HostReference, built against the host document.</summary>
        public string StableKey { get; set; }

        /// <summary>Position along DimensionDirection. Drives ordering and spacing.</summary>
        public double SortCoord { get; set; }

        /// <summary>How far off the measuring line this candidate sits. Used to break ties.</summary>
        public double AxisOffset { get; set; }

        /// <summary>What kind of element this is.</summary>
        public DimensionTargetKind Kind { get; set; }

        /// <summary>True when this is a service run being measured rather than a reference datum.</summary>
        public bool IsMeasuredRun { get; set; }

        /// <summary>True when this candidate belongs to the run the user picked.</summary>
        public bool IsSeedRun { get; set; }

        /// <summary>"Current model" or the link's name, for the report.</summary>
        public string SourceName { get; set; }
    }

    /// <summary>
    /// A dimension the tool intends to create.
    /// </summary>
    internal sealed class DimensionPlan
    {
        /// <summary>The run this plan was built for, for reporting.</summary>
        public ElementId SeedElementId { get; set; }

        /// <summary>Identity of the run this plan measures.</summary>
        public string SeedElementKey { get; set; }

        public DimensionAxis Axis { get; set; }

        /// <summary>The line the dimension sits on, in host coordinates.</summary>
        public Line DimensionLine { get; set; }

        /// <summary>References in order along DimensionDirection. At least two.</summary>
        public IList<DimensionReferenceCandidate> References { get; set; }

        /// <summary>Identities of every run this dimension documents.</summary>
        public IList<string> CoveredElementKeys { get; set; }

        /// <summary>Builds the ReferenceArray Revit needs, preserving order.</summary>
        public ReferenceArray ToReferenceArray()
        {
            ReferenceArray array = new ReferenceArray();
            if (References == null)
                return array;

            foreach (DimensionReferenceCandidate candidate in References)
            {
                if (candidate?.HostReference != null)
                    array.Append(candidate.HostReference);
            }

            return array;
        }
    }

    /// <summary>The outcome of planning one run.</summary>
    internal sealed class DimensionPlanResult
    {
        public IList<DimensionPlan> Plans { get; set; }
        public IList<DimensionPlanFailure> Failures { get; set; }

        public bool HasPlans
        {
            get { return Plans != null && Plans.Count > 0; }
        }
    }

    /// <summary>A run that could not be planned, and why.</summary>
    internal sealed class DimensionPlanFailure
    {
        public ElementId ElementId { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>
    /// A fingerprint of a dimension that exists (or is about to), used to avoid stacking a second
    /// identical dimension on top of the first.
    /// </summary>
    internal sealed class DimensionLineRecord
    {
        public XYZ DimensionDirection { get; set; }
        public XYZ RunDirection { get; set; }

        /// <summary>Position of the dimension line across the run.</summary>
        public double RunCoord { get; set; }

        public double MinDimensionCoord { get; set; }
        public double MaxDimensionCoord { get; set; }

        /// <summary>Stable representations of every reference this dimension carries.</summary>
        public HashSet<string> StableReferenceKeys { get; set; }
    }
}
