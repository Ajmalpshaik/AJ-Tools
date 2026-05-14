// Tool Name: Duct Reference Dimension Models
// Description: Data objects used by the AJ Annotation duct reference dimension workflow.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-05-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB

using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace AJTools.Services.DuctReferenceDimension
{
    internal enum DuctReferenceTargetType
    {
        Wall,
        StructuralColumn,
        StructuralBeam,
        Duct
    }

    internal sealed class DuctDimensionAxis
    {
        public XYZ Origin { get; set; }
        public XYZ DimensionDirection { get; set; }
        public XYZ DuctDirection { get; set; }
        public XYZ ViewNormal { get; set; }
        public double OriginDimensionCoord { get; set; }
        public double OriginDuctCoord { get; set; }
        public double SearchHalfLength { get; set; }
        public double AxisBandTolerance { get; set; }
    }

    internal sealed class DuctReferenceCandidate
    {
        public ElementId ElementId { get; set; }
        public Reference Reference { get; set; }
        public double SortCoord { get; set; }
        public double AxisOffset { get; set; }
        public DuctReferenceTargetType TargetType { get; set; }
        public bool IsDuct { get; set; }
        public bool IsSelectedDuct { get; set; }
        public string StableKey { get; set; }
    }

    internal sealed class DuctDimensionPlan
    {
        public ElementId SelectedDuctId { get; set; }
        public DuctDimensionAxis Axis { get; set; }
        public Line DimensionLine { get; set; }
        public IList<DuctReferenceCandidate> References { get; set; }
        public IList<ElementId> CoveredDuctIds { get; set; }

        public ReferenceArray ToReferenceArray()
        {
            ReferenceArray array = new ReferenceArray();
            if (References == null)
                return array;

            foreach (DuctReferenceCandidate candidate in References)
            {
                if (candidate?.Reference != null)
                    array.Append(candidate.Reference);
            }

            return array;
        }
    }

    internal sealed class DuctDimensionBuildResult
    {
        public bool Succeeded { get; set; }
        public DuctDimensionPlan Plan { get; set; }
        public string FailureReason { get; set; }
    }

    internal sealed class DuctDimensionBatchBuildResult
    {
        public IList<DuctDimensionPlan> Plans { get; set; }
        public IList<DuctDimensionFailure> Failures { get; set; }

        public bool HasPlans
        {
            get
            {
                return Plans != null && Plans.Count > 0;
            }
        }
    }

    internal sealed class DuctDimensionFailure
    {
        public ElementId ElementId { get; set; }
        public string Reason { get; set; }
    }

    internal sealed class DuctDimensionLineRecord
    {
        public ElementId DuctId { get; set; }
        public XYZ DimensionDirection { get; set; }
        public XYZ DuctDirection { get; set; }
        public double DuctCoord { get; set; }
        public double MinDimensionCoord { get; set; }
        public double MaxDimensionCoord { get; set; }
        public HashSet<string> StableReferenceKeys { get; set; }
    }
}
