#region Metadata
/*
 * Tool Name     : Unhide All (service)
 * File Name     : UnhideAllService.cs
 * Purpose       : The actual model work behind the Unhide All tool, separated from any UI so the
 *                 command that calls it (CmdUnhideAll) only has to present the result. Returns a
 *                 result object and shows nothing itself.
 *                 The split was made for a second caller (the Web Panel) that was removed in 1.44.0.
 *                 It is kept because UI-free model work is worth having on its own - it is the shape
 *                 any future caller needs, and it is testable without a dialog.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-12
 * Last Updated  : 2026-08-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : An open Document and the View to act on. Caller validates both first.
 * Output        : UnhideAllResult - counts and a ready-to-display summary. No dialog, no popup.
 *
 * Notes         :
 * - This service must never show UI. A TaskDialog raised from here would appear on the Revit
 *   screen even when the run was triggered from a browser, leaving the person looking at the
 *   browser with no result and Revit blocked on a dialog nobody can see. Callers decide how to
 *   present Summary.
 * - Collector uses a full-model scan to safely capture permanently hidden elements (a view-scoped
 *   collector may exclude hidden elements - carried over unchanged from CmdUnhideAll v1.2.0).
 * - Owns its own transaction, so a caller only needs a valid document and view.
 *
 * Changelog     :
 * v1.0.0 (2026-08-12) - Extracted verbatim from CmdUnhideAll v1.2.0 while adding the Web Panel.
 *                       Behaviour is unchanged: same collector, same transaction name, same
 *                       Temporary Hide/Isolate handling, same wording in the summary. The only
 *                       difference is that the summary is returned instead of shown.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace AJTools.Services.UnhideAll
{
    /// <summary>What Unhide All did, with the exact text every front door should display.</summary>
    public class UnhideAllResult
    {
        public int UnhiddenCount { get; set; }
        public bool TemporaryHideIsolateCleared { get; set; }

        /// <summary>
        /// The two-line report shown by the ribbon command and returned to the browser. Kept here
        /// rather than in each caller so the wording can never drift between the two.
        /// </summary>
        public string Summary
        {
            get
            {
                string elementsLine = UnhiddenCount > 0
                    ? UnhiddenCount + " hidden element(s) restored."
                    : "No permanently hidden elements found.";

                string thiLine = TemporaryHideIsolateCleared
                    ? "Temporary Hide/Isolate cleared."
                    : "Temporary Hide/Isolate was not active.";

                return elementsLine + "\n" + thiLine;
            }
        }
    }

    public static class UnhideAllService
    {
        private const string TransactionName = "AJ-Tools: Unhide All";

        /// <summary>
        /// Unhides every permanently hidden element in <paramref name="view"/> and clears Temporary
        /// Hide/Isolate if it is active. The caller is responsible for having validated the document
        /// and view first (see ValidationHelper).
        /// </summary>
        public static UnhideAllResult Run(Document doc, View view)
        {
            ICollection<ElementId> hiddenIds = CollectHiddenElementIds(doc, view);
            bool thiCleared;

            using (Transaction trans = new Transaction(doc, TransactionName))
            {
                trans.Start();

                if (hiddenIds.Count > 0)
                    view.UnhideElements(hiddenIds);

                thiCleared = TryDisableTemporaryHideIsolate(view);

                trans.Commit();
            }

            return new UnhideAllResult
            {
                UnhiddenCount = hiddenIds.Count,
                TemporaryHideIsolateCleared = thiCleared
            };
        }

        private static ICollection<ElementId> CollectHiddenElementIds(Document doc, View view)
        {
            var hiddenIds = new List<ElementId>();

            foreach (Element element in new FilteredElementCollector(doc).WhereElementIsNotElementType())
            {
                if (element == null || !element.IsValidObject)
                    continue;

                if (IsElementHidden(element, view))
                    hiddenIds.Add(element.Id);
            }

            return hiddenIds;
        }

        private static bool IsElementHidden(Element element, View view)
        {
            try { return element.IsHidden(view); }
            catch { return false; }
        }

        private static bool TryDisableTemporaryHideIsolate(View view)
        {
            try
            {
                view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
