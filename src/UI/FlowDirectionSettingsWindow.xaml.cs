// Tool Name: Flow Direction Annotations - Settings
// Description: Dialog for selecting the annotation family and spacing.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-21
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, System.Windows

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using AJTools.Models;

namespace AJTools.UI
{
    /// <summary>
    /// Interaction logic for FlowDirectionSettingsWindow.xaml
    /// </summary>
    public partial class FlowDirectionSettingsWindow : Window
    {
        private readonly DisplayUnitType _displayUnitType;

        public FamilySymbol SelectedSymbol { get; private set; }

        public double SpacingInternal { get; private set; }

        public FlowDirectionSettingsWindow(Document doc, FlowDirectionSettingsState initialState = null)
        {
            InitializeComponent();

            _displayUnitType = ResolveLengthDisplayUnit(doc);
            SpacingUnitText.Text = SafeUnitLabel(_displayUnitType);

            var symbols = CollectAnnotationSymbols(doc);
            var items = symbols.Select(symbol => new FamilySymbolItem(symbol)).ToList();
            FamilyCombo.ItemsSource = items;
            FamilyCombo.DisplayMemberPath = nameof(FamilySymbolItem.DisplayName);

            ApplyInitialState(items, initialState);
        }

        private void OnStart(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;

            var selectedItem = FamilyCombo.SelectedItem as FamilySymbolItem;
            if (selectedItem == null || selectedItem.Symbol == null)
            {
                ErrorText.Text = "Select an annotation family.";
                return;
            }

            if (!TryResolveSpacing(out double spacingInternal))
            {
                return;
            }

            SelectedSymbol = selectedItem.Symbol;
            SpacingInternal = spacingInternal;
            DialogResult = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private bool TryResolveSpacing(out double spacingInternal)
        {
            spacingInternal = 0;
            string text = SpacingBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                ErrorText.Text = "Enter a spacing value.";
                return false;
            }

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double spacingDisplay))
            {
                ErrorText.Text = "Spacing must be a number.";
                return false;
            }

            if (spacingDisplay <= 0)
            {
                ErrorText.Text = "Spacing must be greater than zero.";
                return false;
            }

            spacingInternal = UnitUtils.ConvertToInternalUnits(spacingDisplay, _displayUnitType);
            if (spacingInternal <= 1e-6)
            {
                ErrorText.Text = "Spacing is too small.";
                return false;
            }

            return true;
        }

        private static IList<FamilySymbol> CollectAnnotationSymbols(Document doc)
        {
            if (doc == null)
                return new List<FamilySymbol>();

            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(IsSupportedAnnotationSymbol)
                .OrderBy(symbol => symbol.FamilyName)
                .ThenBy(symbol => symbol.Name)
                .ToList();
        }

        private static bool IsSupportedAnnotationSymbol(FamilySymbol symbol)
        {
            if (symbol == null)
                return false;

            Category category = symbol.Category;
            if (category == null || category.CategoryType != CategoryType.Annotation)
                return false;

            if (category.IsTagCategory)
                return false;

            Family family = symbol.Family;
            if (family == null)
                return false;

            return family.FamilyPlacementType == FamilyPlacementType.ViewBased;
        }

        private static DisplayUnitType ResolveLengthDisplayUnit(Document doc)
        {
            if (doc == null)
                return DisplayUnitType.DUT_METERS;

            Units units = doc.GetUnits();
            FormatOptions options = units.GetFormatOptions(UnitType.UT_Length);
            return options.DisplayUnits;
        }

        private static string SafeUnitLabel(DisplayUnitType unitType)
        {
            try
            {
                return LabelUtils.GetLabelFor(unitType);
            }
            catch
            {
                return string.Empty;
            }
        }

        private void ApplyInitialState(IList<FamilySymbolItem> items, FlowDirectionSettingsState initialState)
        {
            if (items.Count == 0)
            {
                ErrorText.Text = "No view-based annotation families were found in this project.";
                return;
            }

            FamilyCombo.SelectedIndex = 0;

            if (initialState?.SymbolId != null)
            {
                var match = items.FirstOrDefault(item => item.Symbol?.Id == initialState.SymbolId);
                if (match != null)
                {
                    FamilyCombo.SelectedItem = match;
                }
            }

            if (initialState != null && initialState.SpacingInternal > 1e-6)
            {
                double spacingDisplay = UnitUtils.ConvertFromInternalUnits(initialState.SpacingInternal, _displayUnitType);
                SpacingBox.Text = spacingDisplay.ToString("0.###", CultureInfo.CurrentCulture);
            }
            else
            {
                SpacingBox.Text = "1";
            }
        }

        private sealed class FamilySymbolItem
        {
            public FamilySymbolItem(FamilySymbol symbol)
            {
                Symbol = symbol;
                DisplayName = symbol == null
                    ? string.Empty
                    : $"{symbol.FamilyName} : {symbol.Name}";
            }

            public FamilySymbol Symbol { get; }

            public string DisplayName { get; }
        }
    }
}
