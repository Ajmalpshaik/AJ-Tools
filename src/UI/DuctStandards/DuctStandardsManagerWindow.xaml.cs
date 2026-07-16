using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using AJTools.Models.DuctStandards;
using AJTools.Services.DuctStandards;
using AJTools.Utils;
#if REVIT2022_OR_GREATER
using AjSpec = Autodesk.Revit.DB.ForgeTypeId;
using AjGroup = Autodesk.Revit.DB.ForgeTypeId;
#else
using AjSpec = Autodesk.Revit.DB.ParameterType;
using AjGroup = Autodesk.Revit.DB.BuiltInParameterGroup;
#endif

namespace AJTools.UI.DuctStandards
{
    public partial class DuctStandardsManagerWindow : Window
    {
        private const string DialogTitle = "Duct Standards Manager";
        private const string SharedParamGroupName = "DuctStandards";

        private readonly UIDocument _uidoc;
        private readonly Document _doc;
        private DuctStandardsConfig _config;
        private DuctProcessingReport _lastReport;

        private ObservableCollection<MaterialInfo> _materials;
        private ObservableCollection<DuctRule> _allRules;
        private ObservableCollection<DuctRule> _filteredRules;

        public DuctStandardsManagerWindow(UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc = uidoc.Document;
            InitializeComponent();
            LoadConfigAndPopulateUI();
        }

        // -------------------------------------------------------------------
        // Config <-> UI
        // -------------------------------------------------------------------

        private void LoadConfigAndPopulateUI()
        {
            _config = DuctStandardsConfigService.Load();
            PopulateUI();
        }

        private void PopulateUI()
        {
            var g = _config.General ?? new GeneralSettings();

            // General
            txtStandardName.Text = g.StandardName;
            txtEdition.Text = g.Edition;
            SetComboText(cboMode, g.Mode);
            chkWriteToRevit.IsChecked = g.WriteToRevit;
            chkIncludeAllowances.IsChecked = g.IncludeAllowances;
            chkWriteRuleSource.IsChecked = g.WriteRuleSource;
            chkSkipMissing.IsChecked = g.SkipIfMissingRequiredParameter;

            // Materials
            _materials = new ObservableCollection<MaterialInfo>(_config.Materials ?? new List<MaterialInfo>());
            dgMaterials.ItemsSource = _materials;

            // Default material / pressure combos
            cboDefaultMaterial.Items.Clear();
            foreach (var m in _materials)
                cboDefaultMaterial.Items.Add(m.Name);
            SetComboText(cboDefaultMaterial, _config.DefaultMaterial);

            cboDefaultPressure.Items.Clear();
            foreach (var pc in _config.PressureClasses ?? new List<string> { "low", "medium", "high" })
                cboDefaultPressure.Items.Add(pc);
            SetComboText(cboDefaultPressure, _config.DefaultPressureClass);

            // Rules
            _allRules = new ObservableCollection<DuctRule>(_config.Rules ?? new List<DuctRule>());
            ApplyRuleFilter();

            // Allowances
            var a = _config.Allowances ?? new AllowanceSettings();
            txtSeam.Text = a.SeamPercent.ToString("F1");
            txtJoint.Text = a.JointPercent.ToString("F1");
            txtFlange.Text = a.FlangePercent.ToString("F1");
            txtReinforcement.Text = a.ReinforcementPercent.ToString("F1");
            txtFittings.Text = a.FittingsPercent.ToString("F1");
            txtWastage.Text = a.WastagePercent.ToString("F1");

            // Parameter Map
            var pm = _config.ParameterMap ?? new DuctParameterMap();
            txtParamThickness.Text = pm.SheetThickness;
            txtParamGauge.Text = pm.Gauge;
            txtParamWeightPerMeter.Text = pm.WeightPerMeter;
            txtParamTotalWeight.Text = pm.TotalWeight;
            txtParamSheetArea.Text = pm.SheetArea;
            txtParamRuleSource.Text = pm.RuleSource;
            txtParamPressureClass.Text = pm.PressureClass;
            txtParamMaterialName.Text = pm.MaterialName;

            // Config path
            txtConfigPath.Text = "Config: " + DuctStandardsConfigService.GetConfigFilePath();
        }

