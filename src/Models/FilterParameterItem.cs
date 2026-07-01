#region Metadata
/*
 * Tool Name     : Filter Pro
 * File Name     : FilterParameterItem.cs
 * Purpose       : Immutable wrapper pairing an ElementId with a parameter name and StorageType
 *                 for binding to the Filter Pro parameter list.
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
 * Input         : ElementId, name, StorageType from FilterProDataProvider.GetParametersForCategories()
 * Output        : Bound to the parameters ListBox; StorageType drives rule-type availability
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
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
    internal class FilterParameterItem
    {
        public FilterParameterItem(ElementId id, string name, StorageType storageType)
        {
            Id = id;
            Name = name;
            StorageType = storageType;
        }

        public ElementId Id { get; }
        public string Name { get; }
        public StorageType StorageType { get; }

        public override string ToString() => Name;
    }
}
