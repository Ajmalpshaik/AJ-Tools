// ==================================================
// Tool Name    : View Crop
// Purpose      : Stores last-used View Crop settings for the current Revit session and persists on disk.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.2
// Created      : 2026-04-08
// Last Updated : 2026-05-24
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API, WPF
// Input        : Active Revit document, active or selected target views, and View Crop settings.
// Output       : Updated view crop or annotation crop settings for supported target views.
// Notes        : Skips unsupported, template, scope-box-controlled, and view-template-locked views.
// Changelog    : v1.0.2 - Premium disk-based settings persistence.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================
using System;
using Autodesk.Revit.DB;
using AJTools.Models.ViewCrop;
using AJTools.Utils;

namespace AJTools.Services.ViewCrop
{
    /// <summary>
    /// Stores user settings in-memory during the active document session and persists permanently on disk.
    /// </summary>
    internal sealed class ViewCropSettingsTracker
    {
        private static ViewCropSettings _lastSettings;
        private static string _lastDocKey;

        internal ViewCropSettingsTracker(Document doc)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));

            string key = BuildDocKey(doc);
            if (!string.Equals(_lastDocKey, key, StringComparison.OrdinalIgnoreCase))
            {
                _lastDocKey = key;
                _lastSettings = null; // Clear local session override to reload from disk config
            }
        }

        internal ViewCropSettings LastSettings
        {
            get
            {
                if (_lastSettings == null)
                {
                    _lastSettings = ViewCropConfigStore.Load();
                }
                return _lastSettings.Clone();
            }
        }

        internal void Save(ViewCropSettings settings)
        {
            if (settings == null)
                return;

            _lastSettings = settings.Clone();
            ViewCropConfigStore.Save(_lastSettings);
        }

        private static string BuildDocKey(Document doc)
        {
            if (!string.IsNullOrWhiteSpace(doc.PathName))
                return doc.PathName;

            return $"{doc.Title}|{doc.GetHashCode()}";
        }
    }
}
