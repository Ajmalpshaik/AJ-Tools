using System;
using System.ComponentModel;

namespace AJTools.Models.Purge
{
    internal sealed class FamilyParameterPurgeItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public int ParameterIdValue { get; set; }

        public string ParameterName { get; set; }

        public string ParameterTypeText { get; set; }

        public string ParameterGroupText { get; set; }

        public string StorageTypeText { get; set; }

        public bool IsInstance { get; set; }

        public string InstanceTypeText
        {
            get { return IsInstance ? "Instance" : "Type"; }
        }

        public bool IsShared { get; set; }

        public Guid SharedGuid { get; set; }

        public string SharedGuidText
        {
            get
            {
                if (!IsShared || SharedGuid == Guid.Empty)
                {
                    return "N/A";
                }

                return SharedGuid.ToString();
            }
        }

        public string SourceText
        {
            get { return IsShared ? "Shared" : "Family"; }
        }

        public bool HasFormula { get; set; }

        public string FormulaFlagText
        {
            get { return HasFormula ? "Yes" : "No"; }
        }

        public string FormulaText { get; set; }

        public bool IsReporting { get; set; }

        public string ReportingFlagText
        {
            get { return IsReporting ? "Yes" : "No"; }
        }

        public bool ReferencedByOtherFormulas { get; set; }

        public string ReferencedByFormulaNames { get; set; }

        public bool AssociatedWithDimensions { get; set; }

        public int AssociatedDimensionCount { get; set; }

        public bool AssociatedWithNestedFamilyParameters { get; set; }

        public int AssociatedNestedParameterCount { get; set; }

        public int AssociatedElementParameterCount { get; set; }

        public bool AssociatedWithVisibilityControls { get; set; }

        public bool HasAnyValue { get; set; }

        public bool HasMeaningfulValue { get; set; }

        public bool HasDefaultLikeOnlyValue { get; set; }

        public bool IsDeletable { get; set; }

        public string DeleteProbeReason { get; set; }

        public ParameterPurgeStatus Status { get; set; }

        public string StatusText
        {
            get { return Status.ToDisplayText(); }
        }

        public string Reason { get; set; }

        public string Warning { get; set; }

        public string DetailedNotes { get; set; }

        public bool CanSelectForDeletion
        {
            get
            {
                return IsDeletable &&
                       (Status == ParameterPurgeStatus.SafeToPurge ||
                        Status == ParameterPurgeStatus.PossiblyUnused);
            }
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

                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
