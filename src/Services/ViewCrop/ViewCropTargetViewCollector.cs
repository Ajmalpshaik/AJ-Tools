// ==================================================
// Tool Name    : View Crop
// Purpose      : Collects and groups selectable project views for batch crop processing.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.1
// Created      : 2026-04-08
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
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models.ViewCrop;

namespace AJTools.Services.ViewCrop
{
    /// <summary>
    /// Collects project views and maps each to its sheet context where available.
    /// </summary>
    internal static class ViewCropTargetViewCollector
    {
        private sealed class SheetInfo
        {
            internal string Number { get; set; }
            internal string Name { get; set; }
        }

        internal static IList<ViewCropTargetViewItem> Collect(Document doc, ElementId preselectViewId)
        {
            if (doc == null)
                return new List<ViewCropTargetViewItem>();

            Dictionary<int, SheetInfo> sheetMap = BuildSheetMap(doc);

            var views = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v != null && !v.IsTemplate && v.ViewType != ViewType.Internal)
                .Where(v => v.ViewType != ViewType.ProjectBrowser && v.ViewType != ViewType.SystemBrowser)
                .Where(v => v.ViewType != ViewType.Undefined)
                .Where(v => v.ViewType != ViewType.DrawingSheet)
                .OrderBy(v => ViewCropViewSupport.ToFriendlyTypeName(v.ViewType))
                .ThenBy(v => v.Name)
                .ToList();

            var items = new List<ViewCropTargetViewItem>(views.Count);
            foreach (View view in views)
            {
                bool isSupported = ViewCropViewSupport.TryValidateType(view, out string reason);

                string groupName = "Unplaced Views";
                string sheetNumber = string.Empty;
                string sheetName = string.Empty;

                if (sheetMap.TryGetValue(view.Id.IntegerValue, out SheetInfo sheet))
                {
                    sheetNumber = sheet.Number;
                    sheetName = sheet.Name;
                    groupName = $"Sheet {sheetNumber} - {sheetName}";
                }

                items.Add(new ViewCropTargetViewItem
                {
                    ViewId = view.Id,
                    ViewName = view.Name,
                    ViewTypeName = ViewCropViewSupport.ToFriendlyTypeName(view),
                    SheetNumber = sheetNumber,
                    SheetName = sheetName,
                    GroupName = groupName,
                    CanSelect = isSupported,
                    SupportMessage = isSupported ? "Supported" : reason,
                    IsSelected = isSupported && preselectViewId != null && preselectViewId.IntegerValue == view.Id.IntegerValue
                });
            }

            return items;
        }

        private static Dictionary<int, SheetInfo> BuildSheetMap(Document doc)
        {
            var map = new Dictionary<int, SheetInfo>();

            IList<ViewSheet> sheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .ToList();

            foreach (ViewSheet sheet in sheets)
            {
                if (sheet == null)
                    continue;

                string number = sheet.SheetNumber ?? string.Empty;
                string name = sheet.Name ?? string.Empty;
                var sheetInfo = new SheetInfo { Number = number, Name = name };

                ICollection<ElementId> viewportIds = sheet.GetAllViewports();
                foreach (ElementId viewportId in viewportIds)
                {
                    Viewport viewport = doc.GetElement(viewportId) as Viewport;
                    if (viewport == null || viewport.ViewId == null)
                        continue;

                    int viewInt = viewport.ViewId.IntegerValue;
                    if (!map.ContainsKey(viewInt))
                    {
                        map.Add(viewInt, sheetInfo);
                    }
                    else
                    {
                        SheetInfo existing = map[viewInt];
                        existing.Number = existing.Number + "+";
                    }
                }
            }

            return map;
        }
    }
}
