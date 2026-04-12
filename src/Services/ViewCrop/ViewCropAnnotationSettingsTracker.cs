// Tool Name: View Crop Annotation Settings Tracker
// Description: Keeps last-used annotation crop settings per document in the current Revit session.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-11
// Revit Version: 2020

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
