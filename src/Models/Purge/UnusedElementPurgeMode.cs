#region Metadata
/*
 * Tool Name     : Purge Unused Elements (shared model)
 * File Name     : UnusedElementPurgeMode.cs
 * Purpose       : Identifies which kind of "declared but not actually used anywhere" element the shared
 *                 Purge Unused Elements engine is scanning for, and supplies its display text.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-28
 * Last Updated  : 2026-07-28
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : None (plain enum + extension methods)
 *
 * Input         : N/A
 * Output        : N/A
 *
 * Notes         :
 * - ViewTemplates = view templates not assigned to any regular view. Filters = view/selection filters
 *   (ParameterFilterElement) not added to any view or view template's V/G overrides. Groups = Model Group
 *   AND Detail Group types with zero placed instances left in the project (shown together in one grid,
 *   distinguished per-row by UnusedElementPurgeItem.ElementKind, filterable via the kind combo).
 * - This is a DIFFERENT shape of "unused" to the existing UnplacedViewPurgeMode family (not-on-a-sheet) -
 *   these three are "not referenced/assigned anywhere", verified the same safe way: a real Document.Delete
 *   probe inside a rolled-back transaction decides what's actually safe, not just the static scan.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-07-28) - Initial release: ViewTemplates, Filters, Groups.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

namespace AJTools.Models.Purge
{
    public enum UnusedElementPurgeMode
    {
        ViewTemplates = 0,
        Filters = 1,
        Groups = 2
    }

    internal static class UnusedElementPurgeModeExtensions
    {
        public static string GetToolTitle(this UnusedElementPurgeMode mode)
        {
            switch (mode)
            {
                case UnusedElementPurgeMode.ViewTemplates:
                    return "Purge Unused View Templates";
                case UnusedElementPurgeMode.Filters:
                    return "Purge Unused Filters";
                default:
                    return "Purge Unused Groups";
            }
        }

        public static string GetElementKindPlural(this UnusedElementPurgeMode mode)
        {
            switch (mode)
            {
                case UnusedElementPurgeMode.ViewTemplates:
                    return "View Templates";
                case UnusedElementPurgeMode.Filters:
                    return "Filters";
                default:
                    return "Groups";
            }
        }

        public static string GetDescription(this UnusedElementPurgeMode mode)
        {
            switch (mode)
            {
                case UnusedElementPurgeMode.ViewTemplates:
                    return "Review view templates that are not assigned to any view before deleting selected safe candidates. " +
                           "A template set as Revit's default for a view type may still show here - the delete check below will catch it as Cannot Delete if it turns out to be relied on.";
                case UnusedElementPurgeMode.Filters:
                    return "Review view/selection filters that are not added to any view or view template before deleting selected safe candidates.";
                default:
                    return "Review Model Group and Detail Group types with zero placed instances left in the project before deleting selected safe candidates.";
            }
        }

        public static string GetTransactionName(this UnusedElementPurgeMode mode)
        {
            return mode.GetToolTitle();
        }
    }
}
