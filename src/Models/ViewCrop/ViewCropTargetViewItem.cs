#region Metadata
/*
 * Tool Name     : View Crop
 * File Name     : ViewCropTargetViewItem.cs
 * Purpose       : Represents one selectable target view row in the View Crop view-picker UI.
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
 * Dependencies  : Autodesk Revit API, WPF (INotifyPropertyChanged)
 *
 * Input         : View metadata populated by ViewCropTargetViewCollector.
 * Output        : View row bound by the WPF picker.
 *
 * Notes         :
 * - CanSelect=false rows are visible but cannot be checked - they show the SupportMessage instead.
 *
 * Changelog     :
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

        public bool CanSelect { get; set; }

        public string SupportMessage { get; set; }

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

        public string StatusText => CanSelect ? "Supported" : SupportMessage;

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
