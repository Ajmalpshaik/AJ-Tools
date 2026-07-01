#region Metadata
/*
 * Tool Name     : View Crop
 * File Name     : ViewCropSettingsTracker.cs
 * Purpose       : Stores last-used View Crop settings for the active document and persists them on disk.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-04-08
 * Last Updated  : 2026-06-27
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Active Revit Document, View Crop settings.
 * Output        : Cached last settings (per document), disk-persisted via ViewCropConfigStore.
 *
 * Notes         :
 * - Inherits the shared per-document caching logic from SettingsTrackerBase{T}.
 * - Disk persistence delegated to ViewCropConfigStore.
 *
 * Changelog     :
 * v1.1.0 (2026-06-27) - Refactored onto shared SettingsTrackerBase. Behaviour unchanged.
 * v1.0.2 (2026-05-24) - Premium disk-based settings persistence.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using Autodesk.Revit.DB;
using AJTools.Models.ViewCrop;
using AJTools.Utils;

namespace AJTools.Services.ViewCrop
{
    /// <summary>
    /// Stores user settings in-memory during the active document session and persists them on disk.
    /// </summary>
    internal sealed class ViewCropSettingsTracker : SettingsTrackerBase<ViewCropSettings>
    {
        internal ViewCropSettingsTracker(Document doc) : base(doc)
        {
        }

        protected override ViewCropSettings CreateDefault() => new ViewCropSettings();

        protected override ViewCropSettings CloneSettings(ViewCropSettings settings) =>
            (settings ?? CreateDefault()).Clone();

        protected override ViewCropSettings LoadFromStore() => ViewCropConfigStore.Load();

        protected override void SaveToStore(ViewCropSettings settings) => ViewCropConfigStore.Save(settings);
    }
}
