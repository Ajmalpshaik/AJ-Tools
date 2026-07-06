#region Metadata
/*
 * Tool Name     : MEP Openings
 * File Name     : MepOpeningElementRule.cs
 * Purpose       : Stores one row of user-editable MEP opening settings.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-03
 * Last Updated  : 2026-07-03
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Newtonsoft.Json
 *
 * Input         : User-edited opening shape, cutout buffer, and future opening families.
 * Output        : Normalized settings per MEP element kind.
 *
 * Notes         :
 * - CutoutBufferMm is applied on each side after insulation is included.
 * - Duct and cable tray are always rectangular.
 *
 * Changelog     :
 * v1.0.0 (2026-07-03) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;
using Newtonsoft.Json;

namespace AJTools.Models.MepOpenings
{
    public sealed class MepOpeningElementRule
    {
        public MepOpeningElementKind ElementKind { get; set; }

        public MepOpeningShape Shape { get; set; }

        public bool IsIncluded { get; set; } = true;

        public double CutoutBufferMm { get; set; }

        public string OpeningFamilyName { get; set; }

        public string VerticalOpeningFamilyName { get; set; }

        public string HorizontalOpeningFamilyName { get; set; }

        [JsonIgnore]
        public string ElementName
        {
            get
            {
                switch (ElementKind)
                {
                    case MepOpeningElementKind.Pipe:
                        return "Pipe";
                    case MepOpeningElementKind.Duct:
                        return "Duct";
                    case MepOpeningElementKind.CableTray:
                        return "Cable Tray";
                    case MepOpeningElementKind.Conduit:
                        return "Conduit";
                    default:
                        return ElementKind.ToString();
                }
            }
        }

        [JsonIgnore]
        public bool CanUseCircle
        {
            get
            {
                return ElementKind == MepOpeningElementKind.Pipe ||
                       ElementKind == MepOpeningElementKind.Conduit;
            }
        }

        [JsonIgnore]
        public IList<MepOpeningShape> ShapeChoices
        {
            get
            {
                if (CanUseCircle)
                {
                    return new List<MepOpeningShape>
                    {
                        MepOpeningShape.Circle,
                        MepOpeningShape.Rectangle
                    };
                }

                return new List<MepOpeningShape>
                {
                    MepOpeningShape.Rectangle
                };
            }
        }

        public MepOpeningElementRule Clone()
        {
            return new MepOpeningElementRule
            {
                ElementKind = ElementKind,
                Shape = Shape,
                IsIncluded = IsIncluded,
                CutoutBufferMm = CutoutBufferMm,
                OpeningFamilyName = OpeningFamilyName,
                VerticalOpeningFamilyName = VerticalOpeningFamilyName,
                HorizontalOpeningFamilyName = HorizontalOpeningFamilyName
            };
        }

        public void Normalize()
        {
            if (!CanUseCircle)
            {
                Shape = MepOpeningShape.Rectangle;
            }

            if (CutoutBufferMm < 0)
            {
                CutoutBufferMm = 0;
            }

            if (OpeningFamilyName == null)
            {
                OpeningFamilyName = string.Empty;
            }

            if (VerticalOpeningFamilyName == null)
            {
                VerticalOpeningFamilyName = string.Empty;
            }

            if (HorizontalOpeningFamilyName == null)
            {
                HorizontalOpeningFamilyName = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(OpeningFamilyName))
            {
                if (string.IsNullOrWhiteSpace(VerticalOpeningFamilyName))
                {
                    VerticalOpeningFamilyName = OpeningFamilyName;
                }

                if (string.IsNullOrWhiteSpace(HorizontalOpeningFamilyName))
                {
                    HorizontalOpeningFamilyName = OpeningFamilyName;
                }
            }
        }
    }
}
