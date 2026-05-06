// ==================================================
// Tool Name    : View Crop
// Purpose      : Represents selectable target views in the View Crop batch UI.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.1
// Created      : 2026-04-08
// Last Updated : 2026-05-06
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API, WPF
// Input        : Active Revit document, active or selected target views, and View Crop settings.
// Output       : Updated view crop or annotation crop settings for supported target views.
// Notes        : Skips unsupported, template, scope-box-controlled, and view-template-locked views.
// Changelog    : v1.0.1 - Standardized metadata after production cleanup.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================
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
