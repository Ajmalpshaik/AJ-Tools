// Tool Name: Create Tags - Settings Tracker
// Description: Tracks last-used Create Tags settings per document session.
// Author: Ajmal P.S.
// Version: 1.0.0
// Revit Version: 2020

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using AJTools.Models.CreateTags;
using AJTools.Services.SmartTag;
using AJTools.Utils;

namespace AJTools.Services.CreateTags
{
    /// <summary>
    /// Keeps Create Tags settings scoped to the active document during this Revit session.
    /// Same category list as Smart MEP Tag (reused directly), no priority/offset concept -
    /// Create Tags places each tag exactly where the user clicks.
    /// </summary>
    internal sealed class CreateTagsSettingsTracker
    {
        private static CreateTagsSettingsState _lastState;
        private static string _lastDocKey;

        /// <summary>
        /// The out-of-the-box minimum length in mm. Internal rather than private so the settings
        /// command can hand it to the window's "Reset to defaults" instead of hard-coding a second copy.
        /// </summary>
        internal const double DefaultMinLengthMm = 1000.0;

        /// <summary>
        /// How far a tag sits from its element, mm on the printed sheet.
        /// </summary>
        /// <remarks>
        /// Added 2026-08-17 with the switch to automatic placement: the tool no longer asks for a
        /// click per tag, so this is what decides where the tag lands. Matches the offset Smart MEP
        /// Tags has always used, so a view tagged by either tool reads the same.
        /// </remarks>
        internal const double DefaultTagOffsetMm = 300.0;

        public CreateTagsSettingsTracker(Document doc)
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

        public CreateTagsSettingsState LastState => _lastState;

        public void Save(CreateTagsSettingsState state)
        {
            if (state == null)
                return;

            _lastState = CloneWithDefaults(state);
        }

        internal static double ResolveMinLengthInternal(CreateTagsSettingsState state)
        {
            if (state != null && state.MinLengthInternal > Constants.ZERO_LENGTH_TOLERANCE)
                return state.MinLengthInternal;

            return DefaultMinLengthMm * Constants.MM_TO_FEET;
        }

        internal static bool IsCategoryEnabled(CreateTagsSettingsState state, BuiltInCategory category)
        {
            if (state?.CategoryEnabled != null
                && state.CategoryEnabled.TryGetValue(category, out bool enabled))
            {
                return enabled;
            }

            return true;
        }

        internal static CreateTagsSettingsState EnsureDefaults(CreateTagsSettingsState state)
        {
            return CloneWithDefaults(state);
        }

        private static CreateTagsSettingsState CloneWithDefaults(CreateTagsSettingsState state)
        {
            var clone = new CreateTagsSettingsState
            {
                MinLengthInternal = ResolveMinLengthInternal(state),
                TagOffsetMm = state != null && state.TagOffsetMm > 0.0
                    ? state.TagOffsetMm
                    : DefaultTagOffsetMm,
                CategoryEnabled = new Dictionary<BuiltInCategory, bool>()
            };

            foreach (BuiltInCategory category in SmartTagSettingsTracker.SupportedCategories)
            {
                bool enabled = true;
                if (state?.CategoryEnabled != null
                    && state.CategoryEnabled.TryGetValue(category, out bool stateEnabled))
                {
                    enabled = stateEnabled;
                }

                clone.CategoryEnabled[category] = enabled;
            }

            return clone;
        }

        private static string BuildDocKey(Document doc)
        {
            if (!string.IsNullOrWhiteSpace(doc.PathName))
                return doc.PathName;

            return $"{doc.Title}|{doc.GetHashCode()}";
        }
    }
}
