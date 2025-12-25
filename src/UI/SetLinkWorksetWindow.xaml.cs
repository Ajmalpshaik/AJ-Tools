// Tool Name: Set Link Workset UI
// Description: Dialog to assign Revit links and CAD imports to a workset.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-23
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, System.Windows

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using AJTools.Utils;

namespace AJTools.UI
{
    /// <summary>
    /// Interaction logic for SetLinkWorksetWindow.xaml
    /// </summary>
    public partial class SetLinkWorksetWindow : Window
    {
        private const string DialogTitle = "Set Link Workset";
        private readonly Document _doc;
        private readonly ObservableCollection<LinkWorksetItem> _linkItems;

        public SetLinkWorksetWindow(Document doc)
        {
            InitializeComponent();

            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _linkItems = new ObservableCollection<LinkWorksetItem>(CollectLinkItems(_doc));
            links_listbox.ItemsSource = _linkItems;

            var worksetNames = new FilteredWorksetCollector(_doc)
                .OfKind(WorksetKind.UserWorkset)
                .ToWorksets()
                .Select(ws => ws.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            workset_combobox.ItemsSource = worksetNames;
            workset_combobox.Text = LinkWorksetSettings.GetLastWorksetName();

            Loaded += (s, e) => workset_combobox.Focus();
        }

        private static IList<LinkWorksetItem> CollectLinkItems(Document doc)
        {
            var linkInstances = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .WhereElementIsNotElementType()
                .Cast<Element>();

            var importInstances = new FilteredElementCollector(doc)
                .OfClass(typeof(ImportInstance))
                .WhereElementIsNotElementType()
                .Cast<Element>();

            return linkInstances
                .Concat(importInstances)
                .Select(element => new LinkWorksetItem(element))
                .OrderBy(item => item.Name ?? string.Empty, StringComparer.Ordinal)
                .ToList();
        }

        private void OnSelectAll(object sender, RoutedEventArgs e)
        {
            foreach (var item in _linkItems)
            {
                item.IsChecked = true;
            }
        }

        private void OnSelectNone(object sender, RoutedEventArgs e)
        {
            foreach (var item in _linkItems)
            {
                item.IsChecked = false;
            }
        }

        private void OnInvertSelection(object sender, RoutedEventArgs e)
        {
            foreach (var item in _linkItems)
            {
                item.IsChecked = !item.IsChecked;
            }
        }

        private void OnAssignWorkset(object sender, RoutedEventArgs e)
        {
            string targetWorksetName = workset_combobox.Text ?? string.Empty;
            if (string.IsNullOrEmpty(targetWorksetName))
            {
                DialogHelper.ShowError(DialogTitle, "Please provide or select a name for the workset.");
                return;
            }

            List<Element> linksToMove = _linkItems
                .Where(item => item.IsChecked)
                .Select(item => item.Element)
                .ToList();

            if (linksToMove.Count == 0)
            {
                DialogHelper.ShowError(DialogTitle, "No links were selected to be moved.");
                return;
            }

            try
            {
                using (var t = new Transaction(_doc, "Assign Links to Workset"))
                {
                    t.Start();

                    if (!_doc.IsWorkshared)
                    {
                        _doc.EnableWorksharing("Shared Levels and Grids", "Workset1");
                    }

                    Workset targetWorkset = FindOrCreateWorkset(_doc, targetWorksetName);

                    foreach (Element link in linksToMove)
                    {
                        Parameter worksetParam = link.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                        if (worksetParam != null && !worksetParam.IsReadOnly)
                        {
                            // The workset parameter expects an integer Workset id value
                            worksetParam.Set(targetWorkset.Id.IntegerValue);
                        }
                    }

                    t.Commit();
                }

                LinkWorksetSettings.SaveLastWorksetName(targetWorksetName);
                Close();
                DialogHelper.ShowInfo(
                    "Success",
                    $"Successfully moved {linksToMove.Count} link(s) to the \"{targetWorksetName}\" workset.");
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(DialogTitle, "Failed to assign workset:\n\n" + ex.Message);
            }
        }

        private static Workset FindOrCreateWorkset(Document doc, string targetWorksetName)
        {
            var existingWorksets = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .ToWorksets();

            foreach (Workset workset in existingWorksets)
            {
                if (workset.Name == targetWorksetName)
                    return workset;
            }

            return Workset.Create(doc, targetWorksetName);
        }
    }

    internal sealed class LinkWorksetItem : INotifyPropertyChanged
    {
        private bool _isChecked;

        internal LinkWorksetItem(Element element)
        {
            Element = element;
            _isChecked = true;
            Name = GetDisplayName(element);
        }

        public string Name { get; }

        public Element Element { get; }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value)
                    return;

                _isChecked = value;
                OnPropertyChanged(nameof(IsChecked));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private static string GetDisplayName(Element element)
        {
            if (element is RevitLinkInstance linkInstance)
            {
                string name = linkInstance.Name ?? string.Empty;
                int index = name.IndexOf(':');
                return index >= 0 ? name.Substring(0, index) : name;
            }

            if (element is ImportInstance importInstance)
            {
                Parameter param = importInstance.get_Parameter(BuiltInParameter.IMPORT_SYMBOL_NAME);
                if (param != null && param.HasValue)
                    return param.AsString();
            }

            return "Unnamed Import/Link";
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
