#region Metadata
/*
 * Tool Name     : Match Category Graphics
 * File Name     : CmdMatchCategoryGraphics.cs
 * Purpose       : Copies category-level graphics overrides from one picked source category to the categories of one or more picked target elements in the active view.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.5.0
 *
 * Created Date  : 2026-03-30
 * Last Updated  : 2026-06-30
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Active View - one source model element, then one or more target model elements (pick, ESC to finish).
 * Output        : Target category graphics overrides matched to the source category in the active view (single undo step).
 *
 * Notes         :
 * - Targets Revit 2020 through latest; version-safe ElementId access via ElementIdHelper.
 * - All target category matches are grouped in one TransactionGroup so a single Ctrl+Z reverses the whole operation.
 * - Only overridable model categories are matched; ESC during a pick cancels silently.
 *
 * Changelog     :
 * v1.5.0 (2026-06-30) - Grouped multi-target matching into one TransactionGroup (single undo); version-safe ElementId access; full metadata block.
 * v1.4.4 (2026-05-09) - Reviewed Match Category Graphics flow, shared validation, and metadata for release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Services.GraphicsTools;
using AJTools.Utils;

namespace AJTools.Commands.GraphicsTools
{
    /// <summary>
    /// Matches source category overrides to target categories based on picked target elements.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdMatchCategoryGraphics : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            const string dialogTitle = "Match Category Graphics";

            try
            {
                Result contextResult = GraphicsCommandService.TryCreateContext(
                    commandData,
                    dialogTitle,
                    ref message,
                    out GraphicsCommandContext context);
                if (contextResult != Result.Succeeded)
                    return contextResult;

                if (!GraphicsSelectionService.TryPickSingleElementId(
                    context.UIDocument,
                    new ModelCategorySelectionFilter(),
                    "Pick SOURCE element to copy category graphics from.",
                    out ElementId sourceElementId,
                    out _))
                {
                    return Result.Cancelled;
                }

                Element sourceElement = context.Document.GetElement(sourceElementId);
                Category sourceCategory = GraphicsCategoryService.GetCategoryFromElement(
                    sourceElement,
                    context.ActiveView,
                    includeAnnotationCategories: false);

                if (sourceCategory == null)
                {
                    DialogHelper.ShowError(dialogTitle, "Source element does not have a valid overridable model category in this view.");
                    return Result.Cancelled;
                }

                int sourceCategoryKey = ElementIdHelper.GetIntegerValue(sourceCategory.Id);
                OverrideGraphicSettings sourceSettings = GraphicsOverrideBuilder.Clone(
                    context.ActiveView.GetCategoryOverrides(sourceCategory.Id));

                var processedCategoryIds = new HashSet<int>();
                int appliedCount = 0;

                using (var transactionGroup = new TransactionGroup(context.Document, "AJ Tools - Match Category Graphics"))
                {
                    transactionGroup.Start();

                    while (true)
                    {
                        if (!GraphicsSelectionService.TryPickSingleElementId(
                            context.UIDocument,
                            new ModelCategorySelectionFilter(),
                            "Select TARGET element (ESC to finish).",
                            out ElementId targetElementId,
                            out bool wasCancelled))
                        {
                            if (wasCancelled)
                            {
                                break;
                            }

                            continue;
                        }

                        if (targetElementId == sourceElementId)
                        {
                            continue;
                        }

                        Element targetElement = context.Document.GetElement(targetElementId);
                        Category targetCategory = GraphicsCategoryService.GetCategoryFromElement(
                            targetElement,
                            context.ActiveView,
                            includeAnnotationCategories: false);

                        if (!ElementIdHelper.IsValid(targetCategory?.Id) ||
                            ElementIdHelper.GetIntegerValue(targetCategory.Id) == sourceCategoryKey)
                        {
                            continue;
                        }

                        int categoryKey = ElementIdHelper.GetIntegerValue(targetCategory.Id);
                        if (processedCategoryIds.Contains(categoryKey))
                        {
                            continue;
                        }

                        using (var transaction = new Transaction(context.Document, "AJ Tools - Match Category Graphics"))
                        {
                            transaction.Start();
                            try
                            {
                                context.ActiveView.SetCategoryOverrides(targetCategory.Id, sourceSettings);
                                transaction.Commit();
                                processedCategoryIds.Add(categoryKey);
                                appliedCount++;
                            }
                            catch
                            {
                                transaction.RollBack();
                            }
                        }
                    }

                    if (appliedCount > 0)
                    {
                        transactionGroup.Assimilate();
                    }
                    else
                    {
                        transactionGroup.RollBack();
                    }
                }

                if (appliedCount == 0)
                {
                    return Result.Cancelled;
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                DialogHelper.ShowError(dialogTitle, ex.Message);
                return Result.Failed;
            }
        }
    }
}
