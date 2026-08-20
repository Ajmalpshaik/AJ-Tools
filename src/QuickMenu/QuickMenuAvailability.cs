#region Metadata
/*
 * Tool Name     : AJ Quick Menu (availability mirror)
 * File Name     : QuickMenuAvailability.cs
 * Purpose       : Asks a wheel slot the same question Revit asks a ribbon button before it greys it
 *                 out - "can this tool run right now?" - so the wheel shows a tool as unavailable
 *                 instead of appearing to run it and doing nothing.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-20
 * Last Updated  : 2026-08-20
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit UI API (IExternalCommandAvailability, UIApplication, CategorySet),
 *                 QuickToolEntry (carries the availability class name read off the live ribbon)
 *
 * Input         : The running UIApplication and the wheel's slot list.
 * Output        : One true/false per slot. Opens no transaction, changes nothing, never shows a dialog -
 *                 the caller decides what to say (house rule: shared code returns reasons, the
 *                 command shows them).
 *
 * Notes         :
 * - WHY THIS EXISTS. A wheel slot used to be posted to Revit whether or not its button was greyed
 *   out. Revit silently ignores a posted command whose availability class says no, so the tool
 *   simply never ran and nothing at all was said. The panel already shows that state by greying the
 *   button; the wheel now shows it the same way, for the same reason, from the same class.
 * - IT IS THE BUTTON'S OWN CLASS, NOT A COPY OF ITS RULES. Nothing about "needs a plan view" or
 *   "needs filters" is written here. The class named on the ribbon button is instantiated and asked.
 *   Add a new availability class to a button in a ribbon manager and the wheel follows by itself.
 * - VERIFIED SIGNATURE. IExternalCommandAvailability has exactly ONE method,
 *   bool IsCommandAvailable(UIApplication, CategorySet), identical in Revit 2020 and 2027 - read
 *   from the installed RevitAPIUI.dll metadata. The XML docs Autodesk ships list three overloads;
 *   they are stale and code written against them does not compile.
 * - UNKNOWN MEANS ALLOWED. If the class cannot be found, built or asked, the slot stays enabled.
 *   A fault in this file must never make a working tool unreachable - QuickMenuLauncher's
 *   CanPostCommand guard is the second net, and that one can explain itself.
 * - REVIT'S OWN COMMANDS ARE NOT ASKED. Revit decides those for itself when the command is posted,
 *   and add-ins are given no availability object for them.
 * - THE WHOLE WHEEL IS ASKED IN ONE PASS, ON PURPOSE. Evaluate() is the only way in, because the
 *   selected-categories set Revit passes to an availability class costs one walk of the entire
 *   selection to build - and building it per slot meant walking a 50,000-element selection once for
 *   every slot that had a rule, which would have made the wheel slow to open in exactly the case it
 *   was meant to fix. It is built once per opening, and lazily: a wheel whose slots carry no
 *   availability rule never touches the selection at all.
 * - The instances are cached: these classes are stateless rule checks, and the wheel asks every
 *   slot on every open.
 *
 * Changelog     :
 * v1.0.0 (2026-08-20) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.Services.QuickMenu
{
    /// <summary>Mirrors the ribbon's greyed-out state onto a wheel slot.</summary>
    internal static class QuickMenuAvailability
    {
        /// <summary>One instance per availability class - they hold no state worth rebuilding.</summary>
        private static readonly Dictionary<string, IExternalCommandAvailability> _checkers =
            new Dictionary<string, IExternalCommandAvailability>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Class names already tried and found unusable, so they are not retried per open.</summary>
        private static readonly HashSet<string> _unresolvable =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// One "would the ribbon let this be clicked right now" answer per slot, in slot order. An
        /// empty slot and anything that cannot be determined both count as available - see the Notes
        /// block. This is the only entry point on purpose: the selected-categories set is expensive
        /// to build and must be built once for the whole wheel, never once per slot.
        /// </summary>
        internal static IList<bool> Evaluate(UIApplication uiapp, IList<QuickToolEntry> slots)
        {
            var answers = new List<bool>(slots != null ? slots.Count : 0);
            if (slots == null)
            {
                return answers;
            }

            // Built at most once per wheel, and only if some slot actually has a rule to ask.
            CategorySet selectedCategories = null;

            foreach (QuickToolEntry entry in slots)
            {
                // An empty slot has nothing to grey out; the wheel draws it as empty either way.
                if (entry == null)
                {
                    answers.Add(true);
                    continue;
                }

                if (uiapp == null)
                {
                    answers.Add(false);
                    continue;
                }

                // Revit's own commands: Revit decides when the command is posted, not us.
                if (entry.Source == QuickToolSource.Revit)
                {
                    answers.Add(true);
                    continue;
                }

                // No availability class on the button means the ribbon never greys it out.
                if (string.IsNullOrEmpty(entry.AvailabilityClassName))
                {
                    answers.Add(true);
                    continue;
                }

                IExternalCommandAvailability checker = Resolve(entry.AvailabilityClassName);
                if (checker == null)
                {
                    answers.Add(true);
                    continue;
                }

                if (selectedCategories == null)
                {
                    selectedCategories = SelectedCategories(uiapp);
                }

                try
                {
                    answers.Add(checker.IsCommandAvailable(uiapp, selectedCategories));
                }
                catch (Exception)
                {
                    // A rule that throws is a rule we cannot honour - leave the slot usable.
                    answers.Add(true);
                }
            }

            return answers;
        }

        /// <summary>The availability class named on the button, built once and kept. Null if unusable.</summary>
        private static IExternalCommandAvailability Resolve(string className)
        {
            IExternalCommandAvailability cached;
            if (_checkers.TryGetValue(className, out cached))
            {
                return cached;
            }

            if (_unresolvable.Contains(className))
            {
                return null;
            }

            IExternalCommandAvailability created = null;

            try
            {
                // Every AJ Tools availability class lives in this same add-in assembly.
                Type type = Assembly.GetExecutingAssembly().GetType(className, false, true);
                if (type != null)
                {
                    created = Activator.CreateInstance(type) as IExternalCommandAvailability;
                }
            }
            catch (Exception)
            {
                created = null;
            }

            if (created == null)
            {
                _unresolvable.Add(className);
                return null;
            }

            _checkers[className] = created;
            return created;
        }

        /// <summary>
        /// The categories of whatever is selected, which is the second argument Revit hands an
        /// availability class. An empty set when nothing is selected or the document is unavailable.
        /// </summary>
        private static CategorySet SelectedCategories(UIApplication uiapp)
        {
            var categories = new CategorySet();

            try
            {
                UIDocument uiDocument = uiapp.ActiveUIDocument;
                Document document = uiDocument != null ? uiDocument.Document : null;
                if (document == null)
                {
                    return categories;
                }

                foreach (ElementId id in uiDocument.Selection.GetElementIds())
                {
                    Element element = document.GetElement(id);
                    Category category = element != null ? element.Category : null;

                    if (category != null && !categories.Contains(category))
                    {
                        categories.Insert(category);
                    }
                }
            }
            catch (Exception)
            {
                // A selection we cannot read is the same as no selection for this question.
            }

            return categories;
        }
    }
}
