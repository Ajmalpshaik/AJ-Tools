#region Metadata
/*
 * Tool Name     : View Crop
 * File Name     : ViewCropTargetViewItem.cs
 * Purpose       : Represents one selectable target view row in the View Crop view-picker UI.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.1
 *
 * Created Date  : 2026-04-08
 * Last Updated  : 2026-10-12
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, WPF (INotifyPropertyChanged)
 *
 * Input         : View metadata populated by ViewCropTargetViewCollector.
 * Output        : View row bound by the WPF picker.
 *
 * Notes         :
 * - Only supported plan views are collected (ViewCropTargetViewCollector v1.2.0+), so every row can be
 *   ticked - there is no per-row "cannot select" state.
 *
 * Changelog     :
 * v1.1.1 (2026-10-12) - Removed CanSelect, SupportMessage and StatusText. Only supported views are listed,
 *                       so they were always true / "Supported". Their only readers were the picker's
 *                       always-true checks and its search, where the hidden word "Supported" made any
 *                       part of it (e.g. "sup") match every row.
 * v1.1.0 (2026-06-27) - Metadata refresh and version coverage notes.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion
using System.ComponentModel;
using Autodesk.Revit.DB;

namespace AJTools.Models.ViewCrop
{
    /// <summary>
    /// Represents one selectable target view row in the view picker dialog.
    /// </summary>
    internal sealed class ViewCropTargetViewItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public ElementId ViewId { get; set; }

        public string ViewName { get; set; }

        public string ViewTypeName { get; set; }

        public string SheetNumber { get; set; }

        public string SheetName { get; set; }

        public string GroupName { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