        private DuctStandardsConfig ReadUIToConfig()
        {
            var config = new DuctStandardsConfig();

            config.General = new GeneralSettings
            {
                StandardName = txtStandardName.Text,
                Edition = txtEdition.Text,
                Mode = GetComboText(cboMode) ?? "editable",
                WriteToRevit = chkWriteToRevit.IsChecked == true,
                IncludeAllowances = chkIncludeAllowances.IsChecked == true,
                WriteRuleSource = chkWriteRuleSource.IsChecked == true,
                SkipIfMissingRequiredParameter = chkSkipMissing.IsChecked == true
            };

            config.Materials = _materials.ToList();
            config.DefaultMaterial = GetComboText(cboDefaultMaterial) ?? "GI";
            config.DefaultPressureClass = GetComboText(cboDefaultPressure) ?? "low";
            config.PressureClasses = new List<string> { "low", "medium", "high" };

            config.Rules = _allRules.ToList();

            config.Allowances = new AllowanceSettings
            {
                SeamPercent = ParseDouble(txtSeam.Text, 3.0),
                JointPercent = ParseDouble(txtJoint.Text, 2.0),
                FlangePercent = ParseDouble(txtFlange.Text, 4.0),
                ReinforcementPercent = ParseDouble(txtReinforcement.Text, 5.0),
                FittingsPercent = ParseDouble(txtFittings.Text, 10.0),
                WastagePercent = ParseDouble(txtWastage.Text, 5.0)
            };

            config.ParameterMap = new DuctParameterMap
            {
                SheetThickness = txtParamThickness.Text,
                Gauge = txtParamGauge.Text,
                WeightPerMeter = txtParamWeightPerMeter.Text,
                TotalWeight = txtParamTotalWeight.Text,
                SheetArea = txtParamSheetArea.Text,
                RuleSource = txtParamRuleSource.Text,
                PressureClass = txtParamPressureClass.Text,
                MaterialName = txtParamMaterialName.Text
            };

            return config;
        }

        // -------------------------------------------------------------------
        // Parameter Tools
        // -------------------------------------------------------------------

        private void BtnCheckParameters_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _config = ReadUIToConfig();
                var missing = GetMissingMappedParameterNames(_config);

