#region Metadata
/*
 * Tool Name     : Create 3D Views by Workset
 * File Name     : Workset3DViewService.cs
 * Purpose       : Service that creates one isometric 3D view per user workset and isolates each workset
 *                 in its own view, inside a single transaction, with a created/skipped/failed report.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-03-24
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Utils (ValidationHelper, DialogHelper)
 *
 * Input         : Full Project - user worksets of a workshared, editable, non-family document.
 * Output        : One named 3D view per workset; views that already exist are skipped and reported.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Validates document, edit-ability, and worksharing before any change; all views created in one undo step.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-03-24) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. View-creation behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Utils;

namespace AJTools.Services.WorksetViews
{
    /// <summary>
    /// Handles creation of 3D views per user workset and workset visibility isolation.
    /// </summary>
    internal static class Workset3DViewService
    {
        private const string Title = "3D Views as per Workset";

        public static Result Execute(ExternalCommandData commandData, ref string message)
        {
            UIDocument uidoc = commandData?.Application?.ActiveUIDocument;
            if (!ValidationHelper.ValidateUIDocument(uidoc, out message))
            {
                DialogHelper.ShowError(Title, message);
                return Result.Cancelled;
            }

            Document doc = uidoc.Document;
            if (!ValidationHelper.ValidateEditableDocument(doc, out message))
            {
                DialogHelper.ShowError(Title, message);
                return Result.Cancelled;
            }

            if (!doc.IsWorkshared)
            {
                DialogHelper.ShowInfo(Title, "The current model is not workshared.");
                return Result.Cancelled;
            }

            IList<Workset> userWorksets = GetUserWorksets(doc);
            if (userWorksets.Count == 0)
            {
                DialogHelper.ShowInfo(Title, "No user worksets were found in this model.");
                return Result.Cancelled;
            }

            ElementId threeDTypeId = GetThreeDimensionalViewFamilyTypeId(doc);
            if (threeDTypeId == ElementId.InvalidElementId)
            {
                message = "No 3D view family type was found in this project.";
                DialogHelper.ShowError(Title, message);
                return Result.Failed;
            }

            HashSet<string> existingViewNames = CollectExistingViewNames(doc);

            int createdCount = 0;
            int skippedCount = 0;
            int failedCount = 0;

            using (Transaction t = new Transaction(doc, "AJ Tools - Create 3D Views as per Workset"))
            {
                t.Start();

                foreach (Workset targetWorkset in userWorksets)
                {
                    if (existingViewNames.Contains(targetWorkset.Name))
                    {
                        skippedCount++;
                        continue;
                    }

                    View3D view3D = null;
                    try
                    {
                        view3D = View3D.CreateIsometric(doc, threeDTypeId);
                        view3D.Name = targetWorkset.Name;
                        SetWorksetVisibilityForView(view3D, userWorksets, targetWorkset.Id);

                        existingViewNames.Add(targetWorkset.Name);
                        createdCount++;
                    }
                    catch (Autodesk.Revit.Exceptions.ArgumentException)
                    {
                        DeleteCreatedView(doc, view3D);
                        skippedCount++;
                    }
                    catch
                    {
                        DeleteCreatedView(doc, view3D);
                        failedCount++;
                    }
                }

                t.Commit();
            }

            if (failedCount > 0 && createdCount == 0)
            {
                message = "Could not create any 3D views.";
                DialogHelper.ShowError(Title, BuildSummary(createdCount, skippedCount, failedCount));
                return Result.Failed;
            }

            DialogHelper.ShowInfo(Title, BuildSummary(createdCount, skippedCount, failedCount));
            return createdCount > 0 ? Result.Succeeded : Result.Cancelled;
        }

        private static IList<Workset> GetUserWorksets(Document doc)
        {
            return new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .ToWorksets()
                .OrderBy(ws => ws.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static ElementId GetThreeDimensionalViewFamilyTypeId(Document doc)
        {
            ViewFamilyType type = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.ThreeDimensional);

            return type != null ? type.Id : ElementId.InvalidElementId;
        }

        private static HashSet<string> CollectExistingViewNames(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v != null && !string.IsNullOrWhiteSpace(v.Name))
                .Select(v => v.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static void SetWorksetVisibilityForView(View3D view3D, IEnumerable<Workset> worksets, WorksetId targetWorksetId)
        {
            foreach (Workset workset in worksets)
            {
                WorksetVisibility visibility = workset.Id == targetWorksetId
                    ? WorksetVisibility.Visible
                    : WorksetVisibility.Hidden;

                view3D.SetWorksetVisibility(workset.Id, visibility);
            }
        }

        private static void DeleteCreatedView(Document doc, View3D view3D)
        {
            if (view3D == null)
            {
                return;
            }

            if (!view3D.IsValidObject)
            {
                return;
            }

            doc.Delete(view3D.Id);
        }

        private static string BuildSummary(int createdCount, int skippedCount, int failedCount)
        {
            return
                $"Created: {createdCount}\n" +
                $"Skipped (already exists): {skippedCount}\n" +
                $"Failed: {failedCount}";
        }
    }
}
