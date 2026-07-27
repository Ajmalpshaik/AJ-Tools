#region Metadata
/*
 * Tool Name     : Arrange Tags Settings
 * File Name     : CmdIntelligentTagArrangerSettings.cs
 * Purpose       : Settings dialog that stores the default vertical spacing (mm) used by Rearrange Tags.
 *
 * Author        : Ajmal P.S.
 * Version       : 2.0.0
 *
 * Created Date  : 2026-04-07
 * Last Updated  : 2026-07-27
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Utils (TagArrangeSettings, DialogHelper),
 *                 AJTools.UI.TagArrange.ArrangeTagsSettingsWindow (WPF)
 *
 * Input         : A spacing value (mm) entered in the settings window.
 * Output        : Saved spacing setting (no model change; read-only to the Revit model).
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Settings-only tool; does not modify the model, so no transaction.
 * - Modal WPF window shown from inside Execute, owned by the Revit main window so it cannot fall behind Revit.
 * - Works with or without an open project; the active view scale is only used for the live explanation text.
 * - Cancel closes silently. A save is confirmed by reading the value back; only a failed write reports an error.
 *
 * Changelog     :
 * v1.0.0 (2026-04-07) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. Settings behaviour unchanged.
 * v2.0.0 (2026-07-27) - UI rebuilt as a themed WPF window (was a plain WinForms prompt): live inline
 *                       validation instead of losing the entry on a typo, presets, reset to default,
 *                       live sheet/model explanation. Fixed comma-decimal locale misreads, added a
 *                       sane range check, dropped the unnecessary open-project requirement, and the
 *                       save is now verified by read-back instead of assumed.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.UI.TagArrange;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Opens settings dialog for Arrange Tags spacing (in mm).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CmdIntelligentTagArrangerSettings : IExternalCommand
    {
        private const string Title = "Arrange Tags Settings";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiapp = commandData?.Application;
                if (uiapp == null)
                {
                    message = "No Revit session available.";
                    return Result.Failed;
                }

                double current = TagArrangeSettings.GetTagSpacingMm();

                var window = new ArrangeTagsSettingsWindow(
                    current,
                    TagArrangeSettings.DefaultTagSpacingMm,
                    GetActiveViewScale(uiapp));

                new WindowInteropHelper(window)
                {
                    Owner = uiapp.MainWindowHandle
                };

                if (window.ShowDialog() != true)
                    return Result.Cancelled;

                double spacingMm = window.SpacingMm;
                TagArrangeSettings.SaveTagSpacingMm(spacingMm);

                // Settings writes are swallowed by design; confirm the value actually stuck.
                double stored = TagArrangeSettings.GetTagSpacingMm();
                if (System.Math.Abs(stored - spacingMm) > 0.0005)
                {
                    DialogHelper.ShowError(
                        Title,
                        "The spacing could not be saved. Check that AJ Tools can write to your AppData folder, then try again.");
                    return Result.Failed;
                }

                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        /// <summary>
        /// Returns the active view scale (1:x) for the explanation text, or 0 when there is no usable view.
        /// </summary>
        private static int GetActiveViewScale(UIApplication uiapp)
        {
            try
            {
                Document doc = uiapp?.ActiveUIDocument?.Document;
                View view = doc?.ActiveView;
                if (view == null)
                    return 0;

                return view.Scale > 0 ? view.Scale : 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
