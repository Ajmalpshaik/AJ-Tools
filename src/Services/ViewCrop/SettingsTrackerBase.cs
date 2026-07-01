#region Metadata
/*
 * Tool Name     : View Crop
 * File Name     : SettingsTrackerBase.cs
 * Purpose       : Generic per-document settings tracker shared by View Crop tools.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-06-27
 * Last Updated  : 2026-06-27
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Active Revit Document, settings object T.
 * Output        : Cached last settings for the current document, optional disk persistence via overrides.
 *
 * Notes         :
 * - Holds last-used settings in static fields scoped per closed generic type
 *   (each settings type T gets its own static cache).
 * - Resets the cache when the active document changes.
 * - Disk persistence is opt-in: derived class overrides LoadFromStore / SaveToStore.
 *
 * Changelog     :
 * v1.1.0 (2026-06-27) - Initial release as shared base for View Crop settings trackers.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System;
using Autodesk.Revit.DB;

namespace AJTools.Services.ViewCrop
{
    /// <summary>
    /// Generic per-document settings tracker. Each derived type sharing the same T
    /// closure shares the same static cache - which matches the per-tool singleton
    /// pattern used by the View Crop trackers.
    /// </summary>
    internal abstract class SettingsTrackerBase<T> where T : class
    {
        private static T _lastSettings;
        private static string _lastDocKey;

        protected SettingsTrackerBase(Document doc)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));

            string key = BuildDocKey(doc);
            if (!string.Equals(_lastDocKey, key, StringComparison.OrdinalIgnoreCase))
            {
                _lastDocKey = key;
                _lastSettings = null;
            }
        }

        /// <summary>
        /// Returns a clone of the last-used settings. Falls back to <see cref="LoadFromStore"/>
        /// (which defaults to <see cref="CreateDefault"/>) when no cache exists.
        /// </summary>
        internal T LastSettings
        {
            get
            {
                if (_lastSettings == null)
                    _lastSettings = LoadFromStore();

                return CloneSettings(_lastSettings);
            }
        }

        /// <summary>
        /// Updates the cache (and optional disk store) with a clone of the provided settings.
        /// Null inputs are ignored.
        /// </summary>
        internal void Save(T settings)
        {
            if (settings == null)
                return;

            _lastSettings = CloneSettings(settings);
            SaveToStore(_lastSettings);
        }

        protected abstract T CreateDefault();

        protected abstract T CloneSettings(T settings);

        protected virtual T LoadFromStore() => CreateDefault();

        protected virtual void SaveToStore(T settings)
        {
            // No-op by default. Override to persist to disk.
        }

        private static string BuildDocKey(Document doc)
        {
            if (!string.IsNullOrWhiteSpace(doc.PathName))
                return doc.PathName;

            return $"{doc.Title}|{doc.GetHashCode()}";
        }
    }
}
