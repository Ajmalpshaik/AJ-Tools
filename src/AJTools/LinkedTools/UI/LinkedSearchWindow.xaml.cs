// Tool Name: Linked Element Search UI
// Description: WPF dialog to search by Element ID across host and linked models and zoom to the result.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, System.Windows
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.LinkedTools.UI
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
        private static readonly System.Random _random = new System.Random();

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
                    _linkItems.Add(new LinkDisplayItem(link.Name, link, link.GetLinkDocument(), isHost: false));
                }
            }

            LinkSelector.ItemsSource = _linkItems;
            if (_linkItems.Count > 0)
            {
                LinkSelector.SelectedIndex = 0;
            }

            OnSearchAllChanged(this, null);
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
                var targetItem = LinkSelector.SelectedItem as LinkDisplayItem;
                if (targetItem == null)
                {
                    ErrorText.Text = "Select a model to search.";
                    return;
                }

                found = targetItem.IsHost
                    ? TryFindInHost(parsedId)
                    : TryFindInLink(parsedId, targetItem);
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
                Color projColor = RandomColor();
                Color fillColor = RandomColor();
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

            LinkSelector.IsEnabled = SearchAllCheck.IsChecked != true;
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
                    Transform linkTransform = item.Instance.GetTransform();
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

        private void ClearMessages()
        {
            ErrorText.Text = string.Empty;
            // clear status
        }

        private static Color RandomColor()
        {
            byte r = (byte)_random.Next(30, 256);
            byte g = (byte)_random.Next(30, 256);
            byte b = (byte)_random.Next(30, 256);
            return new Color(r, g, b);
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
