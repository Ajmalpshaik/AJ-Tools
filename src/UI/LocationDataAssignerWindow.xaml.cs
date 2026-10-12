// Tool Name: Location Data Assigner UI
// Description: Code-behind for assigning room, level, coordinate, altitude, and HVAC zone data.
// Author: Ajmal P.S.
// Version: 1.0.2
// Last Updated: 2026-10-12
// Revit Version: 2020

using AJTools.Utils;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
#if REVIT2022_OR_GREATER
using AjSpec = Autodesk.Revit.DB.ForgeTypeId;
using AjGroup = Autodesk.Revit.DB.ForgeTypeId;
#else
using AjSpec = Autodesk.Revit.DB.ParameterType;
using AjGroup = Autodesk.Revit.DB.BuiltInParameterGroup;
#endif
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AJTools.UI
{
    public partial class LocationDataAssignerWindow : Window
    {
        private const string DialogTitle = "Location Data Assigner";
        private const string SharedParamGroupName = "LocationData";
        private const string DefaultRoomNameParameter = "ID_Room Name";
        private const string DefaultRoomNumberParameter = "ID_Room Number";
        private const string DefaultLevelNameParameter = "ID_Level Name";
        private const string DefaultEastingParameter = "ID_Easting";
        private const string DefaultNorthingParameter = "ID_Norting";
        private const string DefaultAltitudeParameter = "ID_Altitude";
        private const string DefaultHvacZoneNameParameter = "HVAC Zone Name";
        private const string DefaultHvacZoneNumberParameter = "HVAC Zone Number";

        private static readonly string[] PriorityCategoryNames =
        {
            "Air Terminals", "Mechanical Equipment", "Duct Accessories", "Duct Fittings",
            "Pipe Accessories", "Pipe Fittings", "Plumbing Fixtures", "Sprinklers",
            "Fire Alarm Devices", "Electrical Equipment", "Electrical Fixtures", "Lighting Fixtures",
            "Communication Devices", "Security Devices", "Nurse Call Devices"
        };

        private static readonly string[] ExcludedCategoryNames =
        {
            "Duct Systems",
            "Piping Systems",
            "Electrical Circuits",
            "Materials",
            "Project Information"
        };

        private readonly Document _doc;
        private readonly ObservableCollection<CategoryItem> _allCategories = new ObservableCollection<CategoryItem>();
        private readonly ObservableCollection<CategoryItem> _visibleCategories = new ObservableCollection<CategoryItem>();
        private readonly ObservableCollection<LinkItem> _links = new ObservableCollection<LinkItem>();
        private bool _isBusy;

        public LocationDataAssignerWindow(Document doc)
        {
            InitializeComponent();

            // Process and Create/Rebind pump the dispatcher to repaint their progress, and that pump
            // runs a real Win32 message loop - so the title-bar X and Alt+F4 DO reach this window
            // mid-run. Disabling the buttons does not cover those, so refuse the close while busy.
            // Subscribed before the motion helpers so this veto is already set when they run.
            Closing += RefuseCloseWhileBusy;

            // Shared AJ Tools window entrance (fade + short rise). Cosmetic only.
            WindowMotionHelper.AttachStandardEntrance(this);

            // Shared AJ Tools window exit (fade + short sink). The window's result,
            // validation and close behaviour are unchanged - see WindowMotionHelper's header.
            WindowMotionHelper.AttachStandardExit(this);

            // Shared tab-change transition (fade + short rise on the panel). Cosmetic only.
            TabMotionHelper.AttachTabTransitions(this);
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));

            PopulateParameterMapUi(CreateDefaultParameterMap());

            CategoriesListBox.ItemsSource = _visibleCategories;
            LinkedModelsComboBox.ItemsSource = _links;

            IncludeLevelsCheckBox.IsChecked = true;
            OverwriteCheckBox.IsChecked = false;

            WireEvents();
            LoadCategories();
            LoadLinks();
            ApplyFilter();
            UpdateButtons();
            StatusText.Text = "Select categories and run Process Elements.";
        }

        private void WireEvents()
        {
            CategorySearchBox.TextChanged += (s, e) => ApplyFilter();
            SelectAllCategoriesButton.Click += (s, e) => { foreach (var item in _visibleCategories) item.IsChecked = true; UpdateButtons(); };
            SelectNoneCategoriesButton.Click += (s, e) => { foreach (var item in _visibleCategories) item.IsChecked = false; UpdateButtons(); };

            UseLinkedModelCheckBox.Checked += (s, e) => LinkedModelsComboBox.IsEnabled = _links.Count > 0;
            UseLinkedModelCheckBox.Unchecked += (s, e) => LinkedModelsComboBox.IsEnabled = false;

            IncludeLevelsCheckBox.Checked += (s, e) => UpdateButtons();
            IncludeLevelsCheckBox.Unchecked += (s, e) => UpdateButtons();
            IncludeCoordinatesCheckBox.Checked += (s, e) => UpdateButtons();
            IncludeCoordinatesCheckBox.Unchecked += (s, e) => UpdateButtons();
            IncludeAltitudeCheckBox.Checked += (s, e) => UpdateButtons();
            IncludeAltitudeCheckBox.Unchecked += (s, e) => UpdateButtons();
            IncludeHvacCheckBox.Checked += (s, e) => UpdateButtons();
            IncludeHvacCheckBox.Unchecked += (s, e) => UpdateButtons();

            txtParamRoomName.TextChanged += (s, e) => UpdateButtons();
            txtParamRoomNumber.TextChanged += (s, e) => UpdateButtons();
            txtParamLevelName.TextChanged += (s, e) => UpdateButtons();
            txtParamEasting.TextChanged += (s, e) => UpdateButtons();
            txtParamNorthing.TextChanged += (s, e) => UpdateButtons();
            txtParamAltitude.TextChanged += (s, e) => UpdateButtons();
            txtParamHvacZoneName.TextChanged += (s, e) => UpdateButtons();
            txtParamHvacZoneNumber.TextChanged += (s, e) => UpdateButtons();

            CheckParametersButton.Click += OnCheckParametersClick;
            CreateParametersButton.Click += OnCreateParametersClick;
            ProcessButton.Click += OnProcessClick;
            CancelButton.Click += (s, e) => Close();
        }

        private void LoadCategories()
        {
            _allCategories.Clear();
            var priority = new HashSet<string>(PriorityCategoryNames, StringComparer.OrdinalIgnoreCase);

            var items = new List<CategoryItem>();
            foreach (Category cat in _doc.Settings.Categories)
            {
                if (cat == null || cat.CategoryType != CategoryType.Model || !cat.AllowsBoundParameters)
                    continue;
                if (IsExcludedCategoryName(cat.Name))
                    continue;

                int count;
                try
                {
                    count = new FilteredElementCollector(_doc)
                        .OfCategoryId(cat.Id)
                        .WhereElementIsNotElementType()
                        .GetElementCount();
                }
                catch
                {
                    continue;
                }

                if (count <= 0)
                    continue;
                if (!HasLocationAssignableElements(cat.Id))
                    continue;

                var item = new CategoryItem(cat, count) { IsChecked = priority.Contains(cat.Name) };
                item.PropertyChanged += OnCategoryChanged;
                items.Add(item);
            }

            foreach (var item in items.OrderBy(i => GetRank(i.Name)).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
                _allCategories.Add(item);
        }

        private static bool IsExcludedCategoryName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return true;

            return ExcludedCategoryNames.Any(ex => string.Equals(ex, name, StringComparison.OrdinalIgnoreCase));
        }

        private bool HasLocationAssignableElements(ElementId categoryId)
        {
            if (categoryId == null || categoryId == ElementId.InvalidElementId)
                return false;

            try
            {
                foreach (Element element in new FilteredElementCollector(_doc)
                    .OfCategoryId(categoryId)
                    .WhereElementIsNotElementType())
                {
                    if (IsLocationAssignableElement(element))
                        return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool IsLocationAssignableElement(Element element)
        {
            if (element == null || element.ViewSpecific)
                return false;

            if (element is Room || element is Space)
                return true;

            return element.Location is LocationPoint || element.Location is LocationCurve;
        }

        private void OnCategoryChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CategoryItem.IsChecked))
                UpdateButtons();
        }

        private static int GetRank(string name)
        {
            for (int i = 0; i < PriorityCategoryNames.Length; i++)
            {
                if (string.Equals(PriorityCategoryNames[i], name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return int.MaxValue;
        }

        private void ApplyFilter()
        {
            string q = (CategorySearchBox.Text ?? string.Empty).Trim();
            IEnumerable<CategoryItem> source = _allCategories;

            if (!string.IsNullOrWhiteSpace(q))
                source = source.Where(i => i.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);

            _visibleCategories.Clear();
            foreach (var item in source)
                _visibleCategories.Add(item);

            UpdateCategoryLabel();
        }

        private void UpdateCategoryLabel()
        {
            int selected = _allCategories.Count(i => i.IsChecked);
            CategoryCountText.Text = string.Format("{0}/{1} selected | {2} shown", selected, _allCategories.Count, _visibleCategories.Count);
        }

        private void LoadLinks()
        {
            _links.Clear();
            var links = new FilteredElementCollector(_doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Where(l => l != null && l.GetLinkDocument() != null)
                .Select(l => new LinkItem(CleanLinkName(l.Name), l))
                .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var link in links)
                _links.Add(link);

            if (_links.Count > 0)
            {
                LinkedModelsComboBox.SelectedIndex = 0;
                UseLinkedModelCheckBox.IsEnabled = true;
            }
            else
            {
                UseLinkedModelCheckBox.IsChecked = false;
                UseLinkedModelCheckBox.IsEnabled = false;
                LinkedModelsComboBox.IsEnabled = false;
            }
        }

        private static string CleanLinkName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "(Unnamed Link)";

            int index = name.IndexOf(':');
            return index > 0 ? name.Substring(0, index) : name;
        }

        private void UpdateButtons()
        {
            UpdateCategoryLabel();
            if (_isBusy)
                return;

            var map = ReadParameterMapFromUi();
            bool includeLevels = IncludeLevelsCheckBox.IsChecked == true;
            bool includeCoords = IncludeCoordinatesCheckBox.IsChecked == true;
            bool includeAltitude = IncludeAltitudeCheckBox.IsChecked == true;
            bool includeHvac = IncludeHvacCheckBox.IsChecked == true;

            bool roomMapInvalid =
                string.IsNullOrWhiteSpace(map.RoomName) ||
                string.IsNullOrWhiteSpace(map.RoomNumber);

            bool coordMapInvalid = includeCoords &&
                (string.IsNullOrWhiteSpace(map.Easting) || string.IsNullOrWhiteSpace(map.Northing));

            var missing = GetMissingParams(map, includeLevels, includeCoords, includeAltitude, includeHvac);
            bool roomMissing = !roomMapInvalid &&
                (missing.Contains(map.RoomName, StringComparer.OrdinalIgnoreCase) ||
                 missing.Contains(map.RoomNumber, StringComparer.OrdinalIgnoreCase));

            bool hasCategories = _allCategories.Any(i => i.IsChecked);
            bool hasParamSpecs = GetSpecs(map, includeLevels, includeCoords, includeAltitude, includeHvac).Count > 0;
            ProcessButton.IsEnabled = hasCategories && !roomMapInvalid && !coordMapInvalid && !roomMissing;
            // Create/Rebind binds to the ticked categories, so it needs at least one ticked - it no longer
            // falls back to a built-in list of categories the user never chose.
            CreateParametersButton.IsEnabled = hasParamSpecs && hasCategories;
            CheckParametersButton.IsEnabled = hasParamSpecs;

            CreateParametersButton.Content = "Create/Rebind Parameters";

            if (!hasCategories)
                StatusText.Text = "Select at least one category on the Process tab.";
            else if (roomMapInvalid)
                StatusText.Text = "Map Room Name and Room Number parameters.";
            else if (coordMapInvalid)
                StatusText.Text = "Map Easting and Northing parameter names or disable Coordinates.";
            else if (roomMissing)
                StatusText.Text = "Room parameters are missing. Create them first.";
            else
                StatusText.Text = missing.Count == 0 ? "Ready to process." : "Optional parameters are missing; you can create or continue.";
        }

        private List<string> GetMissingParams(LocationParameterMap map, bool includeLevels, bool includeCoords, bool includeAltitude, bool includeHvac)
        {
            var required = new List<string>();
            AddRequiredName(required, map.RoomName);
            AddRequiredName(required, map.RoomNumber);
            if (includeLevels)
            {
                AddRequiredName(required, map.LevelName);
            }
            if (includeCoords)
            {
                AddRequiredName(required, map.Easting);
                AddRequiredName(required, map.Northing);
            }
            if (includeAltitude)
                AddRequiredName(required, map.Altitude);
            if (includeHvac)
            {
                AddRequiredName(required, map.HvacZoneName);
                AddRequiredName(required, map.HvacZoneNumber);
            }

            var existing = GetBoundParamNames(_doc);
            return required.Where(r => !existing.Contains(r)).ToList();
        }

        private static void AddRequiredName(ICollection<string> required, string name)
        {
            if (required == null || string.IsNullOrWhiteSpace(name))
                return;

            string clean = name.Trim();
            if (required.Any(r => string.Equals(r, clean, StringComparison.OrdinalIgnoreCase)))
                return;

            required.Add(clean);
        }

        private static LocationParameterMap CreateDefaultParameterMap()
        {
            return new LocationParameterMap
            {
                RoomName = DefaultRoomNameParameter,
                RoomNumber = DefaultRoomNumberParameter,
                LevelName = DefaultLevelNameParameter,
                Easting = DefaultEastingParameter,
                Northing = DefaultNorthingParameter,
                Altitude = DefaultAltitudeParameter,
                HvacZoneName = DefaultHvacZoneNameParameter,
                HvacZoneNumber = DefaultHvacZoneNumberParameter
            };
        }

        private void PopulateParameterMapUi(LocationParameterMap map)
        {
            map = map ?? CreateDefaultParameterMap();
            txtParamRoomName.Text = map.RoomName ?? string.Empty;
            txtParamRoomNumber.Text = map.RoomNumber ?? string.Empty;
            txtParamLevelName.Text = map.LevelName ?? string.Empty;
            txtParamEasting.Text = map.Easting ?? string.Empty;
            txtParamNorthing.Text = map.Northing ?? string.Empty;
            txtParamAltitude.Text = map.Altitude ?? string.Empty;
            txtParamHvacZoneName.Text = map.HvacZoneName ?? string.Empty;
            txtParamHvacZoneNumber.Text = map.HvacZoneNumber ?? string.Empty;
        }

        private LocationParameterMap ReadParameterMapFromUi()
        {
            return new LocationParameterMap
            {
                RoomName = CleanParameterName(txtParamRoomName.Text),
                RoomNumber = CleanParameterName(txtParamRoomNumber.Text),
                LevelName = CleanParameterName(txtParamLevelName.Text),
                Easting = CleanParameterName(txtParamEasting.Text),
                Northing = CleanParameterName(txtParamNorthing.Text),
                Altitude = CleanParameterName(txtParamAltitude.Text),
                HvacZoneName = CleanParameterName(txtParamHvacZoneName.Text),
                HvacZoneNumber = CleanParameterName(txtParamHvacZoneNumber.Text)
            };
        }

        private static string CleanParameterName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static HashSet<string> GetBoundParamNames(Document doc)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var it = doc.ParameterBindings.ForwardIterator();
            while (it.MoveNext())
            {
                var def = it.Key;
                if (def != null && !string.IsNullOrWhiteSpace(def.Name))
                    names.Add(def.Name);
            }
            return names;
        }

        private void OnCheckParametersClick(object sender, RoutedEventArgs e)
        {
            if (_isBusy)
                return;

            var map = ReadParameterMapFromUi();
            bool includeLevels = IncludeLevelsCheckBox.IsChecked == true;
            bool includeCoords = IncludeCoordinatesCheckBox.IsChecked == true;
            bool includeAltitude = IncludeAltitudeCheckBox.IsChecked == true;
            bool includeHvac = IncludeHvacCheckBox.IsChecked == true;
            var specs = GetSpecs(map, includeLevels, includeCoords, includeAltitude, includeHvac);

            if (specs.Count == 0)
            {
                DialogHelper.ShowInfo(DialogTitle, "No mapped parameter names found. Fill names in Parameters tab.");
                return;
            }

            var missing = GetMissingParams(map, includeLevels, includeCoords, includeAltitude, includeHvac);
            if (missing.Count == 0)
            {
                DialogHelper.ShowInfo(DialogTitle, "All mapped parameters are available in this project.");
            }
            else
            {
                DialogHelper.ShowInfo(
                    DialogTitle,
                    "Missing parameter bindings:\n\n- " + string.Join("\n- ", missing) +
                    "\n\nUse Create/Rebind Parameters to add them.");
            }
        }

        private void OnCreateParametersClick(object sender, RoutedEventArgs e)
        {
            if (_isBusy)
                return;

            var map = ReadParameterMapFromUi();
            bool includeLevels = IncludeLevelsCheckBox.IsChecked == true;
            bool includeCoords = IncludeCoordinatesCheckBox.IsChecked == true;
            bool includeAltitude = IncludeAltitudeCheckBox.IsChecked == true;
            bool includeHvac = IncludeHvacCheckBox.IsChecked == true;

            var specs = GetSpecs(map, includeLevels, includeCoords, includeAltitude, includeHvac);
            if (specs.Count == 0)
            {
                DialogHelper.ShowInfo(DialogTitle, "No mapped parameter names found. Fill names in Parameters tab.");
                return;
            }

            // The button is disabled with nothing ticked; re-checked here as a guard. Binding to a
            // built-in category list the user never chose is what this used to do instead.
            var selectedCategories = _allCategories.Where(c => c.IsChecked).ToList();
            if (selectedCategories.Count == 0)
            {
                StatusText.Text = "Select at least one category on the Process tab - parameters are bound to the ticked categories.";
                return;
            }

            if (!DialogHelper.ShowYesNo(DialogTitle, BuildBindConfirmText(specs, selectedCategories.Count)))
                return;

            BindResult outcome = null;
            try
            {
                SetBusy(true, "Creating parameters...");
                using (var t = new Transaction(_doc, "Create Location Data Parameters"))
                {
                    t.Start();
                    outcome = CreateParameters(specs, selectedCategories);
                    if (t.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("Revit did not keep the change (the transaction was rolled back).");
                }

                // A clean run is reported in the status line only; a popup is kept for a binding Revit refused.
                if (outcome.Failed.Count > 0)
                {
                    DialogHelper.ShowError(
                        DialogTitle,
                        "These parameters could not be bound:\n\n- " + string.Join("\n- ", outcome.Failed) +
                        "\n\nThe other parameters were created or updated.");
                }
            }
            catch (Exception ex)
            {
                outcome = null;
                DialogHelper.ShowError(DialogTitle, "Failed to create parameters:\n\n" + ex.Message);
            }
            finally
            {
                SetBusy(false, null);
                UpdateButtons();
            }

            // Set after UpdateButtons, which rewrites the status line.
            if (outcome != null)
            {
                StatusText.Text = string.Format(
                    "Parameters: {0} created, {1} updated (existing categories and values kept){2}.",
                    outcome.Created.Count,
                    outcome.Updated.Count,
                    outcome.Failed.Count > 0 ? ", " + outcome.Failed.Count + " could not be bound" : string.Empty);
            }
        }

        /// <summary>
        /// The question asked before Create/Rebind runs. It must say what really happens: a parameter
        /// that already exists keeps every category it is bound to, its values and its instance/type
        /// setting - the ticked categories are only added to it.
        /// </summary>
        private string BuildBindConfirmText(IList<ParamSpec> specs, int tickedCategoryCount)
        {
            BindingMap map = _doc.ParameterBindings;
            var created = new List<string>();
            var existing = new List<string>();

            foreach (var spec in specs)
            {
                Definition definition = FindDefinition(map, spec.Name, out Binding binding);
                if (definition == null)
                    created.Add("- " + spec.Name);
                else if (binding is TypeBinding)
                    existing.Add("- " + spec.Name + " (type parameter - Process Elements only fills instance parameters)");
                else
                    existing.Add("- " + spec.Name);
            }

            var sb = new StringBuilder();
            sb.AppendFormat(
                "These parameters will be bound to the {0} ticked categor{1}.",
                tickedCategoryCount,
                tickedCategoryCount == 1 ? "y" : "ies");

            if (created.Count > 0)
            {
                sb.Append("\n\nNew - added as instance parameters:\n");
                sb.Append(string.Join("\n", created));
            }

            if (existing.Count > 0)
            {
                sb.Append("\n\nAlready in this project - the ticked categories are added to the ones they already have. ");
                sb.Append("Their existing categories, values and instance/type setting are kept:\n");
                sb.Append(string.Join("\n", existing));
            }

            sb.Append("\n\nAll of them are listed under Data in Properties. New ones are taken from the shared ");
            sb.Append("parameter file Revit is set to use (added to its \"" + SharedParamGroupName + "\" group if not ");
            sb.Append("already in it; a temporary file is used when none is set).");
            sb.Append("\n\nContinue?");
            return sb.ToString();
        }

        private static List<ParamSpec> GetSpecs(LocationParameterMap map, bool includeLevels, bool includeCoords, bool includeAltitude, bool includeHvac)
        {
            map = map ?? CreateDefaultParameterMap();
            var specs = new List<ParamSpec>();
            AddSpec(specs, map.RoomName, RevitCompat.SpecText, RevitCompat.GroupData);
            AddSpec(specs, map.RoomNumber, RevitCompat.SpecText, RevitCompat.GroupData);

            if (includeLevels)
            {
                AddSpec(specs, map.LevelName, RevitCompat.SpecText, RevitCompat.GroupData);
            }

            if (includeCoords)
            {
                AddSpec(specs, map.Easting, RevitCompat.SpecLength, RevitCompat.GroupData);
                AddSpec(specs, map.Northing, RevitCompat.SpecLength, RevitCompat.GroupData);
            }

            if (includeAltitude)
                AddSpec(specs, map.Altitude, RevitCompat.SpecLength, RevitCompat.GroupData);

            if (includeHvac)
            {
                AddSpec(specs, map.HvacZoneName, RevitCompat.SpecText, RevitCompat.GroupData);
                AddSpec(specs, map.HvacZoneNumber, RevitCompat.SpecText, RevitCompat.GroupData);
            }

            return specs;
        }

        private static void AddSpec(ICollection<ParamSpec> specs, string name, AjSpec type, AjGroup group)
        {
            if (specs == null || string.IsNullOrWhiteSpace(name))
                return;

            string clean = name.Trim();
            if (specs.Any(s => string.Equals(s.Name, clean, StringComparison.OrdinalIgnoreCase)))
                return;

            specs.Add(new ParamSpec(clean, type, group));
        }

        private BindResult CreateParameters(IList<ParamSpec> specs, IList<CategoryItem> selectedCategories)
        {
            var app = _doc.Application;
            string originalPath = app.SharedParametersFilename;
            bool tempAssigned = false;
            var outcome = new BindResult();

            try
            {
                DefinitionFile file = app.OpenSharedParameterFile();
                if (file == null)
                {
                    string tempPath = Path.Combine(Path.GetTempPath(), "AJTools_LocationData_SharedParams.txt");
                    if (!File.Exists(tempPath))
                        File.WriteAllText(tempPath, string.Empty);

                    app.SharedParametersFilename = tempPath;
                    tempAssigned = true;
                    file = app.OpenSharedParameterFile();
                }

                if (file == null)
                    throw new InvalidOperationException("Shared parameter file could not be opened.");

                var group = GetOrCreateGroup(file, SharedParamGroupName);

                if (FillCategorySet(app.Create.NewCategorySet(), selectedCategories, null) == 0)
                    throw new InvalidOperationException("No valid categories available for binding.");

                BindingMap map = _doc.ParameterBindings;

                foreach (var spec in specs)
                {
                    Definition existing = FindDefinition(map, spec.Name, out Binding existingBinding);
                    Definition definition = existing ?? GetOrCreateDefinition(file, group, spec.Name, spec.ParameterType);
                    if (definition == null)
                    {
                        outcome.Failed.Add(spec.Name + " - could not be created in the shared parameter file");
                        continue;
                    }

                    // Ticked categories plus, for an existing parameter, every category it is bound to now:
                    // a rebind with fewer categories removes the parameter - and its values - from the
                    // categories left out.
                    CategorySet categorySet = app.Create.NewCategorySet();
                    FillCategorySet(categorySet, selectedCategories, existingBinding as ElementBinding);

                    bool ok;
                    if (existing == null)
                    {
                        ok = RevitCompat.InsertBinding(map, definition, app.Create.NewInstanceBinding(categorySet), spec.ParameterGroup);
                    }
                    else
                    {
                        // Keep an existing parameter the kind it is (instance or type): changing the kind
                        // drops every value it holds. If Revit refuses the rebind it is reported - it is
                        // never removed and re-added to force it through, which would wipe its values.
                        Binding binding = existingBinding is TypeBinding
                            ? (Binding)app.Create.NewTypeBinding(categorySet)
                            : app.Create.NewInstanceBinding(categorySet);
                        ok = RevitCompat.ReInsertBinding(map, definition, binding, spec.ParameterGroup);
                    }

                    if (!ok)
                        outcome.Failed.Add(spec.Name + " - Revit did not accept the binding");
                    else if (existing == null)
                        outcome.Created.Add(spec.Name);
                    else
                        outcome.Updated.Add(spec.Name);
                }

                return outcome;
            }
            finally
            {
                if (tempAssigned)
                    app.SharedParametersFilename = originalPath ?? string.Empty;
            }
        }

        /// <summary>
        /// Fills <paramref name="set"/> with the ticked categories plus, when the parameter is already
        /// bound, every category of its current binding, so a rebind only ever adds categories.
        /// Returns how many categories went in.
        /// </summary>
        private static int FillCategorySet(CategorySet set, IList<CategoryItem> selected, ElementBinding existingBinding)
        {
            int count = 0;
            var ids = new HashSet<int>();

            if (existingBinding?.Categories != null)
            {
                foreach (Category c in existingBinding.Categories)
                {
                    if (c != null && ids.Add(c.Id.IntValue()))
                    {
                        set.Insert(c);
                        count++;
                    }
                }
            }

            if (selected != null)
            {
                foreach (var item in selected)
                {
                    var c = item?.Category;
                    if (c == null || c.CategoryType != CategoryType.Model || !c.AllowsBoundParameters)
                        continue;

                    if (ids.Add(c.Id.IntValue()))
                    {
                        set.Insert(c);
                        count++;
                    }
                }
            }

            return count;
        }

        private static DefinitionGroup GetOrCreateGroup(DefinitionFile file, string name)
        {
            DefinitionGroup group = null;
            try { group = file.Groups.get_Item(name); } catch (Exception) { group = null; }
            return group ?? file.Groups.Create(name);
        }

        private static Definition GetOrCreateDefinition(DefinitionFile file, DefinitionGroup preferredGroup, string name, AjSpec type)
        {
            foreach (DefinitionGroup group in file.Groups)
            {
                var existing = TryGetDefinition(group, name);
                if (existing != null)
                    return existing;
            }

            var options = RevitCompat.CreateDefinitionOptions(name, type);
            options.UserModifiable = true;
            options.Visible = true;

            try { return preferredGroup.Definitions.Create(options); }
            catch (Exception) { return TryGetDefinition(preferredGroup, name); }
        }

        private static Definition TryGetDefinition(DefinitionGroup group, string name)
        {
            try { return group.Definitions.get_Item(name); }
            catch (Exception) { return null; }
        }

        private static Definition FindDefinition(BindingMap map, string name, out Binding binding)
        {
            binding = null;
            var it = map.ForwardIterator();
            while (it.MoveNext())
            {
                var def = it.Key;
                if (def != null && string.Equals(def.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    binding = it.Current as Binding;
                    return def;
                }
            }
            return null;
        }

        private void OnProcessClick(object sender, RoutedEventArgs e)
        {
            if (_isBusy)
                return;

            var selectedCats = _allCategories.Where(c => c.IsChecked).ToList();
            if (selectedCats.Count == 0)
            {
                DialogHelper.ShowError(DialogTitle, "Select at least one category.");
                return;
            }

            var map = ReadParameterMapFromUi();
            if (string.IsNullOrWhiteSpace(map.RoomName) || string.IsNullOrWhiteSpace(map.RoomNumber))
            {
                DialogHelper.ShowError(DialogTitle, "Map Room Name and Room Number parameters before processing.");
                return;
            }

            var options = new ProcessOptions
            {
                IncludeLevels = IncludeLevelsCheckBox.IsChecked == true,
                IncludeCoords = IncludeCoordinatesCheckBox.IsChecked == true,
                IncludeAltitude = IncludeAltitudeCheckBox.IsChecked == true,
                IncludeHvac = IncludeHvacCheckBox.IsChecked == true,
                OverwriteText = OverwriteCheckBox.IsChecked == true,
                UseSharedCoords = UseSharedCoordinatesCheckBox.IsChecked == true,
                UseLinkedSource = UseLinkedModelCheckBox.IsChecked == true,
                Debug = DebugCheckBox.IsChecked == true,
                ParameterMap = map
            };

            if (options.IncludeCoords && (string.IsNullOrWhiteSpace(map.Easting) || string.IsNullOrWhiteSpace(map.Northing)))
            {
                DialogHelper.ShowError(DialogTitle, "Map Easting and Northing parameter names or disable Coordinates.");
                return;
            }

            var missing = GetMissingParams(map, options.IncludeLevels, options.IncludeCoords, options.IncludeAltitude, options.IncludeHvac);
            bool roomMissing =
                missing.Contains(map.RoomName, StringComparer.OrdinalIgnoreCase) ||
                missing.Contains(map.RoomNumber, StringComparer.OrdinalIgnoreCase);
            if (roomMissing)
            {
                DialogHelper.ShowError(DialogTitle, "Room parameters are missing. Create parameters first.");
                return;
            }

            if (missing.Count > 0)
            {
                string msg = "Missing optional parameters: " + string.Join(", ", missing) + "\n\nContinue with available data?";
                if (!DialogHelper.ShowYesNo(DialogTitle, msg))
                    return;

                if (!string.IsNullOrWhiteSpace(map.LevelName) && missing.Contains(map.LevelName, StringComparer.OrdinalIgnoreCase))
                    options.IncludeLevels = false;
                if ((!string.IsNullOrWhiteSpace(map.Easting) && missing.Contains(map.Easting, StringComparer.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(map.Northing) && missing.Contains(map.Northing, StringComparer.OrdinalIgnoreCase)))
                    options.IncludeCoords = false;
                if (!string.IsNullOrWhiteSpace(map.Altitude) && missing.Contains(map.Altitude, StringComparer.OrdinalIgnoreCase))
                    options.IncludeAltitude = false;
                if ((!string.IsNullOrWhiteSpace(map.HvacZoneName) && missing.Contains(map.HvacZoneName, StringComparer.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(map.HvacZoneNumber) && missing.Contains(map.HvacZoneNumber, StringComparer.OrdinalIgnoreCase)))
                    options.IncludeHvac = false;
            }

            ProcessResult result = null;
            try
            {
                SetBusy(true, "Preparing data...");
                result = RunProcess(options, selectedCats);

                // A run that did everything is reported in the status line only. The summary pops up
                // when something was left out (skips, errors, notes, type-only parameters), or when
                // debug mode asked for the skip reasons.
                if (HasSomethingToReport(result, options.Debug))
                    ShowSummary(result, options.Debug);
            }
            catch (Exception ex)
            {
                result = null;
                DialogHelper.ShowError(DialogTitle, "Processing failed:\n\n" + ex.Message);
            }
            finally
            {
                SetBusy(false, null);
                UpdateButtons();
            }

            // Set after UpdateButtons, which rewrites the status line.
            if (result != null)
            {
                StatusText.Text = string.Format(
                    "Finished: {0} element(s) updated, {1} skipped, {2} error(s).",
                    result.Updated,
                    result.Skipped,
                    result.Errors);
            }
        }

        private static bool HasSomethingToReport(ProcessResult result, bool includeDebug)
        {
            return result.Skipped > 0 ||
                   result.Errors > 0 ||
                   result.Notes.Count > 0 ||
                   result.TypeOnlyElementIds.Count > 0 ||
                   (includeDebug && result.Reasons.Count > 0);
        }
        private ProcessResult RunProcess(ProcessOptions options, IList<CategoryItem> selectedCats)
        {
            Document sourceDoc = _doc;
            Transform hostToSource = null;
            var result = new ProcessResult();

            if (options.UseLinkedSource)
            {
                var selectedLink = LinkedModelsComboBox.SelectedItem as LinkItem;
                if (selectedLink == null)
                    throw new InvalidOperationException("Select a linked model.");

                sourceDoc = selectedLink.Link.GetLinkDocument();
                if (sourceDoc == null)
                    throw new InvalidOperationException("Could not access linked model document.");

                Transform linkToHost = selectedLink.Link.GetTotalTransform();
                hostToSource = linkToHost?.Inverse;
            }

            var rooms = new FilteredElementCollector(sourceDoc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .OfType<Room>()
                .ToList();

            if (rooms.Count == 0)
                throw new InvalidOperationException("No Rooms found in the selected source model.");

            var spaces = new List<Space>();
            if (options.IncludeHvac)
            {
                spaces = new FilteredElementCollector(sourceDoc)
                    .OfCategory(BuiltInCategory.OST_MEPSpaces)
                    .WhereElementIsNotElementType()
                    .OfType<Space>()
                    .ToList();

                if (spaces.Count == 0)
                {
                    options.IncludeHvac = false;
                    result.Notes.Add("HVAC disabled because no Spaces were found.");
                }
            }
            var spaceZones = options.IncludeHvac
                ? BuildSpaceZoneMap(sourceDoc, spaces)
                : new Dictionary<ElementId, Element>();

            var elements = CollectElements(selectedCats);
            if (elements.Count == 0)
                throw new InvalidOperationException("No elements found in the selected categories.");

            XYZ survey = (options.IncludeCoords || options.IncludeAltitude) && options.UseSharedCoords
                ? GetSurveyPoint(_doc)
                : XYZ.Zero;
            Transform internalToShared = options.UseSharedCoords
                ? GetInternalToShared(_doc)
                : Transform.Identity;

            ProcessProgressBar.Minimum = 0;
            ProcessProgressBar.Maximum = Math.Max(1, elements.Count);
            ProcessProgressBar.Value = 0;

            using (var t = new Transaction(_doc, "Assign Location Data"))
            {
                t.Start();

                for (int i = 0; i < elements.Count; i++)
                {
                    var el = elements[i];
                    bool wroteAny = false;

                    try
                    {
                        XYZ pt = Midpoint(el.Location);
                        if (pt == null)
                        {
                            AddReason(result, options.Debug, "No location");
                        }
                        else
                        {
                            XYZ queryPt = hostToSource == null ? pt : hostToSource.OfPoint(pt);
                            Room room = FindRoom(rooms, queryPt);

                            wroteAny |= WriteRoom(el, room, options, result);

                            if (options.IncludeLevels)
                                wroteAny |= WriteLevel(el, room, options, result);

                            if (options.IncludeHvac)
                                wroteAny |= WriteHvac(el, spaces, spaceZones, queryPt, options, result);

                            if (options.IncludeCoords || options.IncludeAltitude)
                                wroteAny |= WriteCoords(el, pt, options, survey, internalToShared, result);
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors++;
                        AddReason(result, options.Debug, "Error: " + ex.Message);
                    }

                    if (wroteAny)
                        result.Updated++;
                    else
                        result.Skipped++;

                    UpdateProgress(i + 1, elements.Count);
                }

                // Without this check a rolled-back run (e.g. elements owned by another user) would still
                // be reported as updated.
                if (t.Commit() != TransactionStatus.Committed)
                    throw new InvalidOperationException("Revit did not keep the changes (the transaction was rolled back).");
            }

            return result;
        }

        private List<Element> CollectElements(IList<CategoryItem> cats)
        {
            var result = new List<Element>();
            if (cats == null || cats.Count == 0)
                return result;

            foreach (var item in cats)
            {
                var cat = item?.Category;
                if (cat == null)
                    continue;

                result.AddRange(new FilteredElementCollector(_doc)
                    .OfCategoryId(cat.Id)
                    .WhereElementIsNotElementType()
                    .ToElements());
            }
            return result;
        }

        private static XYZ Midpoint(Location location)
        {
            if (location is LocationPoint lp)
                return lp.Point;

            if (location is LocationCurve lc && lc.Curve != null)
            {
                XYZ p0 = lc.Curve.GetEndPoint(0);
                XYZ p1 = lc.Curve.GetEndPoint(1);
                return new XYZ((p0.X + p1.X) / 2.0, (p0.Y + p1.Y) / 2.0, (p0.Z + p1.Z) / 2.0);
            }

            return null;
        }

        private static Room FindRoom(IList<Room> rooms, XYZ point)
        {
            foreach (var room in rooms)
            {
                try
                {
                    if (room != null && room.IsPointInRoom(point))
                        return room;
                }
                catch (Exception) { }
            }
            return null;
        }

        private static Space FindSpace(IList<Space> spaces, XYZ point)
        {
            foreach (var space in spaces)
            {
                try
                {
                    if (space != null && space.IsPointInSpace(point))
                        return space;
                }
                catch (Exception) { }
            }
            return null;
        }

        private static bool WriteRoom(Element el, Room room, ProcessOptions options, ProcessResult result)
        {
            var map = options.ParameterMap ?? CreateDefaultParameterMap();
            if (string.IsNullOrWhiteSpace(map.RoomName) || string.IsNullOrWhiteSpace(map.RoomNumber))
            {
                AddReason(result, options.Debug, "Room mapping");
                return false;
            }

            if (room == null)
            {
                AddReason(result, options.Debug, "No room found");
                return false;
            }

            string name = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString();
            string number = room.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.AsString();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(number))
            {
                AddReason(result, options.Debug, "Room fields empty");
                return false;
            }

            bool wroteName = SetTextOnElement(el, map.RoomName, name, options.OverwriteText, result, out bool skip1);
            bool wroteNumber = SetTextOnElement(el, map.RoomNumber, number, options.OverwriteText, result, out bool skip2);
            if (!wroteName && !wroteNumber && (skip1 || skip2))
                AddReason(result, options.Debug, "Overwrite off");

            return wroteName || wroteNumber;
        }

        private bool WriteLevel(Element el, Room room, ProcessOptions options, ProcessResult result)
        {
            var map = options.ParameterMap ?? CreateDefaultParameterMap();
            if (string.IsNullOrWhiteSpace(map.LevelName))
            {
                AddReason(result, options.Debug, "Level mapping");
                return false;
            }

            if (!TryResolveLevel(el, room, out string name))
            {
                AddReason(result, options.Debug, "No level");
                return false;
            }

            bool wroteName = SetTextOnElement(el, map.LevelName, name, options.OverwriteText, result, out bool skip);
            if (!wroteName && skip)
                AddReason(result, options.Debug, "Overwrite off");

            return wroteName;
        }

        private static bool WriteHvac(Element el, IList<Space> spaces, IDictionary<ElementId, Element> spaceZones, XYZ point, ProcessOptions options, ProcessResult result)
        {
            var map = options.ParameterMap ?? CreateDefaultParameterMap();
            bool hasNameParam = !string.IsNullOrWhiteSpace(map.HvacZoneName);
            bool hasNumberParam = !string.IsNullOrWhiteSpace(map.HvacZoneNumber);
            if (!hasNameParam && !hasNumberParam)
            {
                AddReason(result, options.Debug, "HVAC mapping");
                return false;
            }

            Space space = FindSpace(spaces, point);
            if (space == null)
            {
                AddReason(result, options.Debug, "No HVAC zone");
                return false;
            }

            spaceZones.TryGetValue(space.Id, out Element zone);
            if (zone == null)
            {
                AddReason(result, options.Debug, "No HVAC zone");
                return false;
            }

            string zoneName = ReadString(zone, "Name", "Zone Name");
            string zoneNumber = ReadString(zone, "Number", "Zone Number");

            bool wroteName = hasNameParam && !string.IsNullOrWhiteSpace(zoneName) &&
                             SetTextOnElement(el, map.HvacZoneName, zoneName, options.OverwriteText, result, out bool _);
            bool wroteNumber = hasNumberParam && !string.IsNullOrWhiteSpace(zoneNumber) &&
                               SetTextOnElement(el, map.HvacZoneNumber, zoneNumber, options.OverwriteText, result, out bool _);
            return wroteName || wroteNumber;
        }

        // Space id -> HVAC zone, built once per run. Revit 2027 deprecates Space.Zone: HVAC zones are
        // upgraded to space-based system zones (GenericZone), so there the map is read from the zones.
        private static Dictionary<ElementId, Element> BuildSpaceZoneMap(Document doc, IList<Space> spaces)
        {
            var map = new Dictionary<ElementId, Element>();
#if REVIT2027_OR_GREATER
            var zones = new FilteredElementCollector(doc)
                .OfClass(typeof(Autodesk.Revit.DB.Analysis.GenericZone))
                .Cast<Autodesk.Revit.DB.Analysis.GenericZone>();
            foreach (var zone in zones)
            {
                try
                {
                    if (zone.GeometricDefinition != Autodesk.Revit.DB.Analysis.ZoneGeometricDefinition.Spaces)
                        continue;
                    object data = zone.GetDomainData();
                    bool isUpgradedHvac = data is HVACZoneData;
                    if (!isUpgradedHvac && !(data is SystemZoneData))
                        continue;

                    // A zone upgraded from an old HVAC zone wins over a plain system zone.
                    foreach (ElementId spaceId in zone.GetSpaceIds())
                    {
                        if (isUpgradedHvac || !map.ContainsKey(spaceId))
                            map[spaceId] = zone;
                    }
                }
                catch (Exception) { }
            }
#else
            foreach (Space space in spaces)
            {
                Zone zone = null;
                try { zone = space.Zone; } catch (Exception) { zone = null; }
                if (zone != null)
                    map[space.Id] = zone;
            }
#endif
            return map;
        }

        private static bool WriteCoords(Element el, XYZ point, ProcessOptions options, XYZ survey, Transform internalToShared, ProcessResult result)
        {
            var map = options.ParameterMap ?? CreateDefaultParameterMap();
            XYZ c;
            if (options.UseSharedCoords)
            {
                XYZ s = internalToShared.OfPoint(point);
                c = new XYZ(s.X - survey.X, s.Y - survey.Y, s.Z - survey.Z);
            }
            else
            {
                c = point;
            }

            bool wrote = false;
            if (options.IncludeCoords)
            {
                bool hasEasting = !string.IsNullOrWhiteSpace(map.Easting);
                bool hasNorthing = !string.IsNullOrWhiteSpace(map.Northing);
                if (!hasEasting || !hasNorthing)
                {
                    AddReason(result, options.Debug, "Coordinate mapping");
                    return wrote;
                }

                bool e = SetDoubleOnElement(el, map.Easting, c.X, true, result, out bool _);
                bool n = SetDoubleOnElement(el, map.Northing, c.Y, true, result, out bool _);
                wrote |= e || n;
                if (!e || !n)
                    AddReason(result, options.Debug, "Coordinate write");
            }

            if (options.IncludeAltitude)
            {
                if (string.IsNullOrWhiteSpace(map.Altitude))
                {
                    AddReason(result, options.Debug, "Altitude mapping");
                    return wrote;
                }

                bool a = SetDoubleOnElement(el, map.Altitude, c.Z, true, result, out bool _);
                wrote |= a;
                if (!a)
                    AddReason(result, options.Debug, "Coordinate write");
            }

            return wrote;
        }

        private bool TryResolveLevel(Element el, Room room, out string levelName)
        {
            levelName = string.Empty;

            Level level = null;
            if (room != null)
            {
                try { level = room.Level; } catch (Exception) { level = null; }
            }

            if (level == null)
                level = ResolveElementLevel(el);

            if (level == null)
                return false;

            levelName = level.Name ?? string.Empty;
            return !string.IsNullOrWhiteSpace(levelName);
        }

        private Level ResolveElementLevel(Element el)
        {
            if (el == null)
                return null;

            ElementId id = ElementId.InvalidElementId;
            try { if (el.LevelId != ElementId.InvalidElementId) id = el.LevelId; } catch (Exception) { }

            if (id == ElementId.InvalidElementId) id = LevelIdFrom(el, BuiltInParameter.FAMILY_LEVEL_PARAM);
            if (id == ElementId.InvalidElementId) id = LevelIdFrom(el, BuiltInParameter.SCHEDULE_LEVEL_PARAM);
            if (id == ElementId.InvalidElementId) id = LevelIdFrom(el, BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
            if (id == ElementId.InvalidElementId)
            {
                id = LevelIdFromNames(
                    el,
                    "Level",
                    "Reference Level",
                    "Associated Level",
                    "Schedule Level");
            }

            return id == ElementId.InvalidElementId ? null : _doc.GetElement(id) as Level;
        }

        private static ElementId LevelIdFrom(Element el, BuiltInParameter bip)
        {
            Parameter p = el.get_Parameter(bip);
            if (p == null || p.StorageType != StorageType.ElementId)
                return ElementId.InvalidElementId;

            return p.AsElementId() ?? ElementId.InvalidElementId;
        }

        private static ElementId LevelIdFromNames(Element el, params string[] names)
        {
            if (el == null || names == null || names.Length == 0)
                return ElementId.InvalidElementId;

            foreach (string name in names)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                Parameter p = el.LookupParameter(name);
                if (p == null || p.StorageType != StorageType.ElementId || !p.HasValue)
                    continue;

                ElementId id = p.AsElementId();
                if (id != null && id != ElementId.InvalidElementId)
                    return id;
            }

            return ElementId.InvalidElementId;
        }

        private static string ReadString(Element el, params string[] names)
        {
            foreach (string n in names)
            {
                Parameter p = el.LookupParameter(n);
                if (p != null && p.StorageType == StorageType.String)
                {
                    string s = p.AsString();
                    if (!string.IsNullOrWhiteSpace(s))
                        return s;
                }
            }
            return string.Empty;
        }

        // Room, level, zone and coordinates describe ONE element, so only the element's own (instance)
        // parameter is written. Written into a same-named type parameter, the value would land on every
        // element of that type, with whichever element was processed last winning. When only the type
        // carries the parameter, the element is recorded for the report instead.
        private static bool SetTextOnElement(Element el, string paramName, string value, bool overwrite, ProcessResult result, out bool skipped)
        {
            skipped = false;
            bool wrote = false;
            bool hasInstanceParameter = false;

            foreach (Parameter p in el.GetParameters(paramName))
            {
                hasInstanceParameter = true;
                if (SetText(p, value, overwrite, out bool s)) wrote = true;
                else if (s) skipped = true;
            }

            if (!hasInstanceParameter)
                NoteTypeOnlyParameter(el, paramName, result);

            return wrote;
        }

        private static void NoteTypeOnlyParameter(Element el, string paramName, ProcessResult result)
        {
            ElementId typeId = el.GetTypeId();
            if (typeId == ElementId.InvalidElementId)
                return;

            Element type = el.Document.GetElement(typeId);
            if (type != null && type.GetParameters(paramName).Count > 0)
            {
                result.TypeOnlyElementIds.Add(el.Id);
                result.TypeOnlyParameterNames.Add(paramName);
            }
        }

        private static bool SetText(Parameter p, string value, bool overwrite, out bool skipped)
        {
            skipped = false;
            if (p == null || p.IsReadOnly || p.StorageType != StorageType.String)
                return false;

            if (!overwrite && !string.IsNullOrWhiteSpace(p.AsString()))
            {
                skipped = true;
                return false;
            }

            p.Set(value ?? string.Empty);
            return true;
        }

        // Instance parameter only - same reason as SetTextOnElement.
        private static bool SetDoubleOnElement(Element el, string paramName, double value, bool overwrite, ProcessResult result, out bool skipped)
        {
            skipped = false;
            bool wrote = false;
            bool hasInstanceParameter = false;

            foreach (Parameter p in el.GetParameters(paramName))
            {
                hasInstanceParameter = true;
                if (SetDouble(p, value, overwrite, out bool s)) wrote = true;
                else if (s) skipped = true;
            }

            if (!hasInstanceParameter)
                NoteTypeOnlyParameter(el, paramName, result);

            return wrote;
        }

        private static bool SetDouble(Parameter p, double value, bool overwrite, out bool skipped)
        {
            skipped = false;
            if (p == null || p.IsReadOnly || p.StorageType != StorageType.Double)
                return false;

            if (!overwrite && p.HasValue && Math.Abs(p.AsDouble()) > 1e-9)
            {
                skipped = true;
                return false;
            }

            p.Set(value);
            return true;
        }
        private static XYZ GetSurveyPoint(Document doc)
        {
            try
            {
                // OfClass(BasePoint) returns BOTH the Project Base Point and the Survey Point, in no
                // guaranteed order; taking the first could read the project base point and offset every
                // shared coordinate by it. The survey point is the one that is shared.
                BasePoint survey = new FilteredElementCollector(doc)
                    .OfClass(typeof(BasePoint))
                    .WhereElementIsNotElementType()
                    .Cast<BasePoint>()
                    .FirstOrDefault(bp => bp.IsShared);

                if (survey == null)
                    return XYZ.Zero;

                double ew = survey.get_Parameter(BuiltInParameter.BASEPOINT_EASTWEST_PARAM)?.AsDouble() ?? 0.0;
                double ns = survey.get_Parameter(BuiltInParameter.BASEPOINT_NORTHSOUTH_PARAM)?.AsDouble() ?? 0.0;
                double el = survey.get_Parameter(BuiltInParameter.BASEPOINT_ELEVATION_PARAM)?.AsDouble() ?? 0.0;
                return new XYZ(ew, ns, el);
            }
            catch (Exception)
            {
                return XYZ.Zero;
            }
        }

        private static Transform GetInternalToShared(Document doc)
        {
            try { return doc.ActiveProjectLocation?.GetTransform() ?? Transform.Identity; }
            catch (Exception) { return Transform.Identity; }
        }

        private void RefuseCloseWhileBusy(object sender, CancelEventArgs e)
        {
            if (_isBusy)
            {
                e.Cancel = true;
            }
        }

        private void SetBusy(bool busy, string msg)
        {
            _isBusy = busy;

            if (busy)
            {
                ProgressPanel.Visibility = System.Windows.Visibility.Visible;
                ProgressText.Text = msg ?? "Working...";

                CheckParametersButton.IsEnabled = false;
                CreateParametersButton.IsEnabled = false;
                ProcessButton.IsEnabled = false;
                CancelButton.IsEnabled = false;
                SelectAllCategoriesButton.IsEnabled = false;
                SelectNoneCategoriesButton.IsEnabled = false;
                CategorySearchBox.IsEnabled = false;
                CategoriesListBox.IsEnabled = false;
                IncludeLevelsCheckBox.IsEnabled = false;
                IncludeCoordinatesCheckBox.IsEnabled = false;
                IncludeAltitudeCheckBox.IsEnabled = false;
                IncludeHvacCheckBox.IsEnabled = false;
                OverwriteCheckBox.IsEnabled = false;
                UseSharedCoordinatesCheckBox.IsEnabled = false;
                DebugCheckBox.IsEnabled = false;
                UseLinkedModelCheckBox.IsEnabled = false;
                LinkedModelsComboBox.IsEnabled = false;
                txtParamRoomName.IsEnabled = false;
                txtParamRoomNumber.IsEnabled = false;
                txtParamLevelName.IsEnabled = false;
                txtParamEasting.IsEnabled = false;
                txtParamNorthing.IsEnabled = false;
                txtParamAltitude.IsEnabled = false;
                txtParamHvacZoneName.IsEnabled = false;
                txtParamHvacZoneNumber.IsEnabled = false;
                return;
            }

            ProgressPanel.Visibility = System.Windows.Visibility.Collapsed;
            CancelButton.IsEnabled = true;
            SelectAllCategoriesButton.IsEnabled = true;
            SelectNoneCategoriesButton.IsEnabled = true;
            CategorySearchBox.IsEnabled = true;
            CategoriesListBox.IsEnabled = true;
            IncludeLevelsCheckBox.IsEnabled = true;
            IncludeCoordinatesCheckBox.IsEnabled = true;
            IncludeAltitudeCheckBox.IsEnabled = true;
            IncludeHvacCheckBox.IsEnabled = true;
            OverwriteCheckBox.IsEnabled = true;
            UseSharedCoordinatesCheckBox.IsEnabled = true;
            DebugCheckBox.IsEnabled = true;
            UseLinkedModelCheckBox.IsEnabled = _links.Count > 0;
            LinkedModelsComboBox.IsEnabled = UseLinkedModelCheckBox.IsChecked == true && _links.Count > 0;
            CheckParametersButton.IsEnabled = true;
            txtParamRoomName.IsEnabled = true;
            txtParamRoomNumber.IsEnabled = true;
            txtParamLevelName.IsEnabled = true;
            txtParamEasting.IsEnabled = true;
            txtParamNorthing.IsEnabled = true;
            txtParamAltitude.IsEnabled = true;
            txtParamHvacZoneName.IsEnabled = true;
            txtParamHvacZoneNumber.IsEnabled = true;
        }

        private void UpdateProgress(int done, int total)
        {
            ProcessProgressBar.Value = Math.Min(done, ProcessProgressBar.Maximum);
            ProgressText.Text = string.Format("Processing... {0}/{1}", done, total);

            if (done % 25 == 0 || done == total)
            {
                // Render priority repaints without processing input (Input sits below Render), so a
                // click cannot re-enter the run half way through. The X / Alt+F4 still arrive through
                // the nested message loop - RefuseCloseWhileBusy handles those.
                Dispatcher.Invoke(DispatcherPriority.Render, new Action(delegate { }));
            }
        }

        private static void ShowSummary(ProcessResult result, bool includeDebug)
        {
            var dialog = new TaskDialog(DialogTitle)
            {
                MainInstruction = "Location data assignment finished.",
                MainContent =
                    "Updated: " + result.Updated +
                    "\nSkipped: " + result.Skipped +
                    "\nErrors: " + result.Errors,
                CommonButtons = TaskDialogCommonButtons.Ok
            };

            if (result.TypeOnlyElementIds.Count > 0)
            {
                dialog.MainContent +=
                    "\n\n" + result.TypeOnlyElementIds.Count + " element(s) have " +
                    string.Join(", ", result.TypeOnlyParameterNames) +
                    " only as a type parameter. Those values were not filled: one type is shared by many " +
                    "elements, so a per-element room, level, zone or coordinate cannot be stored there. " +
                    "Bind these parameters as instance parameters to fill them.";
            }

            if (result.Notes.Count > 0)
            {
                dialog.MainContent += "\n\nNotes:\n- " + string.Join("\n- ", result.Notes);
            }

            if (includeDebug && result.Reasons.Count > 0)
            {
                dialog.ExpandedContent = string.Join(
                    "\n",
                    result.Reasons
                        .OrderByDescending(p => p.Value)
                        .ThenBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                        .Take(20)
                        .Select(p => p.Key + ": " + p.Value));
            }

            dialog.Show();
        }

        private static void AddReason(ProcessResult result, bool debug, string reason)
        {
            if (!debug || string.IsNullOrWhiteSpace(reason))
                return;

            if (result.Reasons.ContainsKey(reason))
                result.Reasons[reason]++;
            else
                result.Reasons[reason] = 1;
        }

        private sealed class ParamSpec
        {
            public ParamSpec(string name, AjSpec parameterType, AjGroup parameterGroup)
            {
                Name = name;
                ParameterType = parameterType;
                ParameterGroup = parameterGroup;
            }

            public string Name { get; }
            public AjSpec ParameterType { get; }
            public AjGroup ParameterGroup { get; }
        }

        private sealed class ProcessOptions
        {
            public bool IncludeLevels { get; set; }
            public bool IncludeCoords { get; set; }
            public bool IncludeAltitude { get; set; }
            public bool IncludeHvac { get; set; }
            public bool OverwriteText { get; set; }
            public bool UseSharedCoords { get; set; }
            public bool UseLinkedSource { get; set; }
            public bool Debug { get; set; }
            public LocationParameterMap ParameterMap { get; set; }
        }

        private sealed class LocationParameterMap
        {
            public string RoomName { get; set; }
            public string RoomNumber { get; set; }
            public string LevelName { get; set; }
            public string Easting { get; set; }
            public string Northing { get; set; }
            public string Altitude { get; set; }
            public string HvacZoneName { get; set; }
            public string HvacZoneNumber { get; set; }
        }

        private sealed class ProcessResult
        {
            public int Updated;
            public int Skipped;
            public int Errors;
            public readonly Dictionary<string, int> Reasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly List<string> Notes = new List<string>();

            // Elements where a mapped parameter exists only on the element's type, so it was not filled.
            public readonly HashSet<ElementId> TypeOnlyElementIds = new HashSet<ElementId>();
            public readonly SortedSet<string> TypeOnlyParameterNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class BindResult
        {
            public readonly List<string> Created = new List<string>();
            public readonly List<string> Updated = new List<string>();
            public readonly List<string> Failed = new List<string>();
        }

        private sealed class LinkItem
        {
            public LinkItem(string name, RevitLinkInstance link)
            {
                Name = name;
                Link = link;
            }

            public string Name { get; }
            public RevitLinkInstance Link { get; }

            public override string ToString()
            {
                return Name ?? base.ToString();
            }
        }

        private sealed class CategoryItem : INotifyPropertyChanged
        {
            private bool _isChecked;

            public CategoryItem(Category category, int count)
            {
                Category = category;
                Name = category?.Name ?? string.Empty;
                Count = count;
            }

            public Category Category { get; }
            public string Name { get; }
            public int Count { get; }

            public bool IsChecked
            {
                get => _isChecked;
                set
                {
                    if (_isChecked == value)
                        return;

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
