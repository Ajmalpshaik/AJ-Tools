// ==================================================
// Tool Name    : Graphics Tools
// Purpose      : Copies category override settings from a source category to target categories.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.1.0
// Created      : 2026-03-30
// Last Updated : 2026-05-06
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : One source model element and one or more target model elements.
// Output       : Target category graphics overrides matched in the active view.
// Notes        : Normal success is silent; validation and critical errors are reported to the user.
// Changelog    : v1.1.0 - Cleaned Graphics Tools command flow, shared validation/transaction handling, and metadata.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

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

                OverrideGraphicSettings sourceSettings = GraphicsOverrideBuilder.Clone(
                    context.ActiveView.GetCategoryOverrides(sourceCategory.Id));

                var processedCategoryIds = new HashSet<int>();
                int appliedCount = 0;

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

                    if (targetCategory == null ||
                        targetCategory.Id == null ||
                        targetCategory.Id == ElementId.InvalidElementId ||
                        targetCategory.Id.IntegerValue == sourceCategory.Id.IntegerValue)
                    {
                        continue;
                    }

                    int categoryKey = targetCategory.Id.IntegerValue;
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

