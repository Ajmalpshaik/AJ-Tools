// ==================================================
// Tool Name    : Purge Unplaced 3D Views and Sections
// Purpose      : Convert Python shell purge workflow into AJ Tools C# Revit add-in.
// Author       : Ajmal P.S.
// Company      : AJ Tools
// Version      : 1.0.0
// Created      : 2026-05-11
// Last Updated : 2026-05-11
// Target       : Revit 2020
// Framework    : .NET Framework 4.7.2
// Platform     : C# Revit Add-in
// Dependencies : Autodesk Revit API
// Input        : Active Revit document and user purge options.
// Output       : Safe purge result with final report.
// Notes        : Added under AJ Tools Purge panel.
// Changelog    : v1.0.0 - Converted from Interactive Python Shell script.
// License      : All Rights Reserved
// Repo         : AJ-Tools
// ==================================================

using System.ComponentModel;

namespace AJTools.Models.Purge
{
    internal sealed class UnplacedViewPurgeItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public int ViewIdValue { get; set; }

        public string ViewName { get; set; }

        public string ViewKind { get; set; }

        public string ViewTypeText { get; set; }

        public string StatusReason { get; set; }

        public string DetailedNotes { get; set; }

        public bool IsDefault3DView { get; set; }

        public bool IsActiveView { get; set; }

        public bool IsDeletable { get; set; }

        public UnplacedViewPurgeStatus Status { get; set; }

        public string StatusText
        {
            get { return Status.ToDisplayText(); }
        }

        public bool CanSelectForDeletion
        {
            get { return IsDeletable && Status == UnplacedViewPurgeStatus.SafeToPurge; }
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value && CanSelectForDeletion;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void MarkSafe(string reason, string notes)
        {
            Status = UnplacedViewPurgeStatus.SafeToPurge;
            IsDeletable = true;
            StatusReason = reason ?? string.Empty;
            DetailedNotes = notes ?? string.Empty;
            IsSelected = true;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(CanSelectForDeletion));
        }

        public void MarkSkipped(string reason, string notes)
        {
            Status = UnplacedViewPurgeStatus.Skipped;
            IsDeletable = false;
            StatusReason = reason ?? string.Empty;
            DetailedNotes = notes ?? string.Empty;
            IsSelected = false;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(CanSelectForDeletion));
        }

        public void MarkCannotDelete(string reason, string notes)
        {
            Status = UnplacedViewPurgeStatus.CannotDelete;
            IsDeletable = false;
            StatusReason = reason ?? string.Empty;
            DetailedNotes = notes ?? string.Empty;
            IsSelected = false;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(CanSelectForDeletion));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
