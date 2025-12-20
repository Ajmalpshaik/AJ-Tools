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
using RevitColor = Autodesk.Revit.DB.Color;
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
        // Thread-safe Random instance
        private static readonly System.Threading.ThreadLocal<System.Random> _random = 
            new System.Threading.ThreadLocal<System.Random>(() => new System.Random(System.Guid.NewGuid().GetHashCode()));

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
            // Auto-size once to content, then allow manual resizing.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SizeToContent = SizeToContent.Manual;
                Width = ActualWidth;
                Height = ActualHeight;
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

        private void OnIdentify(object sender, RoutedEventArgs e)
        {
            ClearMessages();

            if (_activeView == null || _activeView.IsTemplate)
            {
                ErrorText.Text = "Active view is not valid for overrides.";
                return;
            }

            if (_lastOverrideTarget == ElementId.InvalidElementId)
            {
                ErrorText.Text = "Search for an element first.";
                return;
            }

            ExecuteInTransaction("AJTools - Identify Element", () =>
            {
                OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                RevitColor projColor = RandomColor();
                RevitColor fillColor = RandomColor();
                ogs.SetProjectionLineColor(projColor);
                ogs.SetCutLineColor(projColor);
                ogs.SetSurfaceForegroundPatternColor(fillColor);
                ElementId solidFill = GetSolidFillPatternId();
                if (solidFill != ElementId.InvalidElementId)
                {
                    ogs.SetSurfaceForegroundPatternId(solidFill);
                }
                ogs.SetSurfaceTransparency(0);
                ogs.SetProjectionLineWeight(8);
                ogs.SetCutLineWeight(8);
                ogs.SetHalftone(false);
                _activeView.SetElementOverrides(_lastOverrideTarget, ogs);
            });
        }

        private void OnReset(object sender, RoutedEventArgs e)
        {
            ClearMessages();

            if (_lastOverrideTarget == ElementId.InvalidElementId)
            {
                ErrorText.Text = "Nothing to reset.";
                return;
            }

            if (_activeView == null || _activeView.IsTemplate)
            {
                ErrorText.Text = "Active view is not valid for overrides.";
                return;
            }

            ExecuteInTransaction("AJTools - Reset Overrides", () =>
            {
                _activeView.SetElementOverrides(_lastOverrideTarget, new OverrideGraphicSettings());
            });
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
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

            double minWidth = Math.Max(260, Math.Min(targetWidth, 560));
            ElementIdBox.MinWidth = minWidth;
            LinkSelector.MinWidth = minWidth;
            if (ModelDropdownButton != null)
            {
                ModelDropdownButton.MinWidth = minWidth;
            }
            if (InfoText != null)
            {
                InfoText.MaxWidth = minWidth;
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
            ElementId id = new ElementId(elementId);
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

            ElementId id = new ElementId(elementId);
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

        private static RevitColor RandomColor()
        {
            byte r = (byte)_random.Value.Next(30, 256);
            byte g = (byte)_random.Value.Next(30, 256);
            byte b = (byte)_random.Value.Next(30, 256);
            return new RevitColor(r, g, b);
        }

        private ElementId GetSolidFillPatternId()
        {
            try
            {
                FillPatternElement solid = new FilteredElementCollector(_hostDoc)
                    .OfClass(typeof(FillPatternElement))
                    .Cast<FillPatternElement>()
                    .FirstOrDefault(f => f.GetFillPattern().IsSolidFill);

                return solid != null ? solid.Id : ElementId.InvalidElementId;
            }
            catch
            {
                // Ignore errors if solid fill pattern cannot be found.
                return ElementId.InvalidElementId;
            }
        }

        private void ExecuteInTransaction(string transactionName, Action action)
        {
            try
            {
                using (Transaction t = new Transaction(_hostDoc, transactionName))
                {
                    t.Start();
                    action();
                    t.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ErrorText.Text = "Operation failed: " + ex.Message;
            }
        }


    }
}
