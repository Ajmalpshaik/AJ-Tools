using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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

        // Why the saved settings file could not be read (null = it was read fine). While set, the
        // settings on screen are the built-in defaults, not rules the user chose.
        private string _configLoadError;

        // The user has already agreed, in this window, to write values calculated from those defaults.
        private bool _defaultsRunConfirmed;

        public DuctStandardsManagerWindow(UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc = uidoc.Document;
            InitializeComponent();

            // Shared AJ Tools window entrance (fade + short rise). Cosmetic only.
            WindowMotionHelper.AttachStandardEntrance(this);

            // Shared AJ Tools window exit (fade + short sink). The window's result,
            // validation and close behaviour are unchanged - see WindowMotionHelper's header.
            WindowMotionHelper.AttachStandardExit(this);

            // Shared tab-change transition (fade + short rise on the panel). Cosmetic only.
            TabMotionHelper.AttachTabTransitions(this);
            LoadConfigAndPopulateUI();

            // An unreadable settings file is reported once the window is on screen, so the warning
            // sits over the window it is about instead of appearing before it.
            ContentRendered += (s, e) => ShowConfigLoadWarning();
        }

        // -------------------------------------------------------------------
        // Config <-> UI
        // -------------------------------------------------------------------

        private void LoadConfigAndPopulateUI()
        {
            string loadError;
            _config = DuctStandardsConfigService.Load(out loadError);
            _configLoadError = loadError;
            _defaultsRunConfirmed = false;
            PopulateUI();
        }

        /// <summary>
        /// Important-warning popup (allowed): the saved settings could not be read, so what is on screen
        /// - and what Run would calculate with - is the built-in defaults, not the user's rules.
        /// </summary>
        private void ShowConfigLoadWarning()
        {
            if (string.IsNullOrEmpty(_configLoadError))
                return;

            MessageBox.Show(
                "Your saved Duct Standards settings could not be read, so the built-in default rules are shown instead.\n\n" +
                "File: " + DuctStandardsConfigService.GetConfigFilePath() + "\n" +
                "Reason: " + _configLoadError + "\n\n" +
                "The file has not been changed. Check the rules before running, or use Save Config to replace the file with the settings shown here.",
                DialogTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        /// <summary>The settings now on screen were chosen by the user (saved, imported, or reset on purpose).</summary>
        private void ClearConfigLoadError()
        {
            _configLoadError = null;
            UpdateConfigPathText();
        }

        private void UpdateConfigPathText()
        {
            txtConfigPath.Text = "Config: " + DuctStandardsConfigService.GetConfigFilePath();
            if (!string.IsNullOrEmpty(_configLoadError))
                txtConfigPath.Text += "\nThis file could not be read (" + _configLoadError + ") - the built-in default rules are loaded. The file has not been changed.";
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
            if (_materials != null)
                _materials.CollectionChanged -= Materials_CollectionChanged;
            _materials = new ObservableCollection<MaterialInfo>(_config.Materials ?? new List<MaterialInfo>());
            _materials.CollectionChanged += Materials_CollectionChanged;
            dgMaterials.ItemsSource = _materials;

            // Default material / pressure combos
            FillDefaultMaterialItems(_config.DefaultMaterial);

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
            UpdateConfigPathText();
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
                        "All mapped parameters are available on Ducts.",
                        DialogTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        "These mapped parameters are not on Ducts:\n\n- " + string.Join("\n- ", missing) +
                        "\n\nUse Create/Rebind Parameters to add them to Ducts.",
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

                // Say exactly what will happen: brand-new parameters are created on Ducts; parameters that
                // already exist in the project only get Ducts ADDED to what they are bound to.
                BindingMap bindings = _doc.ParameterBindings;
                var toCreate = specs.Where(s => FindDefinition(bindings, s.Name) == null).ToList();
                var toExtend = specs.Where(s => FindDefinition(bindings, s.Name) != null).ToList();

                var prompt = new StringBuilder();
                if (toCreate.Count > 0)
                {
                    prompt.Append("These will be created as new Duct instance parameters (shared parameters, group \"" + SharedParamGroupName + "\"):\n\n");
                    prompt.Append(string.Join("\n", toCreate.Select(s => "- " + s.Name + " (" + SharedParamUtils.GetParameterTypeLabel(s.ParameterType) + ")")));
                    prompt.Append("\n\n");
                }

                if (toExtend.Count > 0)
                {
                    prompt.Append("These already exist in the project but are not on Ducts. Ducts will be added to them - the categories they already have, their values and their instance/type setting are kept:\n\n");
                    prompt.Append(string.Join("\n", toExtend.Select(s =>
                    {
                        ElementBinding existingBinding;
                        FindDefinition(bindings, s.Name, out existingBinding);
                        return "- " + s.Name + (existingBinding is TypeBinding
                            ? " (type parameter - Run writes per-duct values only into instance parameters)"
                            : string.Empty);
                    })));
                    prompt.Append("\n\n");
                }

                prompt.Append("Continue?");

                var confirmation = MessageBox.Show(
                    prompt.ToString(),
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
                    SetStatus("Parameters ready on Ducts (" + processed + " created or updated).");
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
            // Bound to Ducts specifically - a parameter bound only to other categories is not on any
            // duct, even though its name is in the project (it used to count as present).
            var boundToDucts = GetDuctBoundParamNames(_doc);
            var required = BuildMappedParameterSpecs(config);
            Element sampleDuct = GetAnyDuctElement();

            return required
                .Where(s =>
                {
                    if (boundToDucts.Contains(s.Name))
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

            // Only what is actually missing from Ducts. Parameters already on Ducts are left alone: re-binding
            // them replaced their binding with a Ducts-only one, dropping every other category they were
            // bound to (and the values there) and turning a type parameter into an instance one.
            var missing = new HashSet<string>(GetMissingMappedParameterNames(config), StringComparer.OrdinalIgnoreCase);

            return mapped
                .Where(s => missing.Contains(s.Name))
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
                Category ductCategory = GetDuctCategory();
                CategorySet categorySet = BuildDuctCategorySet(app, ductCategory);
                BindingMap map = _doc.ParameterBindings;

                int success = 0;
                foreach (var spec in specs)
                {
                    ElementBinding existingBinding;
                    Definition existing = FindDefinition(map, spec.Name, out existingBinding);

                    bool ok;
                    if (existing != null)
                    {
                        // Already in the project, just not on Ducts: ADD Ducts to what it is bound to. Never
                        // re-bind it to Ducts alone (drops its other categories), and never Remove + Insert
                        // as a fallback - removing a binding deletes every value the parameter holds.
                        ok = AddDuctsToBinding(app, map, existing, existingBinding, ductCategory);
                    }
                    else
                    {
                        Definition definition = GetOrCreateDefinition(file, group, spec.Name, spec.ParameterType);
                        if (definition == null)
                            continue;

                        Binding binding = app.Create.NewInstanceBinding(categorySet);
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

        private Category GetDuctCategory()
        {
            Category ductCategory = _doc.Settings.Categories.get_Item(BuiltInCategory.OST_DuctCurves);

            if (ductCategory == null || !ductCategory.AllowsBoundParameters)
                throw new InvalidOperationException("Ducts category is not available for parameter binding.");

            return ductCategory;
        }

        private static CategorySet BuildDuctCategorySet(Autodesk.Revit.ApplicationServices.Application app, Category ductCategory)
        {
            var set = app.Create.NewCategorySet();
            set.Insert(ductCategory);
            return set;
        }

        /// <summary>
        /// Adds Ducts to an existing parameter's binding: same categories plus Ducts, same instance/type
        /// kind, same parameter group. ReInsert with the merged set is the API's way to extend a binding.
        /// </summary>
        private static bool AddDuctsToBinding(Autodesk.Revit.ApplicationServices.Application app, BindingMap map,
            Definition definition, ElementBinding existingBinding, Category ductCategory)
        {
            if (existingBinding == null || existingBinding.Categories == null)
                return false;

            CategorySet merged = app.Create.NewCategorySet();
            foreach (Category category in existingBinding.Categories)
            {
                if (category != null)
                    merged.Insert(category);
            }
            merged.Insert(ductCategory);

            Binding binding = existingBinding is TypeBinding
                ? (Binding)app.Create.NewTypeBinding(merged)
                : app.Create.NewInstanceBinding(merged);

            return RevitCompat.ReInsertBinding(map, definition, binding, RevitCompat.GetGroup(definition));
        }

        /// <summary>Names of the project/shared parameters whose binding includes the Ducts category.</summary>
        private static HashSet<string> GetDuctBoundParamNames(Document doc)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var it = doc.ParameterBindings.ForwardIterator();
            while (it.MoveNext())
            {
                var def = it.Key;
                if (def != null && !string.IsNullOrWhiteSpace(def.Name) && BindingIncludesDucts(it.Current as ElementBinding))
                    names.Add(def.Name);
            }
            return names;
        }

        private static bool BindingIncludesDucts(ElementBinding binding)
        {
            if (binding == null || binding.Categories == null)
                return false;

            foreach (Category category in binding.Categories)
            {
                if (category != null && category.Id.IntValue() == (int)BuiltInCategory.OST_DuctCurves)
                    return true;
            }

            return false;
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
            ElementBinding binding;
            return FindDefinition(map, name, out binding);
        }

        private static Definition FindDefinition(BindingMap map, string name, out ElementBinding binding)
        {
            binding = null;
            var it = map.ForwardIterator();
            while (it.MoveNext())
            {
                var def = it.Key;
                if (def != null && string.Equals(def.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    binding = it.Current as ElementBinding;
                    return def;
                }
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
                // Allowance boxes only matter when allowances are included. A box that is not a number of
                // 0 or more is no longer swapped for a default behind the user's back.
                string allowanceError;
                if (chkIncludeAllowances.IsChecked == true && !ValidateAllowances(out allowanceError))
                {
                    txtResults.Text = "Not run - fix the allowance percentages first (Allowances tab):\n" + allowanceError;
                    return;
                }

                // Settings file unreadable: what would be written comes from rules the user never chose.
                // Important warning before continuing - asked once per window.
                if (writeToRevit && !string.IsNullOrEmpty(_configLoadError) && !_defaultsRunConfirmed)
                {
                    var answer = MessageBox.Show(
                        "Your saved settings file could not be read, so these values will be calculated from the built-in default rules, not the rules you saved.\n\n" +
                        "Write them to the ducts anyway?",
                        DialogTitle,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (answer != MessageBoxResult.Yes)
                    {
                        txtResults.Text = "Not run - your saved settings file could not be read. Check the rules, or use Save Config to keep the settings shown here.";
                        return;
                    }

                    _defaultsRunConfirmed = true;
                }

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
                if (!string.IsNullOrEmpty(_configLoadError))
                    txtResults.Text = "Note: your saved settings file could not be read - these results use the built-in default rules.\n\n" + txtResults.Text;

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

        // The Default Material list on the Process tab used to be filled only when settings loaded, so a
        // material added, removed or renamed here never reached it until the window was reopened.

        private void Materials_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            FillDefaultMaterialItems(GetComboText(cboDefaultMaterial));
        }

        private void DgMaterials_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit)
                return;

            var material = e.Row?.Item as MaterialInfo;
            if (material == null)
                return;

            string nameBefore = material.Name;

            // The typed value reaches the MaterialInfo only after this event returns - refresh once it has.
            // If the renamed row was the default material, the default follows it to the new name.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                string selected = GetComboText(cboDefaultMaterial);
                if (string.Equals(selected, nameBefore, StringComparison.OrdinalIgnoreCase))
                    selected = material.Name;
                FillDefaultMaterialItems(selected);
            }), DispatcherPriority.Background);
        }

        /// <summary>
        /// Lists the current material names and re-selects the given default. A default that is not in
        /// the list is kept and shown (SetComboText adds it), exactly as when the settings load.
        /// </summary>
        private void FillDefaultMaterialItems(string selectedName)
        {
            cboDefaultMaterial.Items.Clear();
            foreach (var m in _materials)
            {
                if (m != null && m.Name != null)
                    cboDefaultMaterial.Items.Add(m.Name);
            }
            SetComboText(cboDefaultMaterial, selectedName);
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
            string allowanceError;
            if (!ValidateAllowances(out allowanceError))
            {
                MessageBox.Show("Not saved - fix the allowance percentages first (Allowances tab):\n\n" + allowanceError,
                    DialogTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _config = ReadUIToConfig();
            if (DuctStandardsConfigService.Save(_config))
            {
                // The file now holds exactly what is on screen - no longer "defaults from an unreadable file".
                ClearConfigLoadError();
                SetStatus("Settings saved.");
            }
            else
                MessageBox.Show("Failed to save configuration.", "Duct Standards Manager", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// One line in the footer for a finished Save / Import / Export / Create-Rebind. These used to be
        /// "done" popups; the house rule is that a finished action does not stop the user with a dialog.
        /// </summary>
        private void SetStatus(string text)
        {
            if (txtStatus != null)
                txtStatus.Text = text ?? string.Empty;
        }

        private void BtnLoadConfig_Click(object sender, RoutedEventArgs e)
        {
            LoadConfigAndPopulateUI();
            ShowConfigLoadWarning();
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
                    _configLoadError = null; // settings the user chose on purpose
                    PopulateUI();
                    SetStatus("Settings imported from " + Path.GetFileName(dlg.FileName) + ".");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Import failed: " + ex.Message, "Duct Standards Manager", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnExportJson_Click(object sender, RoutedEventArgs e)
        {
            string allowanceError;
            if (!ValidateAllowances(out allowanceError))
            {
                MessageBox.Show("Not exported - fix the allowance percentages first (Allowances tab):\n\n" + allowanceError,
                    DialogTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _config = ReadUIToConfig();
            var dlg = new SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                FileName = "duct_standards_config.json"
            };

            if (dlg.ShowDialog() == true)
            {
                if (DuctStandardsConfigService.ExportToFile(_config, dlg.FileName))
                    SetStatus("Settings exported to " + Path.GetFileName(dlg.FileName) + ".");
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
                _configLoadError = null; // the defaults are now the user's own choice
                PopulateUI();
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            string folder = Path.GetDirectoryName(DuctStandardsConfigService.GetConfigFilePath());
            if (!Directory.Exists(folder))
            {
                MessageBox.Show("The settings folder does not exist yet:\n\n" + folder,
                    DialogTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // UseShellExecute must be set: it defaults to false on .NET 8/10 (Revit 2025-2027), where
                // starting a folder path without the shell throws instead of opening Explorer.
                var startInfo = new ProcessStartInfo(folder)
                {
                    UseShellExecute = true
                };
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open the settings folder:\n\n" + folder + "\n\n" + ex.Message,
                    DialogTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
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

        // -------------------------------------------------------------------
        // Allowance validation
        // -------------------------------------------------------------------

        private void Allowance_TextChanged(object sender, TextChangedEventArgs e)
        {
            string error;
            ValidateAllowances(out error);
        }

        /// <summary>
        /// Every allowance must be a number of 0 or more. ReadUIToConfig used to replace anything else with
        /// the default without a word, and took negatives as they were. Shows the first problem inline on
        /// the Allowances tab (no popup while typing) and returns false while there is one; Run, Save and
        /// Export check it before they use the values.
        /// </summary>
        private bool ValidateAllowances(out string error)
        {
            error = CheckAllowance(txtSeam, "Seam Allowance")
                 ?? CheckAllowance(txtJoint, "Joint Allowance")
                 ?? CheckAllowance(txtFlange, "Flange Allowance")
                 ?? CheckAllowance(txtReinforcement, "Reinforcement")
                 ?? CheckAllowance(txtFittings, "Fittings Allowance")
                 ?? CheckAllowance(txtWastage, "Wastage Allowance");

            if (txtAllowanceError != null)
            {
                txtAllowanceError.Text = error ?? string.Empty;
                txtAllowanceError.Visibility = error == null
                    ? System.Windows.Visibility.Collapsed
                    : System.Windows.Visibility.Visible;
            }

            return error == null;
        }

        private static string CheckAllowance(System.Windows.Controls.TextBox box, string label)
        {
            if (box == null)
                return null;

            double value;
            if (!double.TryParse(box.Text, out value) || double.IsNaN(value) || double.IsInfinity(value))
                return label + " (%) must be a number.";
            if (value < 0)
                return label + " (%) cannot be negative.";
            return null;
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
