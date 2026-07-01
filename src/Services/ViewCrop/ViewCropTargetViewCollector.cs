#region Metadata
/*
 * Tool Name     : View Crop
 * File Name     : ViewCropTargetViewCollector.cs
 * Purpose       : Collects project views for the View Crop target-view picker and maps each to its sheet.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-04-08
 * Last Updated  : 2026-06-27
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Active Revit document, optional pre-selected view ElementId.
 * Output        : List of ViewCropTargetViewItem with sheet context and support status.
 *
 * Notes         :
 * - Templates, browsers, sheets, internal views, and drafting sheets are filtered out.
 * - Each view is marked CanSelect=true only when ViewCropViewSupport approves the type.
 * - ElementId numeric value access via ElementIdHelper (Revit 2024+ deprecated IntegerValue).
 *
 * Changelog     :
 * v1.2.0 (2026-06-28) - Collect only supported plan views (hide unsupported types from the picker).
 * v1.1.0 (2026-06-27) - Refactor/audit pass: ElementIdHelper, metadata, version coverage notes.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using AJTools.Models.ViewCrop;
using AJTools.Utils;

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
            int preselectKey = preselectViewId != null
                ? ElementIdHelper.GetIntegerValue(preselectViewId)
                : ElementIdHelper.GetIntegerValue(ElementId.InvalidElementId);

            // Only collect views the tool can actually crop (Floor / Ceiling / Engineering / Area plans).
            // Unsupported types (3D, section, elevation, sheets, legends, schedules, etc.) are not listed.
            var views = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v != null && !v.IsTemplate && ViewCropViewSupport.IsSupportedViewType(v.ViewType))
                .OrderBy(v => ViewCropViewSupport.ToFriendlyTypeName(v.ViewType))
                .ThenBy(v => v.Name)
                .ToList();

            var items = new List<ViewCropTargetViewItem>(views.Count);
            foreach (View view in views)
            {
                bool isSupported = ViewCropViewSupport.TryValidateType(view, out string reason);
                int viewKey = ElementIdHelper.GetIntegerValue(view.Id);

                string groupName = "Unplaced Views";
                string sheetNumber = string.Empty;
                string sheetName = string.Empty;

                if (sheetMap.TryGetValue(viewKey, out SheetInfo sheet))
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
                    IsSelected = isSupported && ElementIdHelper.IsValid(preselectViewId) && preselectKey == viewKey
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

                    int viewInt = ElementIdHelper.GetIntegerValue(viewport.ViewId);
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
