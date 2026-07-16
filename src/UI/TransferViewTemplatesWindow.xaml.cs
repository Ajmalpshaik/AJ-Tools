// Tool Name: Transfer View Templates UI
// Description: Code-behind for selecting source/target projects and view templates to transfer.
// Author: Ajmal P.S.
// Version: 1.1.0
// Last Updated: 2026-07-13
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, System.Windows

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Autodesk.Revit.DB;

namespace AJTools.UI
{
    public partial class TransferViewTemplatesWindow : Window
    {
        // In-memory only, cleared when Revit closes - same convention as FilterProStateTracker.
        // Remembered by Document.Title (not a Document reference, which doesn't survive re-opening
        // the tool with a different set of open documents) only after a successful Transfer, so an
        // accidental browse-around selection never overwrites what was last actually used.
        private static string _lastSourceDocTitle;
        private static string _lastTargetDocTitle;

        private readonly ObservableCollection<DocumentOption> _documents = new ObservableCollection<DocumentOption>();
        private readonly ObservableCollection<ViewTemplateItem> _allTemplates = new ObservableCollection<ViewTemplateItem>();
        private readonly ObservableCollection<ViewTemplateItem> _filteredTemplates = new ObservableCollection<ViewTemplateItem>();

        public Document SourceDocument { get; private set; }
        public Document TargetDocument { get; private set; }
        public IReadOnlyList<ElementId> SelectedTemplateIds { get; private set; } = Array.Empty<ElementId>();
        public IReadOnlyList<string> SelectedTemplateNames { get; private set; } = Array.Empty<string>();
        public bool OverrideExisting => OverrideCheckBox.IsChecked == true;

        public TransferViewTemplatesWindow(IList<Document> projectDocuments)
        {
            InitializeComponent();

            if (projectDocuments == null || projectDocuments.Count == 0)
            {
                throw new ArgumentException("No project documents were provided.", nameof(projectDocuments));
            }

            foreach (DocumentOption option in BuildDocumentOptions(projectDocuments))
            {
                _documents.Add(option);
            }

            SourceDocCombo.ItemsSource = _documents;
            TargetDocCombo.ItemsSource = _documents;
            TemplatesListBox.ItemsSource = _filteredTemplates;

            WireEvents();

            RestoreLastDocumentSelection();

            SourceDocument = (SourceDocCombo.SelectedItem as DocumentOption)?.Document;
            TargetDocument = (TargetDocCombo.SelectedItem as DocumentOption)?.Document;
            RefreshTemplateList();
            UpdateUiState();
        }

        private void WireEvents()
        {
            SourceDocCombo.SelectionChanged += (s, e) => OnDocumentSelectionChanged();
            TargetDocCombo.SelectionChanged += (s, e) => OnDocumentSelectionChanged();
            FilterTextBox.TextChanged += (s, e) => ApplyTemplateFilter();

            SelectAllButton.Click += (s, e) => SetFilteredSelectionState(true);
            SelectNoneButton.Click += (s, e) => SetFilteredSelectionState(false);
            TransferButton.Click += OnTransferClick;
            CancelButton.Click += (s, e) =>
            {
                DialogResult = false;
                Close();
            };
        }

        private void RestoreLastDocumentSelection()
        {
            DocumentOption source = FindDocumentOptionByTitle(_lastSourceDocTitle);
            DocumentOption target = FindDocumentOptionByTitle(_lastTargetDocTitle);

            if (source != null)
            {
                SourceDocCombo.SelectedItem = source;
            }
            else if (_documents.Count > 0)
            {
                SourceDocCombo.SelectedIndex = 0;
            }

            if (target != null && target != SourceDocCombo.SelectedItem)
            {
                TargetDocCombo.SelectedItem = target;
                return;
            }

            if (_documents.Count > 1)
            {
                TargetDocCombo.SelectedIndex = SourceDocCombo.SelectedIndex == 0 ? 1 : 0;
            }
            else if (_documents.Count > 0)
            {
                TargetDocCombo.SelectedIndex = 0;
            }
        }

        private DocumentOption FindDocumentOptionByTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            return _documents.FirstOrDefault(option =>
                string.Equals(option.Document?.Title, title, StringComparison.OrdinalIgnoreCase));
        }

        private void OnDocumentSelectionChanged()
        {
            SourceDocument = (SourceDocCombo.SelectedItem as DocumentOption)?.Document;
            TargetDocument = (TargetDocCombo.SelectedItem as DocumentOption)?.Document;

            RefreshTemplateList();
            UpdateUiState();
        }

        private void RefreshTemplateList()
        {
            foreach (ViewTemplateItem item in _allTemplates)
            {
                item.PropertyChanged -= OnTemplateItemPropertyChanged;
            }

            _allTemplates.Clear();

            if (SourceDocument != null)
            {
                List<View> templates = new FilteredElementCollector(SourceDocument)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => v != null && v.IsTemplate)
                    .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (View template in templates)
                {
                    var item = new ViewTemplateItem(template);
                    item.PropertyChanged += OnTemplateItemPropertyChanged;
                    _allTemplates.Add(item);
                }
            }

