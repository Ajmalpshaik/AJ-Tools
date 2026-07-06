#region Metadata
/*
 * Tool Name     : Opening Settings
 * File Name     : MepOpeningSettingsWindow.xaml.cs
 * Purpose       : Provides the WPF settings window for opening source, host, and element rules.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-03
 * Last Updated  : 2026-07-03
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in WPF UI
 *
 * Dependencies  : AJTools.Models.MepOpenings, AJTools.Services.MepOpenings
 *
 * Input         : Active project family symbols plus user-edited source, host, shape, buffer, family, insulation, and merge distance settings.
 * Output        : Saved opening settings in user AppData.
 *
 * Notes         :
 * - This settings window does not read or write Revit model elements.
 * - Direct wall openings are rectangular in the Revit API; circle requests use their bounding rectangle on walls.
 *
 * Changelog     :
 * v1.0.0 (2026-07-03) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using AJTools.Models.MepOpenings;
using AJTools.Services.MepOpenings;

namespace AJTools.UI.MepOpenings
{
    public partial class MepOpeningSettingsWindow : Window, INotifyPropertyChanged
    {
        private string _mergeDistanceText;
        private bool _includeInsulation;
        private MepOpeningCreationMode _creationMode;
        private MepOpeningSelectionMethod _selectionMethod;
        private bool _useCurrentModelSources;
        private bool _useLinkedModelSources;
        private string _selectedSourceLinkUniqueId;
        private bool _useCurrentModelHosts;
        private bool _useLinkedModelHosts;
        private string _selectedHostLinkUniqueId;

        public MepOpeningSettingsWindow()
            : this(null)
        {
        }

        public MepOpeningSettingsWindow(Document doc)
        {
            InitializeComponent();
            OpeningModeChoices = new ObservableCollection<OpeningModeChoice>(CreateOpeningModeChoices());
            OpeningSelectionMethodChoices = new ObservableCollection<OpeningSelectionMethodChoice>(CreateOpeningSelectionMethodChoices());
            OpeningLinkChoices = new ObservableCollection<OpeningLinkChoice>(CollectOpeningLinkChoices(doc));
            OpeningFamilyChoices = new ObservableCollection<string>(CollectOpeningFamilyChoices(doc));
            LoadSettings();
            DataContext = this;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<MepOpeningElementRule> Rules { get; private set; }

        public ObservableCollection<string> OpeningFamilyChoices { get; private set; }

        public ObservableCollection<OpeningModeChoice> OpeningModeChoices { get; private set; }

        public ObservableCollection<OpeningSelectionMethodChoice> OpeningSelectionMethodChoices { get; private set; }

        public ObservableCollection<OpeningLinkChoice> OpeningLinkChoices { get; private set; }

        public bool IsOpeningFamilyEnabled
        {
            get
            {
                return CreationMode == MepOpeningCreationMode.FamilyOpening ||
                       UseLinkedModelHosts;
            }
        }

        public bool IsShapeSelectionEnabled
        {
            get
            {
                return CreationMode == MepOpeningCreationMode.DirectOpening &&
                       !UseLinkedModelHosts;
            }
        }

        public bool IsSourceLinkPickerEnabled
        {
            get { return UseLinkedModelSources && OpeningLinkChoices != null && OpeningLinkChoices.Count > 1; }
        }

        public bool IsHostLinkPickerEnabled
        {
            get { return UseLinkedModelHosts && OpeningLinkChoices != null && OpeningLinkChoices.Count > 1; }
        }

        public string MergeDistanceText
        {
            get { return _mergeDistanceText; }
            set
            {
                if (_mergeDistanceText == value)
                {
                    return;
                }

                _mergeDistanceText = value;
                OnPropertyChanged(nameof(MergeDistanceText));
            }
        }

        public bool IncludeInsulation
        {
            get { return _includeInsulation; }
            set
            {
                if (_includeInsulation == value)
                {
                    return;
                }

                _includeInsulation = value;
                OnPropertyChanged(nameof(IncludeInsulation));
            }
        }

        public MepOpeningCreationMode CreationMode
        {
            get { return _creationMode; }
            set
            {
                if (UseLinkedModelHosts && value == MepOpeningCreationMode.DirectOpening)
                {
                    value = MepOpeningCreationMode.FamilyOpening;
                }

                if (_creationMode == value)
                {
                    return;
                }

                _creationMode = value;
                OnPropertyChanged(nameof(CreationMode));
                OnPropertyChanged(nameof(IsOpeningFamilyEnabled));
                OnPropertyChanged(nameof(IsShapeSelectionEnabled));
            }
        }

        public MepOpeningSelectionMethod SelectionMethod
        {
            get { return _selectionMethod; }
            set
            {
                if (_selectionMethod == value)
                {
                    return;
                }

                _selectionMethod = value;
                OnPropertyChanged(nameof(SelectionMethod));
            }
        }

        public bool UseCurrentModelSources
        {
            get { return _useCurrentModelSources; }
            set
            {
                if (_useCurrentModelSources == value)
                {
                    return;
                }

                _useCurrentModelSources = value;
                if (value && _useLinkedModelSources)
                {
                    _useLinkedModelSources = false;
                    OnPropertyChanged(nameof(UseLinkedModelSources));
                    OnPropertyChanged(nameof(IsSourceLinkPickerEnabled));
                }

                OnPropertyChanged(nameof(UseCurrentModelSources));
            }
        }

        public bool UseLinkedModelSources
        {
            get { return _useLinkedModelSources; }
            set
            {
                if (_useLinkedModelSources == value)
                {
                    return;
                }

                _useLinkedModelSources = value;
                if (value && _useCurrentModelSources)
                {
                    _useCurrentModelSources = false;
                    OnPropertyChanged(nameof(UseCurrentModelSources));
                }

                OnPropertyChanged(nameof(UseLinkedModelSources));
                OnPropertyChanged(nameof(IsSourceLinkPickerEnabled));
            }
        }

        public string SelectedSourceLinkUniqueId
        {
            get { return _selectedSourceLinkUniqueId; }
            set
            {
                if (_selectedSourceLinkUniqueId == value)
                {
                    return;
                }

                _selectedSourceLinkUniqueId = value;
                OnPropertyChanged(nameof(SelectedSourceLinkUniqueId));
            }
        }

        public bool UseCurrentModelHosts
        {
            get { return _useCurrentModelHosts; }
            set
            {
                if (_useCurrentModelHosts == value)
                {
                    return;
                }

                _useCurrentModelHosts = value;
                if (value && _useLinkedModelHosts)
                {
                    _useLinkedModelHosts = false;
                    OnPropertyChanged(nameof(UseLinkedModelHosts));
                    OnPropertyChanged(nameof(IsOpeningFamilyEnabled));
                    OnPropertyChanged(nameof(IsShapeSelectionEnabled));
                    OnPropertyChanged(nameof(IsHostLinkPickerEnabled));
                }

                OnPropertyChanged(nameof(UseCurrentModelHosts));
            }
        }

        public bool UseLinkedModelHosts
        {
            get { return _useLinkedModelHosts; }
            set
            {
                if (_useLinkedModelHosts == value)
                {
                    return;
                }

                _useLinkedModelHosts = value;
                if (value && _useCurrentModelHosts)
                {
                    _useCurrentModelHosts = false;
                    OnPropertyChanged(nameof(UseCurrentModelHosts));
                }

                if (value && _creationMode == MepOpeningCreationMode.DirectOpening)
                {
                    _creationMode = MepOpeningCreationMode.FamilyOpening;
                    OnPropertyChanged(nameof(CreationMode));
                }

                OnPropertyChanged(nameof(UseLinkedModelHosts));
                OnPropertyChanged(nameof(IsOpeningFamilyEnabled));
                OnPropertyChanged(nameof(IsShapeSelectionEnabled));
                OnPropertyChanged(nameof(IsHostLinkPickerEnabled));
            }
        }

        public string SelectedHostLinkUniqueId
        {
            get { return _selectedHostLinkUniqueId; }
            set
            {
                if (_selectedHostLinkUniqueId == value)
                {
                    return;
                }

                _selectedHostLinkUniqueId = value;
                OnPropertyChanged(nameof(SelectedHostLinkUniqueId));
            }
        }

        private void LoadSettings()
        {
            MepOpeningSettings settings = MepOpeningSettingsService.Load();
            settings.Normalize();

            Rules = new ObservableCollection<MepOpeningElementRule>();
            foreach (MepOpeningElementRule rule in settings.Rules)
            {
                Rules.Add(rule.Clone());
            }

            MergeDistanceText = FormatNumber(settings.MergeDistanceMm);
            IncludeInsulation = settings.IncludeInsulation;
            CreationMode = settings.CreationMode;
            SelectionMethod = settings.SelectionMethod;
            UseCurrentModelSources = settings.UseCurrentModelSources;
            UseLinkedModelSources = settings.UseLinkedModelSources;
            SelectedSourceLinkUniqueId = ResolveSavedLinkUniqueId(settings.SourceLinkInstanceUniqueId);
            UseCurrentModelHosts = settings.UseCurrentModelHosts;
            UseLinkedModelHosts = settings.UseLinkedModelHosts;
            SelectedHostLinkUniqueId = ResolveSavedLinkUniqueId(settings.HostLinkInstanceUniqueId);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            MepOpeningSettings settings;
            string validationMessage;
            if (!TryBuildSettings(out settings, out validationMessage))
            {
                SetStatus(validationMessage);
                return;
            }

            string saveError;
            if (!MepOpeningSettingsService.Save(settings, out saveError))
            {
                SetStatus("Settings could not be saved: " + saveError);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private bool TryBuildSettings(out MepOpeningSettings settings, out string message)
        {
            settings = null;
            message = string.Empty;

            double mergeDistanceMm;
            if (!TryParseNonNegative(MergeDistanceText, out mergeDistanceMm))
            {
                message = "Enter a valid merge distance in millimeters.";
                return false;
            }

            var built = new MepOpeningSettings
            {
                MergeDistanceMm = mergeDistanceMm,
                IncludeInsulation = IncludeInsulation,
                CreationMode = CreationMode,
                SelectionMethod = SelectionMethod,
                UseCurrentModelSources = UseCurrentModelSources,
                UseLinkedModelSources = UseLinkedModelSources,
                SourceLinkInstanceUniqueId = SelectedSourceLinkUniqueId ?? string.Empty,
                UseCurrentModelHosts = UseCurrentModelHosts,
                UseLinkedModelHosts = UseLinkedModelHosts,
                HostLinkInstanceUniqueId = SelectedHostLinkUniqueId ?? string.Empty,
                Rules = new System.Collections.Generic.List<MepOpeningElementRule>()
            };

            if (!built.UseCurrentModelSources && !built.UseLinkedModelSources)
            {
                message = "Select at least one opening source model option.";
                return false;
            }

            if (built.UseLinkedModelSources && string.IsNullOrWhiteSpace(built.SourceLinkInstanceUniqueId))
            {
                message = "Select the opening source linked model.";
                return false;
            }

            if (!built.UseCurrentModelHosts && !built.UseLinkedModelHosts)
            {
                message = "Select at least one opening host model option.";
                return false;
            }

            if (built.UseLinkedModelHosts && string.IsNullOrWhiteSpace(built.HostLinkInstanceUniqueId))
            {
                message = "Select the opening host linked model.";
                return false;
            }

            if (built.UseLinkedModelHosts && built.CreationMode == MepOpeningCreationMode.DirectOpening)
            {
                message = "Linked host needs Family Opening mode.";
                return false;
            }

            int includedRuleCount = 0;
            foreach (MepOpeningElementRule rule in Rules)
            {
                if (rule == null)
                {
                    continue;
                }

                double bufferMm = rule.CutoutBufferMm;
                if (bufferMm < 0 || double.IsNaN(bufferMm) || double.IsInfinity(bufferMm))
                {
                    message = "Enter valid cutout buffer values in millimeters.";
                    return false;
                }

                var cloned = rule.Clone();
                cloned.Normalize();

                if (cloned.IsIncluded)
                {
                    includedRuleCount++;
                }

                if (cloned.IsIncluded &&
                    RequiresFamily(built.CreationMode, built.UseLinkedModelHosts) &&
                    IsFamilyOpeningImplementedNow(cloned.ElementKind) &&
                    (string.IsNullOrWhiteSpace(cloned.VerticalOpeningFamilyName) ||
                     string.IsNullOrWhiteSpace(cloned.HorizontalOpeningFamilyName)))
                {
                    message = "Select vertical and horizontal opening families for each checked duct and cable tray row.";
                    return false;
                }

                built.Rules.Add(cloned);
            }

            if (includedRuleCount == 0)
            {
                message = "Check at least one element rule.";
                return false;
            }

            built.Normalize();
            settings = built;
            return true;
        }

        private static bool TryParseNonNegative(string text, out double value)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                !double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                return false;
            }

            return value >= 0 && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static IList<string> CollectOpeningFamilyChoices(Document doc)
        {
            var choices = new List<string> { string.Empty };
            if (doc == null)
            {
                return choices;
            }

            try
            {
                var familyNames = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .Where(IsGenericModelOpeningSymbol)
                    .Select(symbol => string.IsNullOrWhiteSpace(symbol.FamilyName)
                        ? symbol.Name
                        : symbol.FamilyName + " : " + symbol.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                choices.AddRange(familyNames);
            }
            catch
            {
                return choices;
            }

            return choices;
        }

        private static IList<OpeningModeChoice> CreateOpeningModeChoices()
        {
            return new List<OpeningModeChoice>
            {
                new OpeningModeChoice(MepOpeningCreationMode.DirectOpening, "Direct Opening"),
                new OpeningModeChoice(MepOpeningCreationMode.FamilyOpening, "Family Opening")
            };
        }

        private static bool IsGenericModelOpeningSymbol(FamilySymbol symbol)
        {
            if (symbol == null || symbol.Category == null)
            {
                return false;
            }

            return MepOpeningSelectionFilter.IsCategory(symbol, BuiltInCategory.OST_GenericModel);
        }

        private static IList<OpeningSelectionMethodChoice> CreateOpeningSelectionMethodChoices()
        {
            return new List<OpeningSelectionMethodChoice>
            {
                new OpeningSelectionMethodChoice(MepOpeningSelectionMethod.SourceElements, "Source Elements"),
                new OpeningSelectionMethodChoice(MepOpeningSelectionMethod.HostElements, "Host Elements")
            };
        }

        private static bool RequiresFamily(MepOpeningCreationMode mode, bool useLinkedHosts)
        {
            return useLinkedHosts ||
                   mode == MepOpeningCreationMode.FamilyOpening;
        }

        private static bool IsFamilyOpeningImplementedNow(MepOpeningElementKind kind)
        {
            return kind == MepOpeningElementKind.Duct ||
                   kind == MepOpeningElementKind.CableTray;
        }

        private static IList<OpeningLinkChoice> CollectOpeningLinkChoices(Document doc)
        {
            var choices = new List<OpeningLinkChoice>
            {
                new OpeningLinkChoice(string.Empty, "(Select linked model)")
            };

            if (doc == null)
            {
                return choices;
            }

            try
            {
                var linkChoices = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .Where(link => link != null && link.GetLinkDocument() != null)
                    .Select(link => new OpeningLinkChoice(link.UniqueId, GetCleanLinkName(link, link.GetLinkDocument())))
                    .Where(choice => !string.IsNullOrWhiteSpace(choice.UniqueId))
                    .GroupBy(choice => choice.UniqueId, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .OrderBy(choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                choices.AddRange(linkChoices);
            }
            catch
            {
                return choices;
            }

            return choices;
        }

        private string ResolveSavedLinkUniqueId(string savedUniqueId)
        {
            if (string.IsNullOrWhiteSpace(savedUniqueId) || OpeningLinkChoices == null)
            {
                return string.Empty;
            }

            return OpeningLinkChoices.Any(choice =>
                string.Equals(choice.UniqueId, savedUniqueId, StringComparison.OrdinalIgnoreCase))
                ? savedUniqueId
                : string.Empty;
        }

        private static string GetCleanLinkName(RevitLinkInstance linkInstance, Document linkDoc)
        {
            string name = linkDoc != null && !string.IsNullOrWhiteSpace(linkDoc.Title)
                ? linkDoc.Title
                : linkInstance?.Name ?? "Linked Model";

            int colonIndex = name.IndexOf(':');
            return colonIndex > -1 ? name.Substring(0, colonIndex).Trim() : name.Trim();
        }

        private void SetStatus(string message)
        {
            if (TxtStatus != null)
            {
                TxtStatus.Text = message ?? string.Empty;
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public sealed class OpeningLinkChoice
        {
            public OpeningLinkChoice(string uniqueId, string displayName)
            {
                UniqueId = uniqueId ?? string.Empty;
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? "(Unnamed Link)" : displayName;
            }

            public string UniqueId { get; private set; }

            public string DisplayName { get; private set; }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        public sealed class OpeningModeChoice
        {
            public OpeningModeChoice(MepOpeningCreationMode mode, string displayName)
            {
                Mode = mode;
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? mode.ToString() : displayName;
            }

            public MepOpeningCreationMode Mode { get; private set; }

            public string DisplayName { get; private set; }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        public sealed class OpeningSelectionMethodChoice
        {
            public OpeningSelectionMethodChoice(MepOpeningSelectionMethod method, string displayName)
            {
                Method = method;
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? method.ToString() : displayName;
            }

            public MepOpeningSelectionMethod Method { get; private set; }

            public string DisplayName { get; private set; }

            public override string ToString()
            {
                return DisplayName;
            }
        }
    }
}
