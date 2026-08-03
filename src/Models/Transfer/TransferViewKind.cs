#region Metadata
/*
 * Tool Name     : Transfer Views (shared model)
 * File Name     : TransferViewKind.cs
 * Purpose       : Identifies which kind of view-like element (Schedule, Legend, Drafting View) the shared
 *                 Transfer Views engine is working with, and supplies its display text.
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
 * - Companion to the existing, separate Transfer View Templates tool (CmdTransferViewTemplates.cs), which
 *   is intentionally left untouched - View Templates keep their own dedicated command/window since its
 *   override behaviour (re-pointing views' ViewTemplateId) is a different shape to these three (re-placing
 *   Viewports/ScheduleSheetInstances on sheets).
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-07-28) - Initial release: Schedule, Legend, DraftingView kinds.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

namespace AJTools.Models.Transfer
{
    public enum TransferViewKind
    {
        Schedule = 0,
        Legend = 1,
        DraftingView = 2
    }

    internal static class TransferViewKindExtensions
    {
        public static string GetToolTitle(this TransferViewKind kind)
        {
            switch (kind)
            {
                case TransferViewKind.Schedule:
                    return "Transfer Schedules";
                case TransferViewKind.Legend:
                    return "Transfer Legends";
                default:
                    return "Transfer Drafting Views";
            }
        }

        public static string GetNounSingular(this TransferViewKind kind)
        {
            switch (kind)
            {
                case TransferViewKind.Schedule:
                    return "schedule";
                case TransferViewKind.Legend:
                    return "legend";
                default:
                    return "drafting view";
            }
        }

        public static string GetNounPlural(this TransferViewKind kind)
        {
            switch (kind)
            {
                case TransferViewKind.Schedule:
                    return "schedules";
                case TransferViewKind.Legend:
                    return "legends";
                default:
                    return "drafting views";
            }
        }

        public static string GetDescription(this TransferViewKind kind)
        {
            return "Copy selected " + kind.GetNounPlural() + " between open project files.";
        }

        public static string GetOverrideCheckboxText(this TransferViewKind kind)
        {
            return "Override " + kind.GetNounPlural() + " with same name in target project (keeps their sheet placement)";
        }
    }
}
