#region Metadata
/*
 * Tool Name     : View Crop
 * File Name     : ViewCropAnnotationSettingsTracker.cs
 * Purpose       : Stores last-used annotation crop settings for the active Revit session.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-04-11
 * Last Updated  : 2026-06-27
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API
 *
 * Input         : Active Revit Document, annotation crop settings.
 * Output        : Cached last settings (per document) - in-memory only.
 *
 * Notes         :
 * - Inherits the shared per-document caching logic from SettingsTrackerBase{T}.
 * - No disk persistence - annotation crop offset is intentionally session-scoped.
 *
 * Changelog     :
 * v1.1.0 (2026-06-27) - Refactored onto shared SettingsTrackerBase. Behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using Autodesk.Revit.DB;
using AJTools.Models.ViewCrop;

namespace AJTools.Services.ViewCrop
{
    /// <summary>
    /// Stores annotation crop settings in-memory for the active document during the session.
    /// </summary>
    internal sealed class ViewCropAnnotationSettingsTracker : SettingsTrackerBase<ViewCropAnnotationSettings>
    {
        internal ViewCropAnnotationSettingsTracker(Document doc) : base(doc)
        {
        }

        protected override ViewCropAnnotationSettings CreateDefault() => new ViewCropAnnotationSettings();

        protected override ViewCropAnnotationSettings CloneSettings(ViewCropAnnotationSettings settings) =>
            (settings ?? CreateDefault()).Clone();
    }
}
