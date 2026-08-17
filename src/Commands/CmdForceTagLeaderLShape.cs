#region Metadata
/*
 * Tool Name     : L-Shape Leader
 * File Name     : CmdForceTagLeaderLShape.cs
 * Purpose       : Forces tags to use a right-angle (L-shaped) leader by computing the elbow position with
 *                 LeaderLogicService. Works on pre-selected tags or picked tags (Tab cycles) until Esc.
 *
 * Author        : Ajmal P.S.
 * Version       : 2.2.1
 *
 * Created Date  : 2026-02-15
 * Last Updated  : 2026-08-16
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Services.LeaderLogic (LeaderLogicService),
 *                 AJTools.Services.ForceTagLeaderLShape, AJTools.Utils
 *
 * Input         : Active View - pre-selected tags, or tags picked one-by-one (Esc to finish).
 * Output        : Tag leaders converted to L-shape elbows; skipped tags reported.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Elbow geometry comes from the shared LeaderLogicService (single source of L-shape leader logic).
 * - Esc during a pick is a normal cancel (handled silently).
 * - The result is DETERMINISTIC: the same tag head and leader end always produce the same elbow, so
 *   running the tool twice on one tag gives the same answer both times. The header and ribbon tooltip
 *   used to claim "run again to flip the elbow side" - no such logic ever existed, and the unused
 *   LeaderToggleState enum in LeaderLogicService was the only trace of the idea. Corrected 2026-08-16
 *   rather than inventing the flip, because this tool works well as it is and a side-flip is a
 *   behaviour change nobody asked for. If a flip is wanted, it belongs in its own change.
 * - Thin command wrapper: selection, the pick-loop, and transaction/sub-transaction handling live
 *   here; reflection-based tag property access, bounding-box math, and elbow-adjustment/margin
 *   geometry live in Services/ForceTagLeaderLShape/ForceTagLeaderLShapeService.cs.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v2.2.1 (2026-08-16) - Corrected the header and ribbon tooltip, which both promised "run again on the
 *                       same tag flips the elbow side". The code never did that. No behaviour change.
 * v2.2.0 (2026-07-17) - Extracted the non-elbow-math logic (reflection-based tag property access,
 *                       bounding-box math, elbow-adjustment/margin geometry) into
 *                       Services/ForceTagLeaderLShape/ForceTagLeaderLShapeService.cs (code review
 *                       cleanup pass) - no behavior change. Elbow math itself still comes from the
 *                       shared LeaderLogicService, called directly as before.
 * v2.0.0 (2026-04-07) - Elbow computation moved to LeaderLogicService; pre-select + pick-loop support.
 * v2.1.0 (2026-07-01) - Refactor/audit: added full metadata block. Leader behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using AJTools.Services.ForceTagLeaderLShape;
using AJTools.Services.LeaderLogic;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Forces tags to use a right-angle leader by computing the elbow position
    /// using <see cref="LeaderLogicService"/> view-space logic.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdForceTagLeaderLShape : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application?.ActiveUIDocument;
            Document doc = uidoc?.Document;

            if (doc == null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            if (doc.IsReadOnly)
            {
                DialogHelper.ShowError("L-Shape Tag Leaders", "The document is read-only.");
                return Result.Cancelled;
            }

            View activeView = doc.ActiveView;
            if (activeView == null)
            {
                message = "No active view.";
                return Result.Failed;
            }

            LeaderLogicService leaderLogic = new LeaderLogicService(activeView);

            IList<Element> preselected = CollectPreselectedTags(doc, uidoc.Selection.GetElementIds());
            if (preselected.Count > 0)
            {
                // Ask before a long run. Only the PRESELECTED path needs this - it reworks every tag in
                // one go with nothing to press. The pick-one-at-a-time path below is self-paced and
                // stops on Esc, so it never freezes.
                if (!DialogHelper.ConfirmLongRun(
                        "L-Shape Tag Leaders",
                        preselected.Count,
                        string.Format(
                            "About to force an L-shaped leader on {0} selected tags.",
                            preselected.Count)))
                {
                    return Result.Cancelled;
                }

                int updated;
                int skipped;
                ApplyToSelection(doc, activeView, preselected, leaderLogic, out updated, out skipped);

                if (updated == 0)
                {
                    DialogHelper.ShowError("L-Shape Tag Leaders", "No editable tag leaders were found in the selection.");
                    return Result.Cancelled;
                }

                if (skipped > 0)
                {
                    DialogHelper.ShowInfo(
                        "L-Shape Tag Leaders",
                        $"Updated {updated} tag(s). Skipped {skipped}.");
                }

                return Result.Succeeded;
            }

            int changedCount = 0;
            int skippedCount = 0;
            bool hadPick = false;
            TagLeaderSelectionFilter filter = new TagLeaderSelectionFilter();
            const string prompt = "Pick tag to force L-shaped leader (Esc to finish)";

            while (true)
            {
                Reference picked;
                try
                {
                    picked = uidoc.Selection.PickObject(ObjectType.Element, filter, prompt);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    break;
                }

                hadPick = true;
                Element tag = doc.GetElement(picked);
                if (tag == null)
                    continue;

                bool ok;
                using (Transaction t = new Transaction(doc, "Force L-Shaped Tag Leader"))
                {
                    t.Start();
                    ok = ForceTagLeaderLShapeService.TryForceLShape(tag, activeView, leaderLogic);
                    if (ok)
                    {
                        t.Commit();
                    }
                    else
                    {
                        t.RollBack();
                    }
                }

                if (ok)
                    changedCount++;
                else
                    skippedCount++;
            }

            if (changedCount == 0)
            {
                if (hadPick && skippedCount > 0)
                {
                    DialogHelper.ShowError(
                        "L-Shape Tag Leaders",
                        "The selected tag(s) did not allow a leader elbow edit.");
                }

                return Result.Cancelled;
            }

            if (skippedCount > 0)
            {
                DialogHelper.ShowInfo(
                    "L-Shape Tag Leaders",
                    $"Updated {changedCount} tag(s). Skipped {skippedCount}.");
            }

            return Result.Succeeded;
        }

        private static IList<Element> CollectPreselectedTags(Document doc, ICollection<ElementId> selectedIds)
        {
            List<Element> results = new List<Element>();
            if (selectedIds == null || selectedIds.Count == 0)
                return results;

            foreach (ElementId id in selectedIds)
            {
                Element element = doc.GetElement(id);
                if (element == null)
                    continue;

                if (ForceTagLeaderLShapeService.HasProperty(element, "TagHeadPosition"))
                    results.Add(element);
            }

            return results;
        }

        private static void ApplyToSelection(Document doc, View activeView, IList<Element> tags, LeaderLogicService leaderLogic,
            out int updated, out int skipped)
        {
            updated = 0;
            skipped = 0;

            using (Transaction t = new Transaction(doc, "Force L-Shaped Tag Leaders"))
            {
                t.Start();

                foreach (Element tag in tags)
                {
                    using (SubTransaction st = new SubTransaction(doc))
                    {
                        st.Start();

                        if (ForceTagLeaderLShapeService.TryForceLShape(tag, activeView, leaderLogic))
                        {
                            st.Commit();
                            updated++;
                        }
                        else
                        {
                            st.RollBack();
                            skipped++;
                        }
                    }
                }

                t.Commit();
            }
        }

        private class TagLeaderSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                return ForceTagLeaderLShapeService.HasProperty(elem, "TagHeadPosition");
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}
