// Tool Name: Set Link Workset UI
// Description: Dialog to assign Revit links and CAD imports to a workset.
// Author: Ajmal P.S.
// Version: 1.0.1
// Last Updated: 2026-10-12
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, System.Windows

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
        private const string NotWorksharedMessage =
            "This model is not workshared, so links can't be put on a workset here. " +
            "Turn on worksharing first (Collaborate tab > Worksets) - this tool will not do that for you.";
        private readonly Document _doc;
        private readonly ObservableCollection<LinkWorksetItem> _linkItems;

        public SetLinkWorksetWindow(Document doc)
        {
            InitializeComponent();

            // Shared AJ Tools window entrance (fade + short rise). Cosmetic only.
            WindowMotionHelper.AttachStandardEntrance(this);

            // Shared AJ Tools window exit (fade + short sink). The window's result,
            // validation and close behaviour are unchanged - see WindowMotionHelper's header.
            WindowMotionHelper.AttachStandardExit(this);

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

            // Worksets only exist in a workshared model. Turning worksharing on is a decision for the
            // whole model (it also clears the Undo history), so this tool never does it - it says so
            // up front instead of failing part way through.
            if (!_doc.IsWorkshared)
            {
                assign_workset_button.IsEnabled = false;
                ShowStatus(NotWorksharedMessage, isProblem: true);
            }

            Loaded += (s, e) => workset_combobox.Focus();
        }

        private void ShowStatus(string message, bool isProblem)
        {
            StatusText.Text = message ?? string.Empty;
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, isProblem ? "AccentDanger" : "TextSecondary");
            StatusText.Visibility = string.IsNullOrEmpty(StatusText.Text)
                ? System.Windows.Visibility.Collapsed
                : System.Windows.Visibility.Visible;
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
            // The button is disabled for a non-workshared model; re-checked here as a guard.
            if (!_doc.IsWorkshared)
            {
                ShowStatus(NotWorksharedMessage, isProblem: true);
                return;
            }

            string targetWorksetName = workset_combobox.Text ?? string.Empty;
            if (string.IsNullOrEmpty(targetWorksetName))
            {
                DialogHelper.ShowError(DialogTitle, "Please provide or select a name for the workset.");
                return;
            }

            List<LinkWorksetItem> linksToMove = _linkItems
                .Where(item => item.IsChecked)
                .ToList();

            if (linksToMove.Count == 0)
            {
                DialogHelper.ShowError(DialogTitle, "No links were selected to be moved.");
                return;
            }

            var moved = new List<string>();
            var notMoved = new List<string>();
            int alreadyThere = 0;

            try
            {
                using (var t = new Transaction(_doc, "Assign Links to Workset"))
                {
                    t.Start();

                    Workset targetWorkset = FindOrCreateWorkset(_doc, targetWorksetName);

                    // The workset parameter expects an integer Workset id value.
                    // WorksetId.IntegerValue is unrelated to ElementId and is valid in all versions.
                    int targetId = targetWorkset.Id.IntegerValue;

                    foreach (LinkWorksetItem item in linksToMove)
                    {
                        Parameter worksetParam = item.Element.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                        if (worksetParam == null || worksetParam.IsReadOnly)
                        {
                            notMoved.Add(item.Name);
                            continue;
                        }

                        if (worksetParam.AsInteger() == targetId)
                        {
                            alreadyThere++;
                            continue;
                        }

                        if (worksetParam.Set(targetId))
                            moved.Add(item.Name);
                        else
                            notMoved.Add(item.Name);
                    }

                    // A rolled-back commit (e.g. a link owned by another user) moved nothing.
                    if (t.Commit() != TransactionStatus.Committed)
                    {
                        ShowStatus("Revit did not keep the change, so no link was moved.", isProblem: true);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(DialogTitle, "Failed to assign workset:\n\n" + ex.Message);
                return;
            }

            bool saved = LinkWorksetSettings.SaveLastWorksetName(targetWorksetName, out string saveError);

            // The result goes in the status line - no success popup. The window stays open so the
            // line can be read; Close when done.
            string status = $"Moved {moved.Count} link(s) to the \"{targetWorksetName}\" workset.";
            if (alreadyThere > 0)
                status += $" {alreadyThere} were already on it.";
            if (notMoved.Count > 0)
                status += " Not moved - their workset can't be changed: " + JoinNames(notMoved) + ".";
            if (!saved)
                status += " The workset name could not be remembered for next time: " + saveError;

            ShowStatus(status, isProblem: notMoved.Count > 0 || !saved);
        }

        private static string JoinNames(IList<string> names)
        {
            const int MaxShown = 5;
            string shown = string.Join(", ", names.Take(MaxShown));
            return names.Count > MaxShown
                ? shown + $" and {names.Count - MaxShown} more"
                : shown;
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
