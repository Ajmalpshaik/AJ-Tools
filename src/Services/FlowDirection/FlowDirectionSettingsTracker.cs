// Tool Name: Duct Flow - Settings Tracker
// Description: Keeps last-used duct flow settings per document session.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-21
// Revit Version: 2020
// Dependencies: System, Autodesk.Revit.DB, AJTools.Models

using System;
using Autodesk.Revit.DB;
using AJTools.Models;

namespace AJTools.Services.FlowDirection
{
    /// <summary>
    /// Tracks last-used duct flow settings per active document.
    /// </summary>
    internal sealed class FlowDirectionSettingsTracker
    {
        private static FlowDirectionSettingsState _lastState;
        private static string _lastDocKey;

        public FlowDirectionSettingsTracker(Document doc)
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

        public FlowDirectionSettingsState LastState => _lastState;

        public void Save(FlowDirectionSettingsState state)
        {
            if (state == null)
                return;

            _lastState = new FlowDirectionSettingsState
            {
                SymbolId = state.SymbolId,
                SpacingInternal = state.SpacingInternal
            };
        }

        private static string BuildDocKey(Document doc)
        {
            if (!string.IsNullOrWhiteSpace(doc.PathName))
                return doc.PathName;

            return $"{doc.Title}|{doc.GetHashCode()}";
        }
    }
}
