#region Metadata
/*
 * Tool Name     : Purge Unused Elements (shared model)
 * File Name     : UnusedElementPurgeItem.cs
 * Purpose       : One scanned row (a View Template, Filter, or Group type) with its safety status, bound
 *                 to the PurgeUnusedElementsWindow data grid.
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
 * Dependencies  : None
 *
 * Input         : N/A
 * Output        : N/A
 *
 * Notes         :
 * - Shaped after UnplacedViewPurgeItem but generalized to a plain Element (ElementKind is a free-text
 *   label such as "View Template" / "Filter" / "Model Group" / "Detail Group" rather than a fixed set of
 *   view types) since Groups mode mixes two kinds in one scan.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-07-28) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System.ComponentModel;

namespace AJTools.Models.Purge
{
    internal sealed class UnusedElementPurgeItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public int ElementIdValue { get; set; }

        public string ElementName { get; set; }

        public string ElementKind { get; set; }

        public string StatusReason { get; set; }

        public string DetailedNotes { get; set; }

        public bool IsDeletable { get; set; }

        public UnusedElementPurgeStatus Status { get; set; }

        public string StatusText
        {
            get { return Status.ToDisplayText(); }
        }

        public bool CanSelectForDeletion
        {
            get { return IsDeletable && Status == UnusedElementPurgeStatus.SafeToPurge; }
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
            Status = UnusedElementPurgeStatus.SafeToPurge;
            IsDeletable = true;
            StatusReason = reason ?? string.Empty;
            DetailedNotes = notes ?? string.Empty;
            IsSelected = true;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(CanSelectForDeletion));
        }

        public void MarkSkipped(string reason, string notes)
        {
            Status = UnusedElementPurgeStatus.Skipped;
            IsDeletable = false;
            StatusReason = reason ?? string.Empty;
            DetailedNotes = notes ?? string.Empty;
            IsSelected = false;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(CanSelectForDeletion));
        }

        public void MarkCannotDelete(string reason, string notes)
        {
            Status = UnusedElementPurgeStatus.CannotDelete;
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
