#region Metadata
/*
 * Tool Name     : Filter Pro
 * File Name     : FilterValueKey.cs
 * Purpose       : Composite key that captures a parameter value's identity (by storage type)
 *                 for persisting and restoring Filter Pro selections across window sessions.
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
 * Input         : Created via ForString / ForInt / ForDouble / ForElementId factory methods
 * Output        : Stored in FilterProState.Values; matched against live values by FilterValueKeyMatcher
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - ElementIdValue stores IntegerValue (int) for compatibility with 2020-2023; in 2024+ ElementId.Value
 *   returns long — update to long if extending beyond int range in a future refactor.
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
    internal class FilterValueKey
    {
        public StorageType StorageType { get; private set; }
        public string StringValue { get; private set; }
        public int? IntValue { get; private set; }
        public double? DoubleValue { get; private set; }
        public int? ElementIdValue { get; private set; }

        private FilterValueKey() { }

        public static FilterValueKey ForString(string value)
        {
            return new FilterValueKey
            {
                StorageType = StorageType.String,
                StringValue = value
            };
        }

        public static FilterValueKey ForInt(int value)
        {
            return new FilterValueKey
            {
                StorageType = StorageType.Integer,
                IntValue = value
            };
        }

        public static FilterValueKey ForDouble(double value)
        {
            return new FilterValueKey
            {
                StorageType = StorageType.Double,
                DoubleValue = value
            };
        }

        public static FilterValueKey ForElementId(ElementId id)
        {
            return new FilterValueKey
            {
                StorageType = StorageType.ElementId,
                ElementIdValue = id?.IntegerValue
            };
        }
    }
}
