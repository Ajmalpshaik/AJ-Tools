// Tool Name: Smart MEP Tag - Settings Tracker
// Description: Tracks last-used Smart MEP Tag settings per document session.
// Author: Ajmal P.S.
// Version: 1.0.0
// Revit Version: 2020

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Models.SmartTag;
using AJTools.Utils;

namespace AJTools.Services.SmartTag
{
    /// <summary>
    /// Keeps Smart MEP Tag settings scoped to the active document during this Revit session.
    /// </summary>
    internal sealed class SmartTagSettingsTracker
    {
        private static SmartTagSettingsState _lastState;
        private static string _lastDocKey;

        private const double DefaultOffsetMm = 300.0;

        /// <summary>Shortest run worth tagging. Was the hardcoded MinCurveLength.</summary>
        internal const double DefaultMinLengthMm = 1000.0;

        /// <summary>Minimum width. Was the hardcoded MinDuctWidth.</summary>
        internal const double DefaultMinWidthMm = 100.0;

        /// <summary>
        /// Minimum height. 0 = no height minimum, and that is deliberate: the old hardcoded filter
        /// only ever tested width, so defaulting this to 100 would silently stop shallow ducts being
        /// tagged the moment this feature shipped.
        /// </summary>
        internal const double DefaultMinHeightMm = 0.0;

        /// <summary>Whether the width/height minimums apply at all.</summary>
        internal const bool DefaultFilterBySize = true;

        internal static readonly BuiltInCategory[] SupportedCategories = new[]
        {
            BuiltInCategory.OST_MechanicalEquipment,
            BuiltInCategory.OST_DuctCurves,
            BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_PipeAccessory,
            BuiltInCategory.OST_DuctAccessory,
            BuiltInCategory.OST_CableTray
        };

