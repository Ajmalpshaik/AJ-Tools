#region Metadata
/*
 * Tool Name     : AJ Tools Shared Helper
 * File Name     : RevitCompat.cs
 * Purpose       : Version-safe access to the Revit "ForgeTypeId transition" API across Revit 2020 -> 2027.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-07
 * Last Updated  : 2026-07-07
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / .NET Fx 4.8 (2021-2024) | .NET 8 (2025-2026) | .NET 10 (2027 - verify SDK)
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Definition / BindingMap / Document, plus spec-, group-, and unit-type tokens.
 * Output        : Version-correct parameter spec / group / unit values, labels, and binding operations.
 *
 * Notes         :
 * - Revit 2020-2021 use the legacy enums: ParameterType, BuiltInParameterGroup, DisplayUnitType, UnitType.
 * - Revit 2022+ removed those enums in favour of ForgeTypeId: SpecTypeId, GroupTypeId, UnitTypeId.
 *   (BuiltInParameterGroup lingered until it was removed in 2025; 2022 is the safe single switch point
 *    because GroupTypeId/GetGroupTypeId already exist from 2021.)
 * - Every version-specific branch lives ONLY in this file so call sites never branch on Revit version.
 * - Companion file: ElementIdHelper.cs handles the separate 2024 ElementId 32->64-bit change.
 * - Companion type aliases (AjSpec / AjGroup / AjUnit) are declared per-file where members are TYPED
 *   as these tokens, so 2020-2021 store the enum and 2022+ store a ForgeTypeId with no other churn.
 *
 * Changelog     :
 * v1.0.0 (2026-07-07) - Initial release: units, parameter spec type, parameter group, shared-parameter
 *                       creation, and parameter binding made version-safe for Revit 2020 -> 2027.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using Autodesk.Revit.DB;

namespace AJTools.Utils
{
    /// <summary>
    /// Centralises the Revit "ForgeTypeId transition" so the rest of the add-in never branches on
    /// Revit version. Legacy enums (Revit 2020-2021) vs ForgeTypeId (Revit 2022+) are hidden here.
    /// </summary>
    internal static class RevitCompat
    {
        // ============================================================
        //  Parameter spec / data type  (Text / Length / Number / YesNo)
        //  Revit 2020-2021: ParameterType enum. Revit 2022+: SpecTypeId (ForgeTypeId).
        // ============================================================
#if REVIT2022_OR_GREATER
        internal static ForgeTypeId SpecText => SpecTypeId.String.Text;
        internal static ForgeTypeId SpecLength => SpecTypeId.Length;
        internal static ForgeTypeId SpecNumber => SpecTypeId.Number;
        internal static ForgeTypeId SpecYesNo => SpecTypeId.Boolean.YesNo;
        internal static ForgeTypeId GroupData => GroupTypeId.Data;
#else
        internal static ParameterType SpecText => ParameterType.Text;
        internal static ParameterType SpecLength => ParameterType.Length;
        internal static ParameterType SpecNumber => ParameterType.Number;
        internal static ParameterType SpecYesNo => ParameterType.YesNo;
        internal static BuiltInParameterGroup GroupData => BuiltInParameterGroup.PG_DATA;
#endif

        /// <summary>Reads a definition's spec/data type in the form the current Revit version understands.</summary>
#if REVIT2022_OR_GREATER
        internal static ForgeTypeId GetDataType(Definition definition) => definition.GetDataType();
#else
        internal static ParameterType GetDataType(Definition definition) => definition.ParameterType;
#endif

        /// <summary>Reads a definition's parameter group in the form the current Revit version understands.</summary>
#if REVIT2022_OR_GREATER
        internal static ForgeTypeId GetGroup(Definition definition) => definition.GetGroupTypeId();
#else
        internal static BuiltInParameterGroup GetGroup(Definition definition) => definition.ParameterGroup;
#endif

        /// <summary>True when the definition is a Yes/No (boolean) parameter, both API styles.</summary>
        internal static bool IsYesNo(Definition definition)
        {
            if (definition == null)
                return false;
#if REVIT2022_OR_GREATER
            return definition.GetDataType().TypeId == SpecTypeId.Boolean.YesNo.TypeId;
#else
            return definition.ParameterType == ParameterType.YesNo;
#endif
        }

        // ============================================================
        //  Human-readable labels for spec / group / unit
        //  Revit 2020-2021: LabelUtils.GetLabelFor(enum).
        //  Revit 2022+    : GetLabelForSpec / GetLabelForGroup / GetLabelForUnit(ForgeTypeId).
        // ============================================================
#if REVIT2022_OR_GREATER
        internal static string SpecLabel(ForgeTypeId spec) => LabelUtils.GetLabelForSpec(spec);
        internal static string GroupLabel(ForgeTypeId group) => LabelUtils.GetLabelForGroup(group);
        internal static string UnitLabel(ForgeTypeId unit) => LabelUtils.GetLabelForUnit(unit);
#else
        internal static string SpecLabel(ParameterType spec) => LabelUtils.GetLabelFor(spec);
        internal static string GroupLabel(BuiltInParameterGroup group) => LabelUtils.GetLabelFor(group);
        internal static string UnitLabel(DisplayUnitType unit) => LabelUtils.GetLabelFor(unit);
#endif

        // ============================================================
        //  Shared parameter creation
        //  Revit 2020-2021: ExternalDefinitionCreationOptions(name, ParameterType).
        //  Revit 2022+    : ExternalDefinitionCreationOptions(name, ForgeTypeId).
        // ============================================================
#if REVIT2022_OR_GREATER
        internal static ExternalDefinitionCreationOptions CreateDefinitionOptions(string name, ForgeTypeId spec)
            => new ExternalDefinitionCreationOptions(name, spec);
#else
        internal static ExternalDefinitionCreationOptions CreateDefinitionOptions(string name, ParameterType spec)
            => new ExternalDefinitionCreationOptions(name, spec);
#endif

        // ============================================================
        //  Parameter binding insert / re-insert
        //  Revit 2020-2021: BindingMap.Insert(def, binding, BuiltInParameterGroup).
        //  Revit 2022+    : BindingMap.Insert(def, binding, ForgeTypeId group).
        // ============================================================
#if REVIT2022_OR_GREATER
        internal static bool InsertBinding(BindingMap map, Definition definition, Binding binding, ForgeTypeId group)
            => map.Insert(definition, binding, group);

        internal static bool ReInsertBinding(BindingMap map, Definition definition, Binding binding, ForgeTypeId group)
            => map.ReInsert(definition, binding, group);
#else
        internal static bool InsertBinding(BindingMap map, Definition definition, Binding binding, BuiltInParameterGroup group)
            => map.Insert(definition, binding, group);

        internal static bool ReInsertBinding(BindingMap map, Definition definition, Binding binding, BuiltInParameterGroup group)
            => map.ReInsert(definition, binding, group);
#endif

        // ============================================================
        //  Family parameter replace (share -> non-shared)
        //  Revit 2020-2021: FamilyManager.ReplaceParameter(param, name, BuiltInParameterGroup, isInstance).
        //  Revit 2022+    : FamilyManager.ReplaceParameter(param, name, ForgeTypeId group, isInstance).
        // ============================================================
#if REVIT2022_OR_GREATER
        internal static FamilyParameter ReplaceParameter(FamilyManager familyManager, FamilyParameter source, string name, ForgeTypeId group, bool isInstance)
            => familyManager.ReplaceParameter(source, name, group, isInstance);
#else
        internal static FamilyParameter ReplaceParameter(FamilyManager familyManager, FamilyParameter source, string name, BuiltInParameterGroup group, bool isInstance)
            => familyManager.ReplaceParameter(source, name, group, isInstance);
#endif

        // ============================================================
        //  Units  (millimetres / metres, conversion, project length unit)
        //  Revit 2020: DisplayUnitType. Revit 2021+: UnitTypeId (ForgeTypeId).
        //  We switch at 2022 to keep one consistent switch point across this helper; 2021 still
        //  compiles against the legacy enums (deprecated but present in 2021).
        // ============================================================
#if REVIT2022_OR_GREATER
        internal static ForgeTypeId UnitMillimeters => UnitTypeId.Millimeters;
        internal static ForgeTypeId UnitMeters => UnitTypeId.Meters;

        /// <summary>The document's active length display unit, in the current API's token type.</summary>
        internal static ForgeTypeId LengthDisplayUnit(Document doc)
            => doc.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId();
#else
        internal static DisplayUnitType UnitMillimeters => DisplayUnitType.DUT_MILLIMETERS;
        internal static DisplayUnitType UnitMeters => DisplayUnitType.DUT_METERS;

        /// <summary>The document's active length display unit, in the current API's token type.</summary>
        internal static DisplayUnitType LengthDisplayUnit(Document doc)
            => doc.GetUnits().GetFormatOptions(UnitType.UT_Length).DisplayUnits;
#endif

        /// <summary>Converts a millimetre value to Revit internal (feet) units, version-safe.</summary>
        internal static double MmToInternal(double millimeters)
            => UnitUtils.ConvertToInternalUnits(millimeters, UnitMillimeters);

        /// <summary>Converts a Revit internal (feet) value to millimetres, version-safe.</summary>
        internal static double InternalToMm(double internalUnits)
            => UnitUtils.ConvertFromInternalUnits(internalUnits, UnitMillimeters);

        /// <summary>
        /// Formats an internal HVAC airflow value using the document's units, version-safe.
        /// Revit 2020-2021: UnitFormatUtils.Format(units, UnitType, value, maxAccuracy, forEditing).
        /// Revit 2022+    : UnitFormatUtils.Format(units, ForgeTypeId spec, value, forEditing).
        /// </summary>
        internal static string FormatHvacAirflow(Document doc, double internalValue)
        {
#if REVIT2022_OR_GREATER
            return UnitFormatUtils.Format(doc.GetUnits(), SpecTypeId.AirFlow, internalValue, false);
#else
            return UnitFormatUtils.Format(doc.GetUnits(), UnitType.UT_HVAC_Airflow, internalValue, false, false);
#endif
        }
    }
}
