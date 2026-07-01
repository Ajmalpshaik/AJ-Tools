#region Metadata
/*
 * Tool Name     : Revision Cloud Settings
 * File Name     : CmdRevisionCloudByElementsSettings.cs
 * Purpose       : Settings dialog that stores the offset distance used by Revision Clouds by Elements.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-05-02
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Models.RevisionCloud, AJTools.UI.RevisionCloud
 *
 * Input         : Offset distance (mm) chosen in the window.
 * Output        : Saved revision cloud offset setting (no model change).
 *
 * Notes         :
 * - Targets Revit 2020 through latest. Settings-only tool; does not modify the model.
 * - Cancel closes silently.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-05-02) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. Settings behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models.RevisionCloud;

namespace AJTools.Commands
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CmdRevisionCloudByElementsSettings : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var settings = RevisionCloudSettings.Load() ?? new RevisionCloudSettings();
                if (double.IsNaN(settings.OffsetDistanceMm) || double.IsInfinity(settings.OffsetDistanceMm) || settings.OffsetDistanceMm < 0)
                    settings.OffsetDistanceMm = 50.0;

                var settingsWindow = new UI.RevisionCloud.RevisionCloudSettingsWindow(settings);
                settingsWindow.ShowDialog();

                return settingsWindow.Confirmed ? Result.Succeeded : Result.Cancelled;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Revision Cloud By Elements - Settings Error", ex.Message);
                return Result.Failed;
            }
        }
    }
}
