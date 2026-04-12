// Tool Name: View Crop Target View Item
// Description: UI model for selectable target views in batch crop processing.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-08
// Revit Version: 2020

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