                if (missing.Count == 0)
                {
                    MessageBox.Show(
                        "All mapped parameters are available on this project.",
                        DialogTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        "Missing parameter bindings:\n\n- " + string.Join("\n- ", missing) +
                        "\n\nUse Create/Rebind Parameters to add them for Ducts.",
                        DialogTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Parameter check failed: " + ex.Message, DialogTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCreateParameters_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _config = ReadUIToConfig();
                var specs = BuildCreateOrRebindSpecs(_config);

                if (specs.Count == 0)
                {
                    MessageBox.Show(
                        "No parameter create/rebind action is required for the current mapping.",
                        DialogTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                string list = string.Join("\n", specs.Select(s => "- " + s.Name + " (" + SharedParamUtils.GetParameterTypeLabel(s.ParameterType) + ")"));
                var confirmation = MessageBox.Show(
                    "The following parameters will be created or rebound to Ducts:\n\n" + list + "\n\nContinue?",
                    DialogTitle,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmation != MessageBoxResult.Yes)
                    return;

                int processed = 0;
                string errorMsg;
                bool success = TransactionHelper.ExecuteSafe(
                    _doc,
                    "Create Duct Standards Parameters",
                    () =>
                    {
                        processed = CreateOrRebindParameters(specs);
                    },
                    out errorMsg);

                if (!success)
                {
                    MessageBox.Show("Parameter create/rebind failed: " + errorMsg, DialogTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var missingAfter = GetMissingMappedParameterNames(_config);
                if (missingAfter.Count == 0)
                {
                    MessageBox.Show(
                        "Parameters processed: " + processed + "\nAll mapped parameters are now available.",
                        DialogTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        "Parameters processed: " + processed +
                        "\n\nStill missing:\n- " + string.Join("\n- ", missingAfter),
                        DialogTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Parameter create/rebind failed: " + ex.Message, DialogTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<string> GetMissingMappedParameterNames(DuctStandardsConfig config)
        {
            var existing = GetBoundParamNames(_doc);
            var required = BuildMappedParameterSpecs(config);
            Element sampleDuct = GetAnyDuctElement();

            return required
                .Where(s =>
                {
                    if (existing.Contains(s.Name))
                        return false;

                    return !HasParameterOnElementOrType(sampleDuct, s.Name);
                })
                .Select(s => s.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private List<DuctParamSpec> BuildMappedParameterSpecs(DuctStandardsConfig config)
        {
            var specs = new List<DuctParamSpec>();
            var map = config?.ParameterMap ?? new DuctParameterMap();

            AddSpec(specs, map.SheetThickness, RevitCompat.SpecNumber, RevitCompat.GroupData);
            AddSpec(specs, map.Gauge, RevitCompat.SpecText, RevitCompat.GroupData);
            AddSpec(specs, map.WeightPerMeter, RevitCompat.SpecNumber, RevitCompat.GroupData);
            AddSpec(specs, map.TotalWeight, RevitCompat.SpecNumber, RevitCompat.GroupData);
            AddSpec(specs, map.SheetArea, RevitCompat.SpecNumber, RevitCompat.GroupData);
            AddSpec(specs, map.RuleSource, RevitCompat.SpecText, RevitCompat.GroupData);
            AddSpec(specs, map.PressureClass, RevitCompat.SpecText, RevitCompat.GroupData);
            AddSpec(specs, map.MaterialName, RevitCompat.SpecText, RevitCompat.GroupData);

            return specs;
        }

        private List<DuctParamSpec> BuildCreateOrRebindSpecs(DuctStandardsConfig config)
        {
            var mapped = BuildMappedParameterSpecs(config);
            if (mapped.Count == 0)
                return mapped;

            var missing = new HashSet<string>(GetMissingMappedParameterNames(config), StringComparer.OrdinalIgnoreCase);
            BindingMap map = _doc.ParameterBindings;

            return mapped
                .Where(s => missing.Contains(s.Name) || FindDefinition(map, s.Name) != null)
                .ToList();
        }

        private static void AddSpec(List<DuctParamSpec> specs, string name, AjSpec parameterType, AjGroup group)
        {
            if (specs == null || string.IsNullOrWhiteSpace(name))
                return;

            string clean = name.Trim();
            if (specs.Any(s => string.Equals(s.Name, clean, StringComparison.OrdinalIgnoreCase)))
                return;

            specs.Add(new DuctParamSpec(clean, parameterType, group));
        }

        private int CreateOrRebindParameters(IList<DuctParamSpec> specs)
        {
            if (specs == null || specs.Count == 0)
                return 0;

            var app = _doc.Application;
            string originalPath = app.SharedParametersFilename;
            bool tempAssigned = false;

            try
            {
                DefinitionFile file = app.OpenSharedParameterFile();
                if (file == null)
                {
                    string tempPath = Path.Combine(Path.GetTempPath(), "AJTools_DuctStandards_SharedParams.txt");
                    if (!File.Exists(tempPath))
                        File.WriteAllText(tempPath, string.Empty);

                    app.SharedParametersFilename = tempPath;
                    tempAssigned = true;
                    file = app.OpenSharedParameterFile();
                }

                if (file == null)
                    throw new InvalidOperationException("Shared parameter file could not be opened.");

                var group = GetOrCreateGroup(file, SharedParamGroupName);
                CategorySet categorySet = BuildDuctCategorySet(app);
                BindingMap map = _doc.ParameterBindings;

                int success = 0;
                foreach (var spec in specs)
                {
                    Definition existing = FindDefinition(map, spec.Name);
                    Definition definition = existing ?? GetOrCreateDefinition(file, group, spec.Name, spec.ParameterType);
                    if (definition == null)
                        continue;

                    Binding binding = app.Create.NewInstanceBinding(categorySet);
                    bool hasExisting = existing != null;
                    bool ok = hasExisting
                        ? RevitCompat.ReInsertBinding(map, definition, binding, spec.ParameterGroup)
                        : RevitCompat.InsertBinding(map, definition, binding, spec.ParameterGroup);

                    if (!ok && hasExisting)
                    {
                        try { map.Remove(definition); } catch (Exception) { }
                        ok = RevitCompat.InsertBinding(map, definition, binding, spec.ParameterGroup);
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

        private CategorySet BuildDuctCategorySet(Autodesk.Revit.ApplicationServices.Application app)
        {
            var set = app.Create.NewCategorySet();
            Category ductCategory = _doc.Settings.Categories.get_Item(BuiltInCategory.OST_DuctCurves);

            if (ductCategory == null || !ductCategory.AllowsBoundParameters)
                throw new InvalidOperationException("Ducts category is not available for parameter binding.");

            set.Insert(ductCategory);
            return set;
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

        private Element GetAnyDuctElement()
        {
            try
            {
                return new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_DuctCurves)
                    .WhereElementIsNotElementType()
                    .FirstElement();
            }
            catch
            {
                return null;
            }
        }

        private bool HasParameterOnElementOrType(Element element, string parameterName)
        {
            if (element == null || string.IsNullOrWhiteSpace(parameterName))
                return false;

            try
            {
                if (element.LookupParameter(parameterName) != null)
                    return true;

                ElementId typeId = element.GetTypeId();
                if (typeId == ElementId.InvalidElementId)
                    return false;

                Element type = _doc.GetElement(typeId);
                return type != null && type.LookupParameter(parameterName) != null;
            }
            catch
            {
                return false;
            }
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

        // -------------------------------------------------------------------
        // Processing
        // -------------------------------------------------------------------

        private void BtnRunProcess_Click(object sender, RoutedEventArgs e)
        {
            RunProcessing(writeToRevit: true);
        }

        private void BtnPreviewCalc_Click(object sender, RoutedEventArgs e)
        {
            RunProcessing(writeToRevit: false);
        }

        private void RunProcessing(bool writeToRevit)
        {
            try
            {
                _config = ReadUIToConfig();

                // Override write setting for preview
                var configToUse = _config;
                if (!writeToRevit)
                {
                    configToUse = ReadUIToConfig();
                    configToUse.General.WriteToRevit = false;
                }

                if (writeToRevit)
                {
                    var missing = GetMissingMappedParameterNames(configToUse);
                    if (missing.Count > 0)
                    {
                        txtResults.Text =
                            "Cannot write values because mapped parameters are missing:\n\n- " +
                            string.Join("\n- ", missing) +
                            "\n\nOpen Parameters tab and click Create/Rebind Parameters.";
                        return;
                    }
                }

                var ducts = CollectDucts();
                if (ducts == null || ducts.Count == 0)
                {
                    txtResults.Text = "No ducts found for the selected processing mode.";
                    return;
                }

                DuctProcessingReport report = null;
                string errorMsg;

                bool success = TransactionHelper.ExecuteSafe(
                    _doc,
                    "Duct Standards Manager",
                    () =>
                    {
                        report = DuctStandardsProcessor.Process(ducts, configToUse, _doc);
                    },
                    out errorMsg);

                if (!success)
                {
                    txtResults.Text = "Transaction failed: " + errorMsg;
                    return;
                }

                _lastReport = report;
                btnExportCsv.IsEnabled = true;

                string mode = writeToRevit ? "PROCESSED" : "PREVIEW (no write)";
                txtResults.Text = string.Format("=== {0} ===\n\n{1}", mode, report.ToSummaryText());

                // Append first few detail lines
                int showCount = Math.Min(report.Results.Count, 50);
                txtResults.Text += "\n\n--- First " + showCount + " Details ---\n";
                for (int i = 0; i < showCount; i++)
                {
                    var r = report.Results[i];
                    if (r.Success)
                    {
                        txtResults.Text += string.Format(
                            "ID {0}: {1} | {2} mm | {3} | t={4} mm | ga={5} | area={6:F4} m2 | wt/m={7:F4} kg | total={8:F4} kg\n",
                            r.ElementId, r.Shape, r.GoverningSize_mm, r.PressureClass,
                            r.ThicknessMm, r.Gauge, r.SheetArea_m2, r.WeightPerMeter_kg, r.TotalWeight_kg);
                    }
                    else
                    {
                        txtResults.Text += string.Format("ID {0}: SKIP/FAIL - {1}\n", r.ElementId, r.ErrorMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                txtResults.Text = "Error: " + ex.Message;
            }
        }

        private List<Element> CollectDucts()
        {
            if (rbSelectedDucts.IsChecked == true)
                return DuctCollectorService.GetSelectedDucts(_uidoc);
            if (rbActiveView.IsChecked == true)
                return DuctCollectorService.GetActiveViewDucts(_doc);
            if (rbWholeProject.IsChecked == true)
                return DuctCollectorService.GetProjectDucts(_doc);
            return new List<Element>();
        }

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_lastReport == null) return;

            var dlg = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = "DuctStandardsReport.csv"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dlg.FileName, _lastReport.ToCsvText());
                    txtResults.Text += "\n\nCSV exported: " + dlg.FileName;
                }
                catch (Exception ex)
                {
                    txtResults.Text += "\n\nCSV export failed: " + ex.Message;
                }
            }
        }

        // -------------------------------------------------------------------
        // Materials Grid
        // -------------------------------------------------------------------

        private void BtnAddMaterial_Click(object sender, RoutedEventArgs e)
        {
            _materials.Add(new MaterialInfo { Name = "New Material", DensityKgM3 = 7850.0 });
        }

        private void BtnRemoveMaterial_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgMaterials.SelectedItem as MaterialInfo;
            if (selected != null)
                _materials.Remove(selected);
        }

        // -------------------------------------------------------------------
        // Rules Grid
        // -------------------------------------------------------------------

        private void BtnAddRule_Click(object sender, RoutedEventArgs e)
        {
            var rule = new DuctRule
            {
                Shape = "rectangular",
                Pressure = "low",
                MinMm = 0,
                MaxMm = 400,
                ThicknessMm = 0.60,
                Gauge = "26",
                Reinforcement = false
            };
            _allRules.Add(rule);
            ApplyRuleFilter();
        }

        private void BtnRemoveRule_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgRules.SelectedItem as DuctRule;
            if (selected != null)
            {
                _allRules.Remove(selected);
                ApplyRuleFilter();
            }
        }

        private void CboFilterShape_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyRuleFilter();
        }

        private void CboFilterPressure_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyRuleFilter();
        }

        private void ApplyRuleFilter()
        {
            if (_allRules == null) return;

            string shapeFilter = GetComboText(cboFilterShape);
            string pressureFilter = GetComboText(cboFilterPressure);

            var filtered = _allRules.AsEnumerable();

            if (!string.IsNullOrEmpty(shapeFilter) && shapeFilter != "All")
                filtered = filtered.Where(r => string.Equals(r.Shape, shapeFilter, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(pressureFilter) && pressureFilter != "All")
                filtered = filtered.Where(r => string.Equals(r.Pressure, pressureFilter, StringComparison.OrdinalIgnoreCase));

            _filteredRules = new ObservableCollection<DuctRule>(filtered);
            dgRules.ItemsSource = _filteredRules;
        }

        // -------------------------------------------------------------------
        // Config Buttons
        // -------------------------------------------------------------------

        private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            _config = ReadUIToConfig();
            if (DuctStandardsConfigService.Save(_config))
                MessageBox.Show("Configuration saved.", "Duct Standards Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show("Failed to save configuration.", "Duct Standards Manager", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void BtnLoadConfig_Click(object sender, RoutedEventArgs e)
        {
            LoadConfigAndPopulateUI();
        }

        private void BtnImportJson_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                Title = "Import Duct Standards Config"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    _config = DuctStandardsConfigService.ImportFromFile(dlg.FileName);
                    PopulateUI();
                    MessageBox.Show("Config imported successfully.", "Duct Standards Manager", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Import failed: " + ex.Message, "Duct Standards Manager", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnExportJson_Click(object sender, RoutedEventArgs e)
        {
            _config = ReadUIToConfig();
            var dlg = new SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                FileName = "duct_standards_config.json"
            };

            if (dlg.ShowDialog() == true)
            {
                if (DuctStandardsConfigService.ExportToFile(_config, dlg.FileName))
                    MessageBox.Show("Config exported.", "Duct Standards Manager", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show("Export failed.", "Duct Standards Manager", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnResetDefault_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Reset all settings to default values?",
                "Duct Standards Manager",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _config = DuctStandardsConfigService.CreateDefault();
                PopulateUI();
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            string folder = Path.GetDirectoryName(DuctStandardsConfigService.GetConfigFilePath());
            if (Directory.Exists(folder))
            {
                try
                {
                    Process.Start(folder);
                }
                catch (Exception) { }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private static string GetComboText(System.Windows.Controls.ComboBox combo)
        {
            if (combo == null || combo.SelectedItem == null)
                return null;

            var item = combo.SelectedItem as ComboBoxItem;
            if (item != null)
                return item.Content?.ToString();

            return combo.SelectedItem.ToString();
        }

        private static void SetComboText(System.Windows.Controls.ComboBox combo, string value)
        {
            if (combo == null || value == null) return;

            for (int i = 0; i < combo.Items.Count; i++)
            {
                var item = combo.Items[i] as ComboBoxItem;
                if (item != null && string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
                if (combo.Items[i] is string s && string.Equals(s, value, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }

            // Fallback: add and select
            combo.Items.Add(value);
            combo.SelectedIndex = combo.Items.Count - 1;
        }

        private static double ParseDouble(string text, double defaultValue)
        {
            double val;
            return double.TryParse(text, out val) ? val : defaultValue;
        }

        private sealed class DuctParamSpec
        {
            public DuctParamSpec(string name, AjSpec parameterType, AjGroup parameterGroup)
            {
                Name = name;
                ParameterType = parameterType;
                ParameterGroup = parameterGroup;
            }

            public string Name { get; }
            public AjSpec ParameterType { get; }
            public AjGroup ParameterGroup { get; }
        }
    }
}