        public SmartTagSettingsTracker(Document doc)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));

            string docKey = BuildDocKey(doc);
            if (!string.Equals(_lastDocKey, docKey, StringComparison.OrdinalIgnoreCase))
            {
                _lastDocKey = docKey;
                _lastState = null;
            }
        }

        public SmartTagSettingsState LastState => _lastState;

        public void Save(SmartTagSettingsState state)
        {
            if (state == null)
                return;

            _lastState = CloneWithDefaults(state);
        }

        public static double ResolveOffsetInternal(SmartTagSettingsState state)
        {
            if (state != null && state.OffsetInternal > Constants.ZERO_LENGTH_TOLERANCE)
                return state.OffsetInternal;

            return DefaultOffsetMm * Constants.MM_TO_FEET;
        }

        internal static bool IsCategoryEnabled(SmartTagSettingsState state, BuiltInCategory category)
        {
            if (state?.CategoryEnabled != null
                && state.CategoryEnabled.TryGetValue(category, out bool enabled))
            {
                return enabled;
            }

            return true;
        }

        internal static double ResolveOffsetInternal(SmartTagSettingsState state, BuiltInCategory category)
        {
            if (state?.CategoryOffsetInternal != null
                && state.CategoryOffsetInternal.TryGetValue(category, out double offset)
                && offset > Constants.ZERO_LENGTH_TOLERANCE)
            {
                return offset;
            }

            return ResolveOffsetInternal(state);
        }

        internal static TagPriority ResolvePriority(SmartTagSettingsState state, BuiltInCategory category)
        {
            if (state?.CategoryPriority != null
                && state.CategoryPriority.TryGetValue(category, out TagPriority priority))
            {
                return NormalizePriority(priority, category);
            }

            return GetDefaultPriority(category);
        }

        /// <summary>
        /// Builds a state carrying the size rules and per-category leader choices as last saved.
        /// </summary>
        /// <remarks>
        /// The leader choices are stored as the EXCEPTIONS - a comma-separated list of categories with
        /// the leader switched off - so a category the tool gains later correctly defaults to having
        /// a leader without needing the stored value migrated.
        /// </remarks>
        internal static SmartTagSettingsState LoadSizeSettingsFromFile()
        {
            var state = new SmartTagSettingsState
            {
                OffsetInternal = DefaultOffsetMm * Constants.MM_TO_FEET,
                MinLengthMm = DefaultMinLengthMm,
                FilterBySize = DefaultFilterBySize,
                MinWidthMm = DefaultMinWidthMm,
                MinHeightMm = DefaultMinHeightMm,
                CategoryUseLeader = new Dictionary<BuiltInCategory, bool>()
            };

            try
            {
                TagClashSettingsState stored = TagClashSettings.Load();

                state.MinLengthMm = stored.SmartTagMinLengthMm;
                state.FilterBySize = stored.SmartTagFilterBySize;
                state.MinWidthMm = stored.SmartTagMinWidthMm;
                state.MinHeightMm = stored.SmartTagMinHeightMm;

                var noLeader = new HashSet<string>(
                    ParseCategoryList(stored.SmartTagNoLeaderCategories), StringComparer.OrdinalIgnoreCase);

                foreach (BuiltInCategory category in SupportedCategories)
                    state.CategoryUseLeader[category] = !noLeader.Contains(category.ToString());
            }
            catch (Exception)
            {
                // An unreadable settings file must never stop the tool - the constants above stand.
            }

            return state;
        }

        /// <summary>
        /// Splits the stored comma-separated category list, ignoring blanks and stray whitespace.
        /// </summary>
        internal static IEnumerable<string> ParseCategoryList(string stored)
        {
            if (string.IsNullOrWhiteSpace(stored))
                yield break;

            foreach (string part in stored.Split(','))
            {
                string trimmed = part == null ? null : part.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    yield return trimmed;
            }
        }

        /// <summary>
        /// Renders the per-category leader choices back to the stored "categories with NO leader" form.
        /// </summary>
        internal static string FormatNoLeaderCategories(SmartTagSettingsState state)
        {
            if (state?.CategoryUseLeader == null)
                return string.Empty;

            var off = new List<string>();

            foreach (BuiltInCategory category in SupportedCategories)
            {
                if (state.CategoryUseLeader.TryGetValue(category, out bool useLeader) && !useLeader)
                    off.Add(category.ToString());
            }

            return string.Join(",", off);
        }

        /// <summary>
        /// Whether tags for this category get a leader. False places the tag beside the element.
        /// </summary>
        internal static bool ResolveUseLeader(SmartTagSettingsState state, BuiltInCategory category)
        {
            if (state?.CategoryUseLeader != null
                && state.CategoryUseLeader.TryGetValue(category, out bool useLeader))
            {
                return useLeader;
            }

            // Unknown category, or a state saved before this setting existed - keep the leader, which
            // is how the tool always behaved.
            return true;
        }

        internal static SmartTagSettingsState EnsureDefaults(SmartTagSettingsState state)
        {
            if (state == null)
            {
                // The one place the size rules are actually sourced. They come from the settings FILE,
                // not from the constants above, because this tracker is a static in-memory field that
                // empties every time Revit restarts - reading the constants here would quietly undo
                // whatever the user last set. The constants are the fallback the file itself uses when
                // a key is missing. CloneWithDefaults only carries values through, because by then a 0
                // cannot be told apart from a deliberate 0.
                return CloneWithDefaults(LoadSizeSettingsFromFile());
            }

            if (state.OffsetInternal > Constants.ZERO_LENGTH_TOLERANCE)
                return CloneWithDefaults(state);

            // Every field must be carried across, not just the ones this method is fixing up - a
            // missing line here silently resets whatever it forgot back to its default.
            var normalized = new SmartTagSettingsState
            {
                OffsetInternal = DefaultOffsetMm * Constants.MM_TO_FEET,
                CategoryEnabled = state.CategoryEnabled,
                CategoryOffsetInternal = state.CategoryOffsetInternal,
                CategoryPriority = state.CategoryPriority,
                CategoryUseLeader = state.CategoryUseLeader,
                MinLengthMm = state.MinLengthMm,
                FilterBySize = state.FilterBySize,
                MinWidthMm = state.MinWidthMm,
                MinHeightMm = state.MinHeightMm
            };

            return CloneWithDefaults(normalized);
        }

        internal static string GetCategoryLabel(BuiltInCategory category)
        {
            switch (category)
            {
                case BuiltInCategory.OST_MechanicalEquipment:
                    return "Mechanical Equipment";
                case BuiltInCategory.OST_DuctCurves:
                    return "Duct";
                case BuiltInCategory.OST_PipeCurves:
                    return "Pipe";
                case BuiltInCategory.OST_PipeAccessory:
                    return "Pipe Accessory";
                case BuiltInCategory.OST_DuctAccessory:
                    return "Duct Accessory";
                case BuiltInCategory.OST_CableTray:
                    return "Cable Tray";
                default:
                    return category.ToString();
            }
        }

        private static SmartTagSettingsState CloneWithDefaults(SmartTagSettingsState state)
        {
            double defaultOffset = ResolveOffsetInternal(state);

            var clone = new SmartTagSettingsState
            {
                OffsetInternal = defaultOffset,
                CategoryEnabled = new Dictionary<BuiltInCategory, bool>(),
                CategoryOffsetInternal = new Dictionary<BuiltInCategory, double>(),
                CategoryPriority = new Dictionary<BuiltInCategory, TagPriority>(),
                CategoryUseLeader = new Dictionary<BuiltInCategory, bool>(),

                // Carried through, never defaulted here. 0 is a LEGITIMATE user value for all three
                // ("no minimum on this axis"), so there is no sentinel that separates "set to zero"
                // from "never set" - inventing a default at this point would silently overwrite a
                // deliberate 0. Defaults are supplied once, by EnsureDefaults' null branch and by the
                // settings file loader, which both know the difference between absent and zero.
                MinLengthMm = state != null ? Math.Max(0.0, state.MinLengthMm) : DefaultMinLengthMm,
                FilterBySize = state == null ? DefaultFilterBySize : state.FilterBySize,
                MinWidthMm = state != null ? Math.Max(0.0, state.MinWidthMm) : DefaultMinWidthMm,
                MinHeightMm = state != null ? Math.Max(0.0, state.MinHeightMm) : DefaultMinHeightMm
            };

            foreach (BuiltInCategory category in SupportedCategories)
            {
                bool enabled = true;
                if (state?.CategoryEnabled != null
                    && state.CategoryEnabled.TryGetValue(category, out bool stateEnabled))
                {
                    enabled = stateEnabled;
                }

                double offset = defaultOffset;
                if (state?.CategoryOffsetInternal != null
                    && state.CategoryOffsetInternal.TryGetValue(category, out double stateOffset)
                    && stateOffset > Constants.ZERO_LENGTH_TOLERANCE)
                {
                    offset = stateOffset;
                }

                TagPriority priority = ResolvePriority(state, category);

                bool useLeader = true;
                if (state?.CategoryUseLeader != null
                    && state.CategoryUseLeader.TryGetValue(category, out bool stateUseLeader))
                {
                    useLeader = stateUseLeader;
                }

                clone.CategoryEnabled[category] = enabled;
                clone.CategoryOffsetInternal[category] = offset;
                clone.CategoryPriority[category] = priority;
                clone.CategoryUseLeader[category] = useLeader;
            }

            return clone;
        }

        private static TagPriority GetDefaultPriority(BuiltInCategory category)
        {
            switch (category)
            {
                case BuiltInCategory.OST_MechanicalEquipment:
                case BuiltInCategory.OST_DuctCurves:
                case BuiltInCategory.OST_PipeCurves:
                    return TagPriority.High;

                case BuiltInCategory.OST_PipeAccessory:
                case BuiltInCategory.OST_DuctAccessory:
                    return TagPriority.Medium;

                case BuiltInCategory.OST_CableTray:
                default:
                    return TagPriority.Low;
            }
        }

        private static TagPriority NormalizePriority(TagPriority priority, BuiltInCategory category)
        {
            switch (priority)
            {
                case TagPriority.High:
                case TagPriority.Medium:
                case TagPriority.Low:
                    return priority;
                default:
                    return GetDefaultPriority(category);
            }
        }

        private static string BuildDocKey(Document doc)
        {
            if (!string.IsNullOrWhiteSpace(doc.PathName))
                return doc.PathName;

            return $"{doc.Title}|{doc.GetHashCode()}";
        }
    }
}
