// Tool Name: Pin Category Item Model
// Description: UI row model for selectable pin target groups.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-15
// Revit Version: 2020

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AJTools.Models.PinTools
{
    /// <summary>
    /// Represents one selectable row in the Pin Elements dialog.
    /// </summary>
    internal sealed class PinCategoryItem : INotifyPropertyChanged
    {
        private bool _isChecked;

        public PinCategoryItem(PinTargetGroup group, string name, string description, int candidateCount, bool isChecked)
        {
            Group = group;
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            CandidateCount = candidateCount;
            _isChecked = isChecked;
        }

        public PinTargetGroup Group { get; }

        public string Name { get; }

        public string Description { get; }

        public int CandidateCount { get; }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value)
                    return;

                _isChecked = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
