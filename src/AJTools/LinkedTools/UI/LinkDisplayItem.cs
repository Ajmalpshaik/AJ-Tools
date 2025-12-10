// Tool Name: Linked Tools UI - Link Display Item
// Description: UI-facing model for presenting host or linked document choices in dialogs.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB
using Autodesk.Revit.DB;

namespace AJTools.LinkedTools.UI
{
    internal class LinkDisplayItem
    {
        public string DisplayName { get; }
        public RevitLinkInstance Instance { get; }
        public Document LinkDocument { get; }
        public bool IsHost { get; }

        public LinkDisplayItem(string name, RevitLinkInstance instance, Document linkDocument, bool isHost)
        {
            DisplayName = name ?? "Model";
            Instance = instance;
            LinkDocument = linkDocument;
            IsHost = isHost;
        }
    }
}
