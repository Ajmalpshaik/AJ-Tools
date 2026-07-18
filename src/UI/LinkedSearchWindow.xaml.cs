// Tool Name: Linked Element Search UI
// Description: WPF dialog to search by Element ID across host and linked models and zoom to the result.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
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

        private ElementId _lastHostElementId = ElementId.InvalidElementId;
        private RevitLinkInstance _lastLinkInstance;
        private ElementId _lastLinkedElementId = ElementId.InvalidElementId;
        private ElementId _lastOverrideTarget = ElementId.InvalidElementId;

        public LinkedSearchWindow(UIDocument uiDoc, Document hostDoc, View activeView, IEnumerable<RevitLinkInstance> links)
        {
            InitializeComponent();

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
            _lastOverrideTarget = ElementId.InvalidElementId;
            _lastHostElementId = ElementId.InvalidElementId;
            _lastLinkedElementId = ElementId.InvalidElementId;
            _lastLinkInstance = null;
            

            if (!int.TryParse(ElementIdBox.Text?.Trim(), out int parsedId) || parsedId <= 0)
            {
                ErrorText.Text = "Enter a valid Element ID.";
                ElementIdBox.Focus();
                ElementIdBox.SelectAll();
                return;
            }

            bool found = false;

            bool searchAll = SearchAllCheck?.IsChecked == true;

            if (searchAll)
            {
                found = TryFindInHost(parsedId);
                if (!found)
                {
                    foreach (var item in _linkItems.Where(i => !i.IsHost))
                    {
                        found = TryFindInLink(parsedId, item);
                        if (found)
                            break;
                    }
                }
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
                foreach (var item in _linkItems.Where(i => selectedSet.Contains(i)))
                {
                    found = item.IsHost
                        ? TryFindInHost(parsedId)
                        : TryFindInLink(parsedId, item);

                    if (found)
                        break;
                }
            }

            if (!found)
            {
                ErrorText.Text = "Element ID not found in the chosen model(s).";
            }
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
        private bool TryFindInHost(int elementId)
        {
            ElementId id = ElementIdHelper.FromInt(elementId);
            Element element = _hostDoc.GetElement(id);
            if (element == null)
                return false;

            try
            {
                _uiDoc.Selection.SetElementIds(new List<ElementId> { id });
                _uiDoc.ShowElements(id);

                _lastHostElementId = id;
                _lastLinkedElementId = ElementId.InvalidElementId;
                _lastLinkInstance = null;
                _lastOverrideTarget = id;
                

                // Found in current model
                return true;
            }
            catch (System.Exception ex)
            {
                ErrorText.Text = "Could not select/zoom: " + ex.Message;
                return false;
            }
        }

        private bool TryFindInLink(int elementId, LinkDisplayItem item)
        {
            if (item?.Instance == null || item.LinkDocument == null)
                return false;

            ElementId id = ElementIdHelper.FromInt(elementId);
            Element element = item.LinkDocument.GetElement(id);
            if (element == null)
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

                _lastHostElementId = ElementId.InvalidElementId;
                _lastLinkedElementId = id;
                _lastLinkInstance = item.Instance;
                _lastOverrideTarget = item.Instance.Id; // override at link instance level
                

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
            // clear status
        }

    }
}
