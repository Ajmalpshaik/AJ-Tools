#region Metadata
/*
 * Tool Name     : Filter Pro
 * File Name     : FilterValueItem.cs
 * Purpose       : Immutable wrapper holding a parameter value's display string, raw typed value,
 *                 StorageType, and optional ElementId reference for filter rule creation.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2025-12-10
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Populated by FilterProDataProvider.GetValues()
 * Output        : Bound to the values ListBox; consumed by FilterCreator.BuildRules()
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - RawValue type varies by StorageType: string, int, double, ElementId, or Tuple<string,string> for Family+Type.
 * - Plain data container — no Revit API calls, no side effects.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2025-12-10) - Initial release.
 * v1.0.1 (2026-06-30) - Added mandatory metadata block; confirmed 2020-latest version coverage.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.DB;

namespace AJTools.Models
{
    internal class FilterValueItem
    {
        public FilterValueItem(
            string display,
            object rawValue,
            StorageType storageType,
            ElementId elementId = null)
        {
            Display = display;
            RawValue = rawValue;
            StorageType = storageType;
            ElementId = elementId;
        }

        public string Display { get; }
        public object RawValue { get; }
        public StorageType StorageType { get; }
        public ElementId ElementId { get; }

        public override string ToString() => Display;
    }
}
