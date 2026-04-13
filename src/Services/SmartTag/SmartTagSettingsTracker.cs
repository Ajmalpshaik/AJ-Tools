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

        internal static SmartTagSettingsState EnsureDefaults(SmartTagSettingsState state)
        {
            if (state == null)
            {
                return CloneWithDefaults(new SmartTagSettingsState
                {
                    OffsetInternal = DefaultOffsetMm * Constants.MM_TO_FEET
                });
            }

            if (state.OffsetInternal > Constants.ZERO_LENGTH_TOLERANCE)
                return CloneWithDefaults(state);

            var normalized = new SmartTagSettingsState
            {
                OffsetInternal = DefaultOffsetMm * Constants.MM_TO_FEET,
                CategoryEnabled = state.CategoryEnabled,
                CategoryOffsetInternal = state.CategoryOffsetInternal,
                CategoryPriority = state.CategoryPriority
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
                CategoryPriority = new Dictionary<BuiltInCategory, TagPriority>()
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

                clone.CategoryEnabled[category] = enabled;
                clone.CategoryOffsetInternal[category] = offset;
                clone.CategoryPriority[category] = priority;
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
