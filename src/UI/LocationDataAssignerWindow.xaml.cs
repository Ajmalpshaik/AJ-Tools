// Tool Name: Location Data Assigner UI
// Description: Code-behind for assigning room, level, coordinate, altitude, and HVAC zone data.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-04-09
// Revit Version: 2020

using AJTools.Utils;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
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
            CreateParametersButton.IsEnabled = hasParamSpecs;
            CheckParametersButton.IsEnabled = hasParamSpecs;

            CreateParametersButton.Content = "Create/Rebind Parameters";

            if (!hasCategories)
                StatusText.Text = "Select at least one category.";
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

            string list = string.Join("\n", specs.Select(s => "- " + s.Name));
            if (!DialogHelper.ShowYesNo(DialogTitle, "The following parameters will be created/rebound:\n\n" + list + "\n\nContinue?"))
                return;

            try
            {
                SetBusy(true, "Creating parameters...");
                int count;
                using (var t = new Transaction(_doc, "AJ Tools - Create Location Data Parameters"))
                {
                    t.Start();
                    count = CreateParameters(specs, _allCategories.Where(c => c.IsChecked).ToList());
                    t.Commit();
                }

                DialogHelper.ShowInfo(DialogTitle, "Parameters processed: " + count);
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(DialogTitle, "Failed to create parameters:\n\n" + ex.Message);
            }
            finally
            {
                SetBusy(false, null);
                UpdateButtons();
            }
        }

        private static List<ParamSpec> GetSpecs(LocationParameterMap map, bool includeLevels, bool includeCoords, bool includeAltitude, bool includeHvac)
        {
            map = map ?? CreateDefaultParameterMap();
            var specs = new List<ParamSpec>();
            AddSpec(specs, map.RoomName, ParameterType.Text, BuiltInParameterGroup.PG_DATA);
            AddSpec(specs, map.RoomNumber, ParameterType.Text, BuiltInParameterGroup.PG_DATA);

            if (includeLevels)
            {
                AddSpec(specs, map.LevelName, ParameterType.Text, BuiltInParameterGroup.PG_DATA);
            }

            if (includeCoords)
            {
                AddSpec(specs, map.Easting, ParameterType.Length, BuiltInParameterGroup.PG_DATA);
                AddSpec(specs, map.Northing, ParameterType.Length, BuiltInParameterGroup.PG_DATA);
            }

            if (includeAltitude)
                AddSpec(specs, map.Altitude, ParameterType.Length, BuiltInParameterGroup.PG_DATA);

            if (includeHvac)
            {
                AddSpec(specs, map.HvacZoneName, ParameterType.Text, BuiltInParameterGroup.PG_DATA);
                AddSpec(specs, map.HvacZoneNumber, ParameterType.Text, BuiltInParameterGroup.PG_DATA);
            }

            return specs;
        }

        private static void AddSpec(ICollection<ParamSpec> specs, string name, ParameterType type, BuiltInParameterGroup group)
        {
            if (specs == null || string.IsNullOrWhiteSpace(name))
                return;

            string clean = name.Trim();
            if (specs.Any(s => string.Equals(s.Name, clean, StringComparison.OrdinalIgnoreCase)))
                return;

            specs.Add(new ParamSpec(clean, type, group));
        }

        private int CreateParameters(IList<ParamSpec> specs, IList<CategoryItem> selectedCategories)
        {
            var app = _doc.Application;
            string originalPath = app.SharedParametersFilename;
            bool tempAssigned = false;

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

                CategorySet categorySet = app.Create.NewCategorySet();
                int catCount = FillCategorySet(categorySet, selectedCategories);
                if (catCount == 0)
                    throw new InvalidOperationException("No valid categories available for binding.");

                int success = 0;
                BindingMap map = _doc.ParameterBindings;

                foreach (var spec in specs)
                {
                    Definition definition = FindDefinition(map, spec.Name) ?? GetOrCreateDefinition(file, group, spec.Name, spec.ParameterType);
                    if (definition == null)
                        continue;

                    Binding binding = app.Create.NewInstanceBinding(categorySet);
                    bool hasExisting = FindDefinition(map, spec.Name) != null;
                    bool ok = hasExisting
                        ? map.ReInsert(definition, binding, spec.ParameterGroup)
                        : map.Insert(definition, binding, spec.ParameterGroup);

                    if (!ok && hasExisting)
                    {
                        try { map.Remove(definition); } catch (Exception) { }
                        ok = map.Insert(definition, binding, spec.ParameterGroup);
                    }

                    if (ok)
                        success++;
                }

                return success;
            }
            finally
            {
                if (tempAssigned)
                    app.SharedParametersFilename = originalPath ?? string.Empty;
            }
        }

        private int FillCategorySet(CategorySet set, IList<CategoryItem> selected)
        {
            int count = 0;
            var ids = new HashSet<int>();

            if (selected != null)
            {
                foreach (var item in selected)
                {
                    var c = item?.Category;
                    if (c == null || c.CategoryType != CategoryType.Model || !c.AllowsBoundParameters)
                        continue;

                    if (ids.Add(c.Id.IntegerValue))
                    {
                        set.Insert(c);
                        count++;
                    }
                }
            }

            if (count > 0)
                return count;

            var defaults = new HashSet<string>(PriorityCategoryNames, StringComparer.OrdinalIgnoreCase);
            foreach (Category c in _doc.Settings.Categories)
            {
                if (c == null || c.CategoryType != CategoryType.Model || !c.AllowsBoundParameters)
                    continue;
                if (!defaults.Contains(c.Name))
                    continue;

                if (ids.Add(c.Id.IntegerValue))
                {
                    set.Insert(c);
                    count++;
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

        private static Definition GetOrCreateDefinition(DefinitionFile file, DefinitionGroup preferredGroup, string name, ParameterType type)
        {
            foreach (DefinitionGroup group in file.Groups)
            {
                var existing = TryGetDefinition(group, name);
                if (existing != null)
                    return existing;
            }

            var options = new ExternalDefinitionCreationOptions(name, type)
            {
                UserModifiable = true,
                Visible = true
            };

            try { return preferredGroup.Definitions.Create(options); }
            catch (Exception) { return TryGetDefinition(preferredGroup, name); }
        }

        private static Definition TryGetDefinition(DefinitionGroup group, string name)
        {
            try { return group.Definitions.get_Item(name); }
            catch (Exception) { return null; }
        }

        private static Definition FindDefinition(BindingMap map, string name)
        {
            var it = map.ForwardIterator();
            while (it.MoveNext())
            {
                var def = it.Key;
                if (def != null && string.Equals(def.Name, name, StringComparison.OrdinalIgnoreCase))
                    return def;
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

            try
            {
                SetBusy(true, "Preparing data...");
                var result = RunProcess(options, selectedCats);
                ShowSummary(result, options.Debug);
            }
            catch (Exception ex)
            {
                DialogHelper.ShowError(DialogTitle, "Processing failed:\n\n" + ex.Message);
            }
            finally
            {
                SetBusy(false, null);
                UpdateButtons();
            }
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

            using (var t = new Transaction(_doc, "AJ Tools - Assign Location Data"))
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
                                wroteAny |= WriteHvac(el, spaces, queryPt, options, result);

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

                t.Commit();
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

            bool wroteName = SetTextOnElementAndType(el, map.RoomName, name, options.OverwriteText, out bool skip1);
            bool wroteNumber = SetTextOnElementAndType(el, map.RoomNumber, number, options.OverwriteText, out bool skip2);
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

            bool wroteName = SetTextOnElementAndType(el, map.LevelName, name, options.OverwriteText, out bool skip);
            if (!wroteName && skip)
                AddReason(result, options.Debug, "Overwrite off");

            return wroteName;
        }

        private static bool WriteHvac(Element el, IList<Space> spaces, XYZ point, ProcessOptions options, ProcessResult result)
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

            Zone zone = null;
            try { zone = space.Zone; } catch (Exception) { zone = null; }
            if (zone == null)
            {
                AddReason(result, options.Debug, "No HVAC zone");
                return false;
            }

            string zoneName = ReadString(zone, "Name", "Zone Name");
            string zoneNumber = ReadString(zone, "Number", "Zone Number");

            bool wroteName = hasNameParam && !string.IsNullOrWhiteSpace(zoneName) &&
                             SetTextOnElementAndType(el, map.HvacZoneName, zoneName, options.OverwriteText, out bool _);
            bool wroteNumber = hasNumberParam && !string.IsNullOrWhiteSpace(zoneNumber) &&
                               SetTextOnElementAndType(el, map.HvacZoneNumber, zoneNumber, options.OverwriteText, out bool _);
            return wroteName || wroteNumber;
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

                bool e = SetDoubleOnElementAndType(el, map.Easting, c.X, options.OverwriteText, out bool _);
                bool n = SetDoubleOnElementAndType(el, map.Northing, c.Y, options.OverwriteText, out bool _);
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

                bool a = SetDoubleOnElementAndType(el, map.Altitude, c.Z, options.OverwriteText, out bool _);
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

        private static bool SetTextOnElementAndType(Element el, string paramName, string value, bool overwrite, out bool skipped)
        {
            skipped = false;
            bool wrote = false;

            foreach (Parameter p in el.GetParameters(paramName))
            {
                if (SetText(p, value, overwrite, out bool s)) wrote = true;
                else if (s) skipped = true;
            }

            ElementId typeId = el.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                Element type = el.Document.GetElement(typeId);
                if (type != null)
                {
                    foreach (Parameter p in type.GetParameters(paramName))
                    {
                        if (SetText(p, value, overwrite, out bool s)) wrote = true;
                        else if (s) skipped = true;
                    }
                }
            }

            return wrote;
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

        private static bool SetDoubleOnElementAndType(Element el, string paramName, double value, bool overwrite, out bool skipped)
        {
            skipped = false;
            bool wrote = false;

            foreach (Parameter p in el.GetParameters(paramName))
            {
                if (SetDouble(p, value, overwrite, out bool s)) wrote = true;
                else if (s) skipped = true;
            }

            ElementId typeId = el.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                Element type = el.Document.GetElement(typeId);
                if (type != null)
                {
                    foreach (Parameter p in type.GetParameters(paramName))
                    {
                        if (SetDouble(p, value, overwrite, out bool s)) wrote = true;
                        else if (s) skipped = true;
                    }
                }
            }

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
                Element survey = new FilteredElementCollector(doc)
                    .OfClass(typeof(BasePoint))
                    .WhereElementIsNotElementType()
                    .FirstElement();

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
                Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { }));
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
            public ParamSpec(string name, ParameterType parameterType, BuiltInParameterGroup parameterGroup)
            {
                Name = name;
                ParameterType = parameterType;
                ParameterGroup = parameterGroup;
            }

            public string Name { get; }
            public ParameterType ParameterType { get; }
            public BuiltInParameterGroup ParameterGroup { get; }
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
