// ==================================================
// Tool Name    : Section Mark Visibility
// Purpose      : External command entry and orchestration for Section Mark Visibility.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.0
// Created      : 2026-05-24
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API, WPF
// ==================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models.SectionMarkVisibility;
using AJTools.UI.SectionMarkVisibility;
using AJTools.Services.SectionMarkVisibility;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Revit command entry point to manage section mark visibility.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdSectionMarkVisibility : IExternalCommand
    {
        private const string ToolTitle = "Section Mark Visibility";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // 1. Initial Revit Application and UI validations
                UIDocument uidoc = commandData.Application?.ActiveUIDocument;
                if (!ValidationHelper.ValidateUIDocument(uidoc, out message))
                {
                    DialogHelper.ShowError(ToolTitle, message);
                    return Result.Cancelled;
                }

                Document doc = uidoc.Document;
                if (!ValidationHelper.ValidateEditableDocument(doc, out message))
                {
                    DialogHelper.ShowError(ToolTitle, message);
                    return Result.Cancelled;
                }

                View activeView = doc.ActiveView;
                if (activeView == null)
                {
                    DialogHelper.ShowError(ToolTitle, "No active view found.");
                    return Result.Cancelled;
                }


                // 2. Load settings from persistent memory
                SectionMarkVisibilitySettings settings = SectionMarkVisibilityConfigStore.Load();

                // 3. Open main options UI window
                var optionsWindow = new SectionMarkVisibilityWindow(settings);
                new WindowInteropHelper(optionsWindow)
                {
                    Owner = commandData.Application.MainWindowHandle
                };

                bool? optionsResult = optionsWindow.ShowDialog();
                if (optionsResult != true)
                    return Result.Cancelled;

                settings = optionsWindow.SelectedSettings;

                // 4. Resolve which target views to process
                IList<View> targetViews = new List<View>();

                if (settings.ApplyToActiveViewOnly)
                {
                    // Active View mode: Validate active view type
                    if (!SectionMarkVisibilityService.IsSupportedPlanView(activeView))
                    {
                        string reason = "The active view is not a supported plan view.\n\nPlease open a Floor Plan, Ceiling Plan, Area Plan, or Structural Plan view and try again.";
                        DialogHelper.ShowError(ToolTitle, reason);
                        message = reason;
                        return Result.Cancelled;
                    }
                    targetViews.Add(activeView);
                }
                else
                {
                    // Selected Views mode: Collect all supported plan views and open view selection list
                    IList<SectionMarkVisibilityViewItem> selectableItems = CollectSelectableViews(doc, activeView.Id);
                    if (selectableItems.Count == 0)
                    {
                        DialogHelper.ShowError(ToolTitle, "No supported plan views found in this project.");
                        return Result.Cancelled;
                    }

                    var selectionWindow = new SectionMarkVisibilityViewsWindow(selectableItems);
                    new WindowInteropHelper(selectionWindow)
                    {
                        Owner = commandData.Application.MainWindowHandle
                    };

                    bool? selectionResult = selectionWindow.ShowDialog();
                    if (selectionResult != true || selectionWindow.SelectedViewIds == null || selectionWindow.SelectedViewIds.Count == 0)
                        return Result.Cancelled;

                    foreach (ElementId id in selectionWindow.SelectedViewIds)
                    {
                        var view = doc.GetElement(id) as View;
                        if (view != null)
                            targetViews.Add(view);
                    }
                }

                if (targetViews.Count == 0)
                    return Result.Cancelled;

                // 5. Invoke the core logic service to modify section visibility
                var service = new SectionMarkVisibilityService(doc, settings);
                string transactionLabel = $"AJ Tools - {ToolTitle}";
                SectionMarkVisibilityResult result = service.Process(targetViews, transactionLabel);

                // Force graphics refresh on the active viewport so the hidden section marks disappear/appear instantly!
                try
                {
                    uidoc.RefreshActiveView();
                }
                catch { }

                // Only display the summary dialog if there were errors
                if (result.Errors != null && result.Errors.Count > 0)
                {
                    ShowSummaryReport(result);
                }

                return result.ProcessedCount > 0 ? Result.Succeeded : Result.Failed;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                DialogHelper.ShowError(ToolTitle, $"An unexpected error occurred during execution:\n\n{ex.Message}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Gathers all supported plan views in the document and structures them for WPF checklist display.
        /// </summary>
        private static IList<SectionMarkVisibilityViewItem> CollectSelectableViews(Document doc, ElementId activeViewId)
        {
            var items = new List<SectionMarkVisibilityViewItem>();

            // Build sheet mapping: ViewId -> SheetInfo for fast placed-views detection
            var sheetMap = new Dictionary<int, SheetInfo>();
            var sheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .ToList();

            foreach (ViewSheet sheet in sheets)
            {
                if (sheet == null) continue;
                ICollection<ElementId> viewports = sheet.GetAllViewports();
                foreach (ElementId vpId in viewports)
                {
                    var vp = doc.GetElement(vpId) as Viewport;
                    if (vp == null || vp.ViewId == null) continue;

                    int viewIdVal = vp.ViewId.IntegerValue;
                    if (!sheetMap.ContainsKey(viewIdVal))
                    {
                        sheetMap[viewIdVal] = new SheetInfo { Number = sheet.SheetNumber, Name = sheet.Name };
                    }
                }
            }

            // Collect all non-template plan views in document
            var views = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v != null && !v.IsTemplate)
                .ToList();

            foreach (View v in views)
            {
                if (!SectionMarkVisibilityService.IsSupportedPlanView(v))
                    continue;

                string sheetNumber = string.Empty;
                string sheetName = string.Empty;
                string groupName = "Unplaced Views";

                if (sheetMap.TryGetValue(v.Id.IntegerValue, out SheetInfo sInfo))
                {
                    sheetNumber = sInfo.Number;
                    sheetName = sInfo.Name;
                    groupName = $"Sheet {sheetNumber} - {sheetName}";
                }

                items.Add(new SectionMarkVisibilityViewItem
                {
                    ViewId = v.Id,
                    ViewName = v.Name,
                    ViewTypeName = SectionMarkVisibilityService.GetFriendlyPlanTypeName(v),
                    SheetNumber = sheetNumber,
                    SheetName = sheetName,
                    GroupName = groupName,
                    CanSelect = true,
                    StatusText = "Supported",
                    IsSelected = activeViewId != null && activeViewId.IntegerValue == v.Id.IntegerValue
                });
            }

            return items
                .OrderBy(i => i.GroupName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.ViewName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Renders a Revit TaskDialog reporting the operation result.
        /// </summary>
        private static void ShowSummaryReport(SectionMarkVisibilityResult result)
        {
            var dialog = new TaskDialog(ToolTitle)
            {
                MainInstruction = "Section visibility processing completed.",
                CommonButtons = TaskDialogCommonButtons.Ok
            };

            string content = $"Successfully processed {result.ProcessedCount} view(s).";
            if (result.SkippedCount > 0)
            {
                content += $"\nSkipped {result.SkippedCount} unsupported view(s).";
            }

            dialog.MainContent = content;

            if (result.Errors != null && result.Errors.Count > 0)
            {
                dialog.ExpandedContent = "Errors encountered during execution:\n" + string.Join("\n", result.Errors);
            }

            dialog.Show();
        }

        private class SheetInfo
        {
            public string Number { get; set; }
            public string Name { get; set; }
        }
    }
}
