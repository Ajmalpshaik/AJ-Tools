#region Metadata
/*
 * Tool Name     : Smart MEP Tags - Step 3, Tag Types
 * File Name     : SmartTagTypeSelector.cs
 * Purpose       : Step 3: picks the tag type for each element - the type already used in the view, then the
 *                 project default, then a preferred name, then any loaded tag of the right category.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-10-15
 * Last Updated  : 2026-10-15
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / 4.8 (2021-2024) | .NET 8 (2025-2026) | .NET 10 (2027)
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models.SmartTag, AJTools.Utils, the other Smart MEP Tags files
 *
 * Input         : Candidates and the tag families loaded in the project.
 * Output        : Each candidate's tag type; warnings for the report.
 *
 * Notes         :
 * - Part of the Smart MEP Tags restructure (suite 1.58.0): the code that used to sit in
 *   SmartMepTagService.cs and SmartTagPlacementEngine.cs is split one job per file, in step
 *   folders. Every routine was moved as-is - tagging results are unchanged.
 * - The numbers that tune the algorithm live in SmartTagTuning.cs.
 *
 * Changelog     :
 * v1.0.0 (2026-10-15) - Split out of the old Smart MEP Tag service / placement engine.
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
using Autodesk.Revit.DB.Plumbing;
using AJTools.Models.SmartTag;
using AJTools.Services.CreateTags;
using AJTools.Services.LeaderLogic;
using AJTools.Services.TagClash;
using AJTools.Services.ViewCrop;
using AJTools.Utils;
using static AJTools.Services.SmartTag.SmartTagCandidatePositions;
using static AJTools.Services.SmartTag.SmartTagCreator;
using static AJTools.Services.SmartTag.SmartTagElementCollector;
using static AJTools.Services.SmartTag.SmartTagElementFilters;
using static AJTools.Services.SmartTag.SmartTagElementGeometry;
using static AJTools.Services.SmartTag.SmartTagPlacementEngine;
using static AJTools.Services.SmartTag.SmartTagPositionFinder;
using static AJTools.Services.SmartTag.SmartTagPositionScorer;
using static AJTools.Services.SmartTag.SmartTagSizeHints;
using static AJTools.Services.SmartTag.SmartTagViewCheck;
using static AJTools.Services.SmartTag.SmartTagViewObstacles;
using static AJTools.Services.SmartTag.SmartTagViewPlane;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// Picks the tag type for each element.
    /// </summary>
    internal static class SmartTagTypeSelector
    {
        /// <summary>
        /// Preferred tag family names by category and view context.
        /// Key = BuiltInCategory, Value = list of name fragments to search for (first match wins).
        /// Plan-specific and Section-specific names are tried first, then generic names.
        /// </summary>
        internal static readonly Dictionary<BuiltInCategory, string[]> PreferredTagNamesPlan =
            new Dictionary<BuiltInCategory, string[]>
            {
                { BuiltInCategory.OST_DuctCurves,            new[] { "Duct Tag - Plan", "Duct Tag" } },
                { BuiltInCategory.OST_PipeCurves,            new[] { "Pipe Tag - Plan", "Pipe Tag" } },
                { BuiltInCategory.OST_MechanicalEquipment,   new[] { "Equipment Tag", "Mechanical Equipment Tag" } },
                { BuiltInCategory.OST_PipeAccessory,         new[] { "Pipe Accessory Tag", "Accessory Tag" } },
                { BuiltInCategory.OST_DuctAccessory,         new[] { "Duct Accessory Tag", "Accessory Tag" } },
                { BuiltInCategory.OST_CableTray,             new[] { "Cable Tray Tag" } }
            };

        internal static readonly Dictionary<BuiltInCategory, string[]> PreferredTagNamesSection =
            new Dictionary<BuiltInCategory, string[]>
            {
                { BuiltInCategory.OST_DuctCurves,            new[] { "Duct Tag - Section", "Duct Tag" } },
                { BuiltInCategory.OST_PipeCurves,            new[] { "Pipe Tag - Section", "Pipe Tag" } },
                { BuiltInCategory.OST_MechanicalEquipment,   new[] { "Equipment Tag", "Mechanical Equipment Tag" } },
                { BuiltInCategory.OST_PipeAccessory,         new[] { "Pipe Accessory Tag", "Accessory Tag" } },
                { BuiltInCategory.OST_DuctAccessory,         new[] { "Duct Accessory Tag", "Accessory Tag" } },
                { BuiltInCategory.OST_CableTray,             new[] { "Cable Tray Tag" } }
            };

        /// <summary>
        /// Resolves the correct tag FamilySymbol for each candidate.
        /// Candidates with no available tag family are removed and logged.
        /// Returns a list of warning messages for fallback tag usage.
        /// </summary>
        public static List<string> SelectTagFamilies(
            Document doc,
            PreFlightResult preflight,
            List<TagCandidate> candidates,
            List<TagPlacementResult> results)
        {
            var warnings = new List<string>();
            bool isSectionOrElevation = preflight.ViewType == ViewType.Section
                                     || preflight.ViewType == ViewType.Elevation;

            // ── Build a cache of all loaded tag types per category ──
            // Done ONCE — never inside the candidate loop.
            Dictionary<BuiltInCategory, List<FamilySymbol>> tagCache = BuildTagFamilyCache(doc);
            Dictionary<BuiltInCategory, ElementId> configuredTagTypeByCategory =
                CollectConfiguredTagTypes(doc, preflight.ActiveView, tagCache);

            // ── Resolve tag for each candidate ──
            // Iterate backwards so we can remove candidates with no tag family.
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                TagCandidate candidate = candidates[i];
                BuiltInCategory bic = candidate.Category;

                List<FamilySymbol> availableTags;
                if (!tagCache.TryGetValue(bic, out availableTags) || availableTags.Count == 0)
                {
                    // No tag family loaded at all for this category.
                    results.Add(new TagPlacementResult
                    {
                        ElementId = candidate.ElementId,
                        LinkInstanceId = candidate.LinkInstanceId,
                        LinkName = candidate.LinkName,
                        Category = bic,
                        Success = false,
                        SkipReason = TagSkipReason.NoTagFamilyAvailable,
                        Note = string.Format("No tag family loaded for {0}", bic)
                    });
                    candidates.RemoveAt(i);
                    continue;
                }

                // First preference: use the tag type already configured by Revit
                // (existing tags in this view, then project default family type for the tag category).
                ElementId configuredTypeId;
                if (configuredTagTypeByCategory.TryGetValue(bic, out configuredTypeId))
                {
                    FamilySymbol configured = FindTagById(availableTags, configuredTypeId);
                    if (configured != null)
                    {
                        candidate.TagTypeId = configured.Id;
                        continue;
                    }
                }

                // Try preferred names first (view-type-specific, then generic).
                var preferredNames = isSectionOrElevation ? PreferredTagNamesSection : PreferredTagNamesPlan;
                string[] namesToTry;
                FamilySymbol matched = null;

                if (preferredNames.TryGetValue(bic, out namesToTry))
                {
                    foreach (string preferredName in namesToTry)
                    {
                        matched = FindTagByName(availableTags, preferredName);
                        if (matched != null)
                            break;
                    }
                }

                if (matched != null)
                {
                    candidate.TagTypeId = matched.Id;
                }
                else
                {
                    // Fallback: use the first available loaded tag for this category.
                    candidate.TagTypeId = availableTags[0].Id;
                    string fallbackName = availableTags[0].Family != null
                        ? availableTags[0].Family.Name
                        : availableTags[0].Name;
                    string msg = string.Format("Preferred tag not found for {0} — fallback used: \"{1}\"",
                        bic, fallbackName);
                    if (!warnings.Contains(msg))
                        warnings.Add(msg);
                }
            }

            return warnings;
        }

        /// <summary>
        /// Builds a lookup of all loaded tag FamilySymbol instances per taggable MEP category.
        /// Collected ONCE at the start — maps each BuiltInCategory to its available tag types.
        /// </summary>
        internal static Dictionary<BuiltInCategory, List<FamilySymbol>> BuildTagFamilyCache(Document doc)
        {
            var cache = new Dictionary<BuiltInCategory, List<FamilySymbol>>();

            // Initialize empty lists for each category we care about.
            foreach (BuiltInCategory bic in SmartTagSettingsTracker.SupportedCategories)
                cache[bic] = new List<FamilySymbol>();

            try
            {
                // Collect all IndependentTag types in the project.
                // Each tag FamilySymbol has a category it can tag — we match on that.
                var collector = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol));

                foreach (Element elem in collector)
                {
                    FamilySymbol fs = elem as FamilySymbol;
                    if (fs == null || fs.Family == null)
                        continue;

                    // In Revit, a tag family's category indicates what it tags.
                    // E.g. a "Duct Tag" family has category OST_DuctTags which tags OST_DuctCurves.
                    Category famCat = fs.Category ?? fs.Family.FamilyCategory;
                    if (famCat == null)
                        continue;

                    BuiltInCategory tagCat = MapTagCategoryToModelCategory(famCat.Id.IntValue());
                    if (tagCat == BuiltInCategory.INVALID)
                        continue;

                    List<FamilySymbol> list;
                    if (cache.TryGetValue(tagCat, out list))
                        list.Add(fs);
                }
            }
            catch (Exception)
            {
                // If collection fails, cache will have empty lists — elements will be skipped.
            }

            foreach (KeyValuePair<BuiltInCategory, List<FamilySymbol>> kvp in cache)
            {
                kvp.Value.Sort((a, b) => string.Compare(
                    GetTagDisplayName(a),
                    GetTagDisplayName(b),
                    StringComparison.OrdinalIgnoreCase));
            }

            return cache;
        }

        /// <summary>
        /// The tag type Revit is already set up to use for each category: the type most used by tags
        /// already in this view, otherwise the project's default tag type for that category.
        /// </summary>
        internal static Dictionary<BuiltInCategory, ElementId> CollectConfiguredTagTypes(
            Document doc,
            View view,
            Dictionary<BuiltInCategory, List<FamilySymbol>> tagCache)
        {
            var preferred = new Dictionary<BuiltInCategory, ElementId>();
            CollectViewTagTypePreferences(doc, view, tagCache, preferred);
            CollectProjectDefaultTagTypePreferences(doc, tagCache, preferred);
            return preferred;
        }

        internal static void CollectViewTagTypePreferences(
            Document doc,
            View view,
            Dictionary<BuiltInCategory, List<FamilySymbol>> tagCache,
            Dictionary<BuiltInCategory, ElementId> preferred)
        {
            if (doc == null || view == null || tagCache == null || preferred == null)
                return;

            try
            {
                var usage = new Dictionary<BuiltInCategory, Dictionary<int, int>>();
                var tags = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(IndependentTag))
                    .WhereElementIsNotElementType();

                foreach (Element elem in tags)
                {
                    IndependentTag tag = elem as IndependentTag;
                    if (tag == null)
                        continue;

                    ElementId taggedId;
                    try
                    {
                        taggedId = TagCompat.GetTaggedLocalElementId(tag);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (taggedId == null || taggedId == ElementId.InvalidElementId)
                        continue;

                    Element taggedElement = doc.GetElement(taggedId);
                    if (taggedElement == null || taggedElement.Category == null)
                        continue;

                    BuiltInCategory modelCategory;
                    try
                    {
                        modelCategory = (BuiltInCategory)taggedElement.Category.Id.IntValue();
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    List<FamilySymbol> available;
                    if (!tagCache.TryGetValue(modelCategory, out available) || available.Count == 0)
                        continue;

                    ElementId typeId = tag.GetTypeId();
                    if (typeId == null || typeId == ElementId.InvalidElementId)
                        continue;

                    if (FindTagById(available, typeId) == null)
                        continue;

                    Dictionary<int, int> categoryUsage;
                    if (!usage.TryGetValue(modelCategory, out categoryUsage))
                    {
                        categoryUsage = new Dictionary<int, int>();
                        usage[modelCategory] = categoryUsage;
                    }

                    int key = typeId.IntValue();
                    int count;
                    categoryUsage.TryGetValue(key, out count);
                    categoryUsage[key] = count + 1;
                }

                foreach (KeyValuePair<BuiltInCategory, Dictionary<int, int>> kvp in usage)
                {
                    int bestTypeInt = ElementId.InvalidElementId.IntValue();
                    int bestCount = -1;
                    foreach (KeyValuePair<int, int> countKvp in kvp.Value)
                    {
                        if (countKvp.Value > bestCount)
                        {
                            bestCount = countKvp.Value;
                            bestTypeInt = countKvp.Key;
                        }
                    }

                    if (bestTypeInt != ElementId.InvalidElementId.IntValue())
                        preferred[kvp.Key] = ElementIdHelper.FromInt(bestTypeInt);
                }
            }
            catch (Exception)
            {
                // Non-fatal: if preference extraction fails, continue with defaults/fallbacks.
            }
        }

        internal static void CollectProjectDefaultTagTypePreferences(
            Document doc,
            Dictionary<BuiltInCategory, List<FamilySymbol>> tagCache,
            Dictionary<BuiltInCategory, ElementId> preferred)
        {
            if (doc == null || tagCache == null || preferred == null)
                return;

            foreach (KeyValuePair<BuiltInCategory, List<FamilySymbol>> kvp in tagCache)
            {
                BuiltInCategory modelCategory = kvp.Key;
                if (preferred.ContainsKey(modelCategory))
                    continue;

                BuiltInCategory tagCategory = MapModelCategoryToTagCategory(modelCategory);
                if (tagCategory == BuiltInCategory.INVALID)
                    continue;

                try
                {
                    ElementId defaultTypeId = doc.GetDefaultFamilyTypeId(new ElementId(tagCategory));
                    if (defaultTypeId == null || defaultTypeId == ElementId.InvalidElementId)
                        continue;

                    if (FindTagById(kvp.Value, defaultTypeId) != null)
                        preferred[modelCategory] = defaultTypeId;
                }
                catch (Exception)
                {
                    // Some categories may not expose a default family type in this project/version.
                }
            }
        }

        internal static FamilySymbol FindTagById(List<FamilySymbol> tags, ElementId typeId)
        {
            if (tags == null || typeId == null || typeId == ElementId.InvalidElementId)
                return null;

            foreach (FamilySymbol fs in tags)
            {
                if (fs != null && fs.Id != null && fs.Id.IntValue() == typeId.IntValue())
                    return fs;
            }

            return null;
        }

        internal static BuiltInCategory MapModelCategoryToTagCategory(BuiltInCategory modelCategory)
        {
            switch (modelCategory)
            {
                case BuiltInCategory.OST_DuctCurves:
                    return BuiltInCategory.OST_DuctTags;
                case BuiltInCategory.OST_PipeCurves:
                    return BuiltInCategory.OST_PipeTags;
                case BuiltInCategory.OST_MechanicalEquipment:
                    return BuiltInCategory.OST_MechanicalEquipmentTags;
                case BuiltInCategory.OST_PipeAccessory:
                    return BuiltInCategory.OST_PipeAccessoryTags;
                case BuiltInCategory.OST_DuctAccessory:
                    return BuiltInCategory.OST_DuctAccessoryTags;
                case BuiltInCategory.OST_CableTray:
                    return BuiltInCategory.OST_CableTrayTags;
                default:
                    return BuiltInCategory.INVALID;
            }
        }

        /// <summary>
        /// Maps a tag annotation category to the model category it tags.
        /// E.g. OST_DuctTags → OST_DuctCurves.
        /// </summary>
        internal static BuiltInCategory MapTagCategoryToModelCategory(int tagCategoryId)
        {
            // Tag category integer IDs for Revit 2020.
            switch ((BuiltInCategory)tagCategoryId)
            {
                case BuiltInCategory.OST_DuctTags:
                    return BuiltInCategory.OST_DuctCurves;
                case BuiltInCategory.OST_PipeTags:
                    return BuiltInCategory.OST_PipeCurves;
                case BuiltInCategory.OST_MechanicalEquipmentTags:
                    return BuiltInCategory.OST_MechanicalEquipment;
                case BuiltInCategory.OST_PipeAccessoryTags:
                    return BuiltInCategory.OST_PipeAccessory;
                case BuiltInCategory.OST_DuctAccessoryTags:
                    return BuiltInCategory.OST_DuctAccessory;
                case BuiltInCategory.OST_CableTrayTags:
                    return BuiltInCategory.OST_CableTray;
                default:
                    return BuiltInCategory.INVALID;
            }
        }

        /// <summary>
        /// Searches available tag families for one matching the preferred name (case-insensitive contains).
        /// Checks both the Family name and the FamilySymbol (type) name.
        /// </summary>
        internal static FamilySymbol FindTagByName(List<FamilySymbol> tags, string preferredName)
        {
            string upper = preferredName.ToUpperInvariant();

            // First pass: exact family name match.
            foreach (FamilySymbol fs in tags)
            {
                try
                {
                    if (fs.Family != null && fs.Family.Name.ToUpperInvariant() == upper)
                        return fs;
                }
                catch (Exception) { }
            }

            // Second pass: family name contains the preferred name.
            foreach (FamilySymbol fs in tags)
            {
                try
                {
                    if (fs.Family != null && fs.Family.Name.ToUpperInvariant().Contains(upper))
                        return fs;
                }
                catch (Exception) { }
            }

            // Third pass: type name contains the preferred name.
            foreach (FamilySymbol fs in tags)
            {
                try
                {
                    if (fs.Name.ToUpperInvariant().Contains(upper))
                        return fs;
                }
                catch (Exception) { }
            }

            return null;
        }

        internal static string GetTagDisplayName(FamilySymbol symbol)
        {
            if (symbol == null)
                return string.Empty;

            string familyName = symbol.Family != null ? symbol.Family.Name : string.Empty;
            string typeName = symbol.Name ?? string.Empty;
            return familyName + " :: " + typeName;
        }
    }
}
