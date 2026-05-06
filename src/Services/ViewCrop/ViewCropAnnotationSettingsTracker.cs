// ==================================================
// Tool Name    : View Crop
// Purpose      : Stores last-used annotation crop settings for the current Revit session.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.1
// Created      : 2026-04-11
// Last Updated : 2026-05-06
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API, WPF
// Input        : Active Revit document, active or selected target views, and View Crop settings.
// Output       : Updated view crop or annotation crop settings for supported target views.
// Notes        : Skips unsupported, template, scope-box-controlled, and view-template-locked views.
// Changelog    : v1.0.1 - Standardized metadata after production cleanup.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================
using System;
using Autodesk.Revit.DB;
using AJTools.Models.ViewCrop;

namespace AJTools.Services.ViewCrop
{
    /// <summary>
    /// Stores annotation crop settings in-memory for the active document during the session.
    /// </summary>
    internal sealed class ViewCropAnnotationSettingsTracker
    {
        private static ViewCropAnnotationSettings _lastSettings;
        private static string _lastDocKey;

        internal ViewCropAnnotationSettingsTracker(Document doc)
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

        internal ViewCropAnnotationSettings LastSettings => (_lastSettings ?? new ViewCropAnnotationSettings()).Clone();

        internal void Save(ViewCropAnnotationSettings settings)
        {
            if (settings == null)
                return;

            _lastSettings = settings.Clone();
        }

        private static string BuildDocKey(Document doc)
        {
            if (!string.IsNullOrWhiteSpace(doc.PathName))
                return doc.PathName;

            return $"{doc.Title}|{doc.GetHashCode()}";
        }
    }
}
