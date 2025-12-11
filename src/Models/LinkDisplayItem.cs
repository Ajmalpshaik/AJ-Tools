// Tool Name: Linked Tools - Link Display Item
// Description: Model helper for representing host or linked documents in search workflows.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB

using Autodesk.Revit.DB;

namespace AJTools.Models
{
    /// <summary>
    /// Lightweight wrapper for displaying either the host document or a linked model entry.
    /// </summary>
    internal sealed class LinkDisplayItem
    {
        internal LinkDisplayItem(string name, RevitLinkInstance instance, Document linkDocument, bool isHost)
        {
            DisplayName = string.IsNullOrWhiteSpace(name) ? "(Unnamed Link)" : name;
            Instance = instance;
            LinkDocument = linkDocument;
            IsHost = isHost;
        }

        internal string DisplayName { get; }

        internal bool IsHost { get; }

        internal RevitLinkInstance Instance { get; }

        internal Document LinkDocument { get; }

        public override string ToString() => DisplayName;
    }
}
