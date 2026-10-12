// Tool Name: Linked Element Search UI
// Description: WPF dialog to search by Element ID across host and linked models and zoom to the result.
// Author: Ajmal P.S.
// Version: 1.0.1
// Last Updated: 2026-10-12
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, System.Windows
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AJTools.Models;
using AJTools.Utils;
using WpfControl = System.Windows.Controls.Control;
using RevitTransform = Autodesk.Revit.DB.Transform;

namespace AJTools.UI
{
    public partial class LinkedSearchWindow : Window
    {
        private readonly UIDocument _uiDoc;
        private readonly Document _hostDoc;
        private readonly View _activeView;
        private readonly IList<LinkDisplayItem> _linkItems;
        private readonly string _defaultInfoText;

        // True while the match list is being filled or cleared in code, so that is not taken as a pick.
        private bool _fillingMatches;

        public LinkedSearchWindow(UIDocument uiDoc, Document hostDoc, View activeView, IEnumerable<RevitLinkInstance> links)
        {
            InitializeComponent();
            _defaultInfoText = InfoText.Text;

            // Shared AJ Tools window entrance (fade + short rise). Cosmetic only.
            WindowMotionHelper.AttachStandardEntrance(this);

            // Shared AJ Tools window exit (fade + short sink). The window's result,
            // validation and close behaviour are unchanged - see WindowMotionHelper's header.
            WindowMotionHelper.AttachStandardExit(this);

            _uiDoc = uiDoc;
            _hostDoc = hostDoc;
            _activeView = activeView;

            _linkItems = new List<LinkDisplayItem>
            {
                new LinkDisplayItem("Current Model", null, hostDoc, isHost: true)
            };

            if (links != null)
            {
                foreach (var link in links.Where(l => l != null && l.GetLinkDocument() != null))
                {
                    Document linkDoc = link.GetLinkDocument();
                    _linkItems.Add(new LinkDisplayItem(GetCleanLinkName(link, linkDoc), link, linkDoc, isHost: false));
                }
            }

            LinkSelector.ItemsSource = _linkItems;
            if (_linkItems.Count > 0)
            {
                LinkSelector.SelectedIndex = 0;
            }

            OnSearchAllChanged(this, null);
            UpdateModelSummary();
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            ApplyModelListSizing();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Preserve user-resizable behavior while ensuring content has a safe baseline width.
                if (Width < MinWidth)
                {
                    Width = MinWidth;
                }
            }), DispatcherPriority.Background);
        }

        private void OnSearch(object sender, RoutedEventArgs e)
        {
            ClearMessages();

            if (!int.TryParse(ElementIdBox.Text?.Trim(), out int parsedId) || parsedId <= 0)
            {
                ErrorText.Text = "Enter a valid Element ID.";
                ElementIdBox.Focus();
                ElementIdBox.SelectAll();
                return;
            }

            List<LinkDisplayItem> modelsToSearch;
            bool searchAll = SearchAllCheck?.IsChecked == true;

            if (searchAll)
            {
                modelsToSearch = _linkItems.ToList();
            }
            else
            {
                var selectedItems = LinkSelector.SelectedItems
                    .Cast<LinkDisplayItem>()
                    .ToList();

                if (selectedItems.Count == 0)
                {
                    ErrorText.Text = "Select at least one model to search.";
                    return;
                }

                var selectedSet = new HashSet<LinkDisplayItem>(selectedItems);
                modelsToSearch = _linkItems.Where(i => selectedSet.Contains(i)).ToList();
            }

            // Element IDs are only unique inside ONE model - the host and every link number their own
            // elements, so the same ID often exists in several of them. Every chosen model is searched
            // instead of stopping at the first hit, so a linked element's ID can't silently land on an
            // unrelated host element.
            List<SearchMatch> matches = FindMatches(parsedId, modelsToSearch);

            if (matches.Count == 0)
            {
                ErrorText.Text = "Element ID not found in the chosen model(s).";
                return;
            }

            if (matches.Count == 1)
            {
                if (ShowMatch(matches[0]))
                {
                    InfoText.Text = "Found in: " + matches[0].Label;
                }

                return;
            }

            // Several models have this ID - nothing is selected until the user says which one they mean.
            InfoText.Text = string.Format(
                CultureInfo.CurrentCulture,
                "Element ID {0} exists in {1} models. Pick the one you mean:",
                parsedId,
                matches.Count);

            _fillingMatches = true;
            try
            {
                MatchList.ItemsSource = matches;
                MatchList.SelectedItem = null;
            }
            finally
            {
                _fillingMatches = false;
            }

            MatchList.Visibility = System.Windows.Visibility.Visible;
        }

        private void OnMatchSelected(object sender, SelectionChangedEventArgs e)
        {
            if (_fillingMatches)
                return;

            if (MatchList.SelectedItem is SearchMatch match)
            {
                ErrorText.Text = string.Empty;
                ShowMatch(match);
            }
        }

        private List<SearchMatch> FindMatches(int elementId, IEnumerable<LinkDisplayItem> models)
        {
            var matches = new List<SearchMatch>();
            ElementId id = ElementIdHelper.FromInt(elementId);

            foreach (LinkDisplayItem model in models)
            {
                Document doc;
                if (model.IsHost)
                {
                    doc = _hostDoc;
                }
                else
                {
                    if (model.Instance == null || model.LinkDocument == null)
                        continue;

                    doc = model.LinkDocument;
                }

                Element element = doc?.GetElement(id);
                if (element != null)
                {
                    matches.Add(new SearchMatch(model, element));
                }
            }

            return matches;
        }

        private bool ShowMatch(SearchMatch match)
        {
            return match.Model.IsHost
                ? ShowHostElement(match.Element.Id)
                : ShowLinkedElement(match.Element, match.Model);
        }

        // Cancel button handler from XAML
        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnSearchAllChanged(object sender, RoutedEventArgs e)
        {
            if (LinkSelector == null || SearchAllCheck == null)
                return;

            bool enableSelection = SearchAllCheck.IsChecked != true;
            LinkSelector.IsEnabled = enableSelection;
            if (ModelDropdownButton != null)
            {
                ModelDropdownButton.IsEnabled = enableSelection;
                if (!enableSelection)
                {
                    ModelDropdownButton.IsChecked = false;
                }
            }

            UpdateModelSummary();
        }

        private void ApplyModelListSizing()
        {
            if (LinkSelector == null || ElementIdBox == null)
                return;

            double maxNameWidth = 0;
            if (_linkItems != null && _linkItems.Count > 0)
            {
                maxNameWidth = _linkItems
                    .Select(item => MeasureTextWidth(item.DisplayName, LinkSelector))
                    .DefaultIfEmpty(0)
                    .Max();
            }

            double chrome = 28;
            double columnPadding = 24 + 90;
            double padding = LinkSelector.Padding.Left + LinkSelector.Padding.Right +
                             LinkSelector.BorderThickness.Left + LinkSelector.BorderThickness.Right;
            double targetWidth = maxNameWidth + chrome + columnPadding + padding + 10;

            double controlMinWidth = Math.Max(320, Math.Min(targetWidth, 560));
            ElementIdBox.MinWidth = controlMinWidth;
            LinkSelector.MinWidth = controlMinWidth;
            if (ModelDropdownButton != null)
            {
                ModelDropdownButton.MinWidth = controlMinWidth;
            }
        }

        private void OnModelSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateModelSummary();
        }

        private void OnModelDropdownClosed(object sender, EventArgs e)
        {
            if (ModelDropdownButton != null)
            {
                ModelDropdownButton.IsChecked = false;
            }
        }

        private void UpdateModelSummary()
        {
            if (ModelSummaryText == null || SearchAllCheck == null || LinkSelector == null)
                return;

            if (SearchAllCheck.IsChecked == true)
            {
                ModelSummaryText.Text = "All models (host + links)";
                return;
            }

            var selectedItems = LinkSelector.SelectedItems.Cast<LinkDisplayItem>().ToList();
            if (selectedItems.Count == 0)
            {
                ModelSummaryText.Text = "Select model(s)";
                return;
            }

            if (selectedItems.Count == 1)
            {
                ModelSummaryText.Text = selectedItems[0].DisplayName;
                return;
            }

            string summary = string.Join(", ", selectedItems.Take(2).Select(i => i.DisplayName));
            if (selectedItems.Count > 2)
            {
                summary += $" +{selectedItems.Count - 2} more";
            }

            ModelSummaryText.Text = summary;
        }

        private double MeasureTextWidth(string text, WpfControl reference)
        {
            string value = text ?? string.Empty;
            var typeface = new Typeface(reference.FontFamily, reference.FontStyle, reference.FontWeight, reference.FontStretch);
            var dpi = VisualTreeHelper.GetDpi(this);

            var formatted = new System.Windows.Media.FormattedText(
                value,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                reference.FontSize,
                Brushes.Black,
                dpi.PixelsPerDip);

            return formatted.Width;
        }
        private bool ShowHostElement(ElementId id)
        {
            try
            {
                _uiDoc.Selection.SetElementIds(new List<ElementId> { id });
                _uiDoc.ShowElements(id);

                // Found in current model
                return true;
            }
            catch (System.Exception ex)
            {
                ErrorText.Text = "Could not select/zoom: " + ex.Message;
                return false;
            }
        }

        private bool ShowLinkedElement(Element element, LinkDisplayItem item)
        {
            if (element == null || item?.Instance == null || item.LinkDocument == null)
                return false;

            try
            {
                // Compute transformed bounding box of the linked element and zoom
                BoundingBoxXYZ bbInLink = element.get_BoundingBox(null);
                if (bbInLink != null)
                {
                    RevitTransform linkTransform = item.Instance.GetTransform();
                    XYZ min = linkTransform.OfPoint(bbInLink.Min);
                    XYZ max = linkTransform.OfPoint(bbInLink.Max);

                    IList<UIView> uiviews = _uiDoc.GetOpenUIViews();
                    UIView targetUiView = uiviews.FirstOrDefault(v => v.ViewId == _activeView.Id);
                    if (targetUiView != null)
                    {
                        targetUiView.ZoomAndCenterRectangle(min, max);
                    }
                    else
                    {
                        // Fallback: show the link instance
                        _uiDoc.ShowElements(new List<ElementId> { item.Instance.Id });
                    }
                }
                else
                {
                    // Fallback: select and show the link instance if no bbox
                    _uiDoc.Selection.SetElementIds(new List<ElementId> { item.Instance.Id });
                    _uiDoc.ShowElements(new List<ElementId> { item.Instance.Id });
                }

                // Found in link
                return true;
            }
            catch (System.Exception ex)
            {
                ErrorText.Text = "Could not select/zoom: " + ex.Message;
                return false;
            }
        }

        private static string GetCleanLinkName(RevitLinkInstance linkInstance, Document linkDoc)
        {
            string name = (linkDoc != null && !string.IsNullOrWhiteSpace(linkDoc.Title))
                ? linkDoc.Title
                : (linkInstance?.Name ?? "Linked Model");

            int colonIndex = name.IndexOf(':');
            if (colonIndex > -1)
            {
                name = name.Substring(0, colonIndex).Trim();
            }

            return name;
        }

        private void ClearMessages()
        {
            ErrorText.Text = string.Empty;
            InfoText.Text = _defaultInfoText;

            _fillingMatches = true;
            try
            {
                MatchList.ItemsSource = null;
            }
            finally
            {
                _fillingMatches = false;
            }

            MatchList.Visibility = System.Windows.Visibility.Collapsed;
        }

        /// <summary>One model that contains the searched Element ID, as listed for the user to pick.</summary>
        private sealed class SearchMatch
        {
            public SearchMatch(LinkDisplayItem model, Element element)
            {
                Model = model;
                Element = element;
                Label = BuildLabel(model, element);
            }

            public LinkDisplayItem Model { get; }

            public Element Element { get; }

            public string Label { get; }

            public override string ToString() => Label;

            private static string BuildLabel(LinkDisplayItem model, Element element)
            {
                string category = "No category";
                string name = string.Empty;

                try { category = element.Category?.Name ?? category; } catch (System.Exception) { }
                try { name = element.Name ?? string.Empty; } catch (System.Exception) { }

                string label = model.DisplayName + " - " + category;
                return string.IsNullOrWhiteSpace(name) ? label : label + ": " + name;
            }
        }
    }
}