            ApplyTemplateFilter();
        }

        private void OnTemplateItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewTemplateItem.IsChecked))
            {
                UpdateUiState();
            }
        }

        private void ApplyTemplateFilter()
        {
            string filter = (FilterTextBox.Text ?? string.Empty).Trim();
            IEnumerable<ViewTemplateItem> candidates = _allTemplates;

            if (!string.IsNullOrWhiteSpace(filter))
            {
                candidates = candidates.Where(item => item.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            _filteredTemplates.Clear();
            foreach (ViewTemplateItem item in candidates)
            {
                _filteredTemplates.Add(item);
            }

            UpdateUiState();
        }

        private void SetFilteredSelectionState(bool isChecked)
        {
            foreach (ViewTemplateItem item in _filteredTemplates)
            {
                item.IsChecked = isChecked;
            }

            UpdateUiState();
        }

        private void OnTransferClick(object sender, RoutedEventArgs e)
        {
            SourceDocument = (SourceDocCombo.SelectedItem as DocumentOption)?.Document;
            TargetDocument = (TargetDocCombo.SelectedItem as DocumentOption)?.Document;

            if (SourceDocument == null || TargetDocument == null)
            {
                StatusText.Text = "Select both source and target projects.";
                return;
            }

            if (SourceDocument.Equals(TargetDocument))
            {
                StatusText.Text = "Source and target projects must be different.";
                return;
            }

            List<ViewTemplateItem> selected = _allTemplates.Where(item => item.IsChecked).ToList();
            if (selected.Count == 0)
            {
                StatusText.Text = "Select at least one view template to transfer.";
                return;
            }

            SelectedTemplateIds = selected.Select(item => item.TemplateId).ToList();
            SelectedTemplateNames = selected.Select(item => item.Name).ToList();

            _lastSourceDocTitle = SourceDocument.Title;
            _lastTargetDocTitle = TargetDocument.Title;

            DialogResult = true;
            Close();
        }

        private void UpdateUiState()
        {
            bool hasDocs = SourceDocument != null && TargetDocument != null;
            bool sameDoc = hasDocs && SourceDocument.Equals(TargetDocument);
            int selectedCount = _allTemplates.Count(item => item.IsChecked);
            int totalCount = _allTemplates.Count;

            CountText.Text = $"{selectedCount}/{totalCount}";

            bool canSelect = hasDocs && !sameDoc && _filteredTemplates.Count > 0;
            SelectAllButton.IsEnabled = canSelect;
            SelectNoneButton.IsEnabled = canSelect;
            TransferButton.IsEnabled = hasDocs && !sameDoc && selectedCount > 0;

            if (!hasDocs)
            {
                StatusText.Text = "Select source and target projects.";
                return;
            }

            if (sameDoc)
            {
                StatusText.Text = "Source and target projects must be different.";
                return;
            }

            if (totalCount == 0)
            {
                StatusText.Text = "No view templates found in source project.";
                return;
            }

            StatusText.Text = $"{selectedCount} template(s) selected.";
        }

        private static IEnumerable<DocumentOption> BuildDocumentOptions(IEnumerable<Document> documents)
        {
            var sortedDocs = documents
                .Where(doc => doc != null)
                .OrderBy(doc => doc.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var titleCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (Document doc in sortedDocs)
            {
                string title = string.IsNullOrWhiteSpace(doc.Title) ? "Untitled Project" : doc.Title;
                if (!titleCount.ContainsKey(title))
                {
                    titleCount[title] = 0;
                }

                titleCount[title]++;
            }

            var runningIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (Document doc in sortedDocs)
            {
                string title = string.IsNullOrWhiteSpace(doc.Title) ? "Untitled Project" : doc.Title;
                if (!runningIndex.ContainsKey(title))
                {
                    runningIndex[title] = 0;
                }

                runningIndex[title]++;
                int total = titleCount[title];
                int index = runningIndex[title];

                string display = title;
                if (total > 1)
                {
                    display = $"{title} ({index}/{total})";
                }

                if (doc.IsReadOnly)
                {
                    display += " [Read-only]";
                }

                yield return new DocumentOption(doc, display);
            }
        }

        private sealed class DocumentOption
        {
            public DocumentOption(Document document, string displayName)
            {
                Document = document;
                DisplayName = displayName ?? string.Empty;
            }

            public Document Document { get; }
            public string DisplayName { get; }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private sealed class ViewTemplateItem : INotifyPropertyChanged
        {
            private bool _isChecked;

            public ViewTemplateItem(View viewTemplate)
            {
                if (viewTemplate == null)
                {
                    throw new ArgumentNullException(nameof(viewTemplate));
                }

                Name = viewTemplate.Name;
                TemplateId = viewTemplate.Id;
            }

            public string Name { get; }
            public ElementId TemplateId { get; }

            public bool IsChecked
            {
                get => _isChecked;
                set
                {
                    if (_isChecked == value)
                    {
                        return;
                    }

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
}
