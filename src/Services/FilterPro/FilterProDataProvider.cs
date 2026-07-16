#region Metadata
/*
 * Tool Name     : Filter Pro
 * File Name     : FilterProDataProvider.cs
 * Purpose       : Read-only data provider — collects filterable categories, parameters, and
 *                 unique parameter values from the project for the Filter Pro UI.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2025-12-11
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, Autodesk.Revit.DB.Mechanical, System.Linq
 *
 * Input         : Active Project document; selected category ElementIds; selected FilterParameterItem
 * Output        : Lists of FilterCategoryItem, FilterParameterItem, FilterValueItem (read-only, no model changes)
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - 2020 = .NET Fx 4.7.2; 2021-2024 = .NET Fx (verify 4.8 if required); 2025-2026 = .NET 8; 2027+ = verify Autodesk SDK.
 * - Element scan limited to Constants.ELEMENT_SCAN_LIMIT (default 10 000) to prevent UI freeze on large models.
 * - ParameterFilterUtilities.GetFilterableParametersInCommon confirmed valid 2020-2026.
 * - LabelUtils.GetLabelFor(BuiltInParameter) used for built-in parameter display names — valid 2020-2026.
 * - Production-ready implementation. No model changes — no Transaction required.
 *
 * Changelog     :
 * v1.0.0 (2025-12-11) - Initial release.
 * v1.0.1 (2026-06-30) - Added mandatory metadata block; confirmed 2020-latest version coverage.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using AJTools.Models;
using AJTools.Utils;

namespace AJTools.Services.FilterPro
{
    /// <summary>
    /// Provides read-only data for the Filter Pro UI: categories, parameters, and available values.
    /// </summary>
    internal class FilterProDataProvider
    {
        private readonly Document _doc;

        public FilterProDataProvider(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public List<FilterCategoryItem> GetFilterableCategories()
        {
            var sorted = new List<FilterCategoryItem>();
            ICollection<ElementId> filterableCats = ParameterFilterUtilities.GetAllFilterableCategories();

            foreach (ElementId catId in filterableCats)
            {
                Category cat = Category.GetCategory(_doc, catId);
                if (cat != null)
                    sorted.Add(new FilterCategoryItem(catId, cat.Name));
            }

            return sorted.OrderBy(x => x.Name).ToList();
        }

        public List<FilterParameterItem> GetParametersForCategories(IList<ElementId> categoryIds)
        {
            if (categoryIds == null || categoryIds.Count == 0)
                return new List<FilterParameterItem>();

            var paramIds = new HashSet<ElementId>(
                ParameterFilterUtilities.GetFilterableParametersInCommon(_doc, categoryIds));

            // Ensure Family Name and Type Name are present for convenience
            paramIds.Add(new ElementId(BuiltInParameter.ALL_MODEL_FAMILY_NAME));
            paramIds.Add(new ElementId(BuiltInParameter.ALL_MODEL_TYPE_NAME));

            var result = new List<FilterParameterItem>
            {
                new FilterParameterItem(
                    SpecialParameterIds.FamilyAndType,
                    "Family and Type",
                    StorageType.String)
            };

            foreach (ElementId pid in paramIds)
            {
                Parameter sample = GetSampleParameter(pid, categoryIds);
                StorageType storage = sample?.StorageType ?? StorageType.None;

                if (pid.IntValue() == (int)BuiltInParameter.ALL_MODEL_FAMILY_NAME ||
                    pid.IntValue() == (int)BuiltInParameter.ALL_MODEL_TYPE_NAME)
                {
                    storage = StorageType.String;
                }

                string name = ResolveParameterName(pid, sample);
                if (storage != StorageType.None)
                {
                    result.Add(new FilterParameterItem(pid, name, storage));
                }
            }

            return result.OrderBy(p => p.Name).ToList();
        }

        public List<FilterValueItem> GetValues(
            FilterParameterItem param,
            List<ElementId> categoryIds,
            out bool hitScanLimit)
        {
            hitScanLimit = false;

            if (param == null || categoryIds == null || categoryIds.Count == 0)
                return new List<FilterValueItem>();

            if (param.Id.IntValue() == SpecialParameterIds.FamilyAndType.IntValue())
                return LoadFamilyAndTypeValues(categoryIds);

            return LoadRegularParameterValues(param, categoryIds, out hitScanLimit);
        }

        private List<FilterValueItem> LoadFamilyAndTypeValues(List<ElementId> catIds)
        {
            var famTypeCollector = new FilteredElementCollector(_doc)
                .WherePasses(new ElementMulticategoryFilter(catIds));

            var famTypeSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var familyAndTypeValues = new List<FilterValueItem>();

            foreach (Element elem in famTypeCollector)
            {
                Parameter pFam = elem.get_Parameter(BuiltInParameter.ALL_MODEL_FAMILY_NAME);
                string familyName = pFam != null ? pFam.AsString() : string.Empty;

                Parameter pType = elem.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_NAME);
                string typeName = pType != null ? pType.AsString() : string.Empty;

                if (string.IsNullOrWhiteSpace(familyName) && elem.Category != null)
                {
                    familyName = elem.Category.Name;
                }

                if (string.IsNullOrWhiteSpace(familyName) || string.IsNullOrWhiteSpace(typeName))
                    continue;

                string display = $"{familyName} - {typeName}";
                if (famTypeSeen.Add(display))
                {
                    familyAndTypeValues.Add(
                        new FilterValueItem(
                            display,
                            new Tuple<string, string>(familyName, typeName),
                            StorageType.String));
                }
            }

            return familyAndTypeValues.OrderBy(v => v.Display).ToList();
        }

        private List<FilterValueItem> LoadRegularParameterValues(
            FilterParameterItem param,
            List<ElementId> catIds,
            out bool hitScanLimit)
        {
            hitScanLimit = false;

            var filter = new ElementMulticategoryFilter(catIds);
            var collector = new FilteredElementCollector(_doc).WherePasses(filter);

            int paramIntId = param.Id.IntValue();
            bool isBuiltIn = ElementIdHelper.IsDefinedBuiltInParameter(paramIntId);
            BuiltInParameter builtInParam = isBuiltIn ? (BuiltInParameter)paramIntId : 0;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var collectedValuesResult = new List<FilterValueItem>();
            int scanned = 0;

            foreach (Element elem in collector)
            {
                scanned++;
                if (scanned > Constants.ELEMENT_SCAN_LIMIT)
                {
                    hitScanLimit = true;
                    break;
                }

                Parameter p = null;

                if (isBuiltIn)
                {
                    p = elem.get_Parameter(builtInParam);
                }

                if (p == null)
                {
                    p = elem.LookupParameter(param.Name);
                }

                if (p == null)
                {
                    foreach (Parameter elemParam in elem.Parameters)
                    {
                        if (elemParam.Id.IntValue() == paramIntId)
                        {
                            p = elemParam;
                            break;
                        }
                    }
                }

                if (p == null || p.StorageType == StorageType.None || !p.HasValue)
                    continue;

                FilterValueItem item = ExtractValueItem(p, elem, param.StorageType, param.Name);
                if (item?.RawValue == null)
                    continue;

                string key = item.StorageType == StorageType.String
                    ? item.RawValue as string
                    : item.Display;

                if (!string.IsNullOrEmpty(key) && seen.Add(key))
                    collectedValuesResult.Add(item);
            }

            return collectedValuesResult.OrderBy(v => v.Display).ToList();
        }

        private FilterValueItem ExtractValueItem(
            Parameter param,
            Element owner,
            StorageType targetStorage,
            string paramName)
        {
            if (param == null || !param.HasValue)
                return null;

            switch (targetStorage)
            {
                case StorageType.String:
                    string text = param.AsString() ?? param.AsValueString();
                    return string.IsNullOrEmpty(text)
                        ? null
                        : new FilterValueItem(text, text, StorageType.String);

                case StorageType.Integer:
                    int i = param.AsInteger();
                    return new FilterValueItem(i.ToString(), i, StorageType.Integer);

                case StorageType.Double:
                    double d = param.AsDouble();
                    string display = param.AsValueString() ?? d.ToString("0.###");
                    return new FilterValueItem(display, d, StorageType.Double);

                case StorageType.ElementId:
                    ElementId eid = param.AsElementId();
                    if (eid == null || eid == ElementId.InvalidElementId)
                        return null;

                    string name = ResolveElementName(_doc.GetElement(eid), eid, paramName);
                    return new FilterValueItem(name, eid, StorageType.ElementId, eid);

                default:
                    return null;
            }
        }

        private string ResolveElementName(Element element, ElementId id, string paramName)
        {
            if (element == null)
                return "#" + id.IntValue();

            // Prefer family + type when available for clarity
            string familyName = null;
            string typeName = null;

            if (element is FamilyInstance inst && inst.Symbol != null)
            {
                familyName = inst.Symbol.FamilyName;
                typeName = inst.Symbol.Name;
            }
            else if (element is FamilySymbol fs)
            {
                familyName = fs.FamilyName;
                typeName = fs.Name;
            }
            else if (element is ElementType et)
            {
                typeName = et.Name;

                Parameter famParam =
                    et.get_Parameter(BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM) ??
                    et.get_Parameter(BuiltInParameter.ALL_MODEL_FAMILY_NAME);

                if (famParam != null && famParam.HasValue)
                    familyName = famParam.AsString();
            }

            // If the parameter name suggests a system/type name, prefer the raw element name
            if (!string.IsNullOrWhiteSpace(paramName) &&
                paramName.IndexOf("system", StringComparison.OrdinalIgnoreCase) >= 0 &&
                !string.IsNullOrWhiteSpace(element.Name))
            {
                return element.Name;
            }

            if (!string.IsNullOrWhiteSpace(familyName) &&
                !string.IsNullOrWhiteSpace(typeName))
            {
                return $"{familyName} : {typeName}";
            }

            if (!string.IsNullOrWhiteSpace(typeName))
                return typeName;

            string label = element.Name;
            if (!string.IsNullOrWhiteSpace(label))
                return label;

            if (element is MechanicalSystemType)
            {
                Parameter nameParam =
                    element.get_Parameter(BuiltInParameter.SYMBOL_NAME_PARAM);

                if (nameParam != null)
                {
                    string name = nameParam.AsString();
                    if (!string.IsNullOrWhiteSpace(name))
                        return name;
                }
            }

            if (!string.IsNullOrWhiteSpace(paramName) &&
                paramName.IndexOf("System Type", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "System " + id.IntValue();
            }

            return "#" + id.IntValue();
        }

        private Parameter GetSampleParameter(ElementId paramId, IList<ElementId> categoryIds)
        {
            int paramIntId = paramId.IntValue();
            bool isBuiltIn = ElementIdHelper.IsDefinedBuiltInParameter(paramIntId);
            BuiltInParameter builtIn = isBuiltIn ? (BuiltInParameter)paramIntId : 0;

            foreach (ElementId catId in categoryIds)
            {
                var catFilter = new ElementCategoryFilter(catId);

                Element instance = new FilteredElementCollector(_doc)
                    .WherePasses(catFilter)
                    .WhereElementIsNotElementType()
                    .FirstOrDefault();

                if (instance != null)
                {
                    Parameter p = null;

                    // Try built-in parameter
                    if (isBuiltIn)
                    {
                        p = instance.get_Parameter(builtIn);
                    }

                    // Try as shared/project parameter
                    if (p == null)
                    {
                        foreach (Parameter elemParam in instance.Parameters)
                        {
                            if (elemParam.Id.IntValue() == paramIntId)
                            {
                                p = elemParam;
                                break;
                            }
                        }
                    }

                    if (p != null)
                        return p;
                }

                Element typeElem = new FilteredElementCollector(_doc)
                    .WherePasses(catFilter)
                    .WhereElementIsElementType()
                    .FirstOrDefault();

                if (typeElem != null)
                {
                    Parameter p = null;

                    if (isBuiltIn)
                    {
                        p = typeElem.get_Parameter(builtIn);
                    }

                    if (p == null)
                    {
                        foreach (Parameter elemParam in typeElem.Parameters)
                        {
                            if (elemParam.Id.IntValue() == paramIntId)
                            {
                                p = elemParam;
                                break;
                            }
                        }
                    }

                    if (p != null)
                        return p;
                }
            }

            return null;
        }

        private string ResolveParameterName(ElementId paramId, Parameter sample)
        {
            if (sample != null)
                return sample.Definition.Name;

            if (_doc.GetElement(paramId) is ParameterElement paramElem)
                return paramElem.Name;

            if (ElementIdHelper.IsDefinedBuiltInParameter(paramId.IntValue()))
            {
                try
                {
                    return LabelUtils.GetLabelFor(
                        (BuiltInParameter)paramId.IntValue());
                }
                catch
                {
                    // ignore label lookups that fail
                }
            }

            return "Param " + paramId.IntValue();
        }
    }
}
