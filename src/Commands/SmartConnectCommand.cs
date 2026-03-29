// Tool Name: Smart Connect Command
// Description: Connects two same-category MEP elements using selected routing and angle settings.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-03-25
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI, AJTools.Services.SmartConnect

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Linq;
using System.Windows.Interop;
using AJTools.Models;
using AJTools.Services.SmartConnect;
using AJTools.UI;
using AJTools.Utils;

namespace AJTools.Commands
{
    /// <summary>
    /// Entry command for Smart Connect workflow.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SmartConnectCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDocument = commandData.Application?.ActiveUIDocument;
            if (!ValidationHelper.ValidateUIDocument(uiDocument, out message))
            {
                DialogHelper.ShowError("Smart Connect", message);
                return Result.Cancelled;
            }

            Document document = uiDocument.Document;
            if (!ValidationHelper.ValidateEditableDocument(document, out message))
            {
                DialogHelper.ShowError("Smart Connect", message);
                return Result.Cancelled;
            }

            try
            {
                if (!TryResolveSettings(commandData, out SmartConnectSettings settings))
                {
                    return Result.Cancelled;
                }

                var routeBuilder = new SmartConnectRouteBuilder(document);
                bool anyConnectionCreated = false;

                while (true)
                {
                    Element firstElement;
                    Element secondElement;
                    string pickError;

                    try
                    {
                        if (!TryPickElements(uiDocument, out firstElement, out secondElement, out pickError))
                        {
                            if (!string.IsNullOrWhiteSpace(pickError))
                            {
                                DialogHelper.ShowError("Smart Connect", pickError);
                            }

                            continue;
                        }
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }

                    using (Transaction transaction = new Transaction(document, "Smart Connect"))
                    {
                        transaction.Start();

                        if (!routeBuilder.TryBuildRoute(
                            firstElement,
                            secondElement,
                            settings.RoutingMode,
                            settings.SelectedAngleDegrees,
                            out string routeError))
                        {
                            transaction.RollBack();
                            DialogHelper.ShowError("Smart Connect", routeError);
                            continue;
                        }

                        transaction.Commit();
                        anyConnectionCreated = true;
                    }
                }

                return anyConnectionCreated ? Result.Succeeded : Result.Cancelled;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                DialogHelper.ShowError("Smart Connect", "An unexpected error occurred:\n" + ex.Message);
                return Result.Failed;
            }
        }

        private static bool TryResolveSettings(ExternalCommandData commandData, out SmartConnectSettings settings)
        {
            settings = null;

            var settingsService = new SmartConnectSettingsService();
            SmartConnectSettings loadedSettings = settingsService.Load();

            var window = new SmartConnectWindow(loadedSettings);
            new WindowInteropHelper(window)
            {
                Owner = commandData.Application.MainWindowHandle
            };

            bool? dialogResult = window.ShowDialog();
            if (dialogResult != true)
            {
                return false;
            }

            settings = new SmartConnectSettings
            {
                RoutingMode = window.SelectedRoutingMode,
                SelectedAngleDegrees = window.SelectedAngleDegrees,
                CustomAngles = window.CustomAngles?.ToList() ?? loadedSettings.CustomAngles
            };

            settingsService.Save(settings);
            return true;
        }

        private static bool TryPickElements(
            UIDocument uiDocument,
            out Element firstElement,
            out Element secondElement,
            out string errorMessage)
        {
            firstElement = null;
            secondElement = null;
            errorMessage = string.Empty;

            Reference firstReference = uiDocument.Selection.PickObject(
                ObjectType.Element,
                new SmartConnectSelectionFilter(),
                "Smart Connect: Select first element (Pipe, Duct, or Cable Tray). Press Esc to finish.");

            firstElement = uiDocument.Document.GetElement(firstReference);
            if (!SmartConnectSelectionFilter.TryGetSupportedCategory(firstElement, out BuiltInCategory firstCategory))
            {
                errorMessage = "First selection must be Pipe, Duct, or Cable Tray.";
                return false;
            }

            string categoryName = SmartConnectSelectionFilter.GetCategoryDisplayName(firstCategory);
            Reference secondReference = uiDocument.Selection.PickObject(
                ObjectType.Element,
                new SmartConnectSelectionFilter(firstCategory),
                $"Smart Connect: Select second {categoryName} element.");

            secondElement = uiDocument.Document.GetElement(secondReference);
            if (secondElement == null)
            {
                errorMessage = "Second selection is invalid.";
                return false;
            }

            if (firstElement.Id == secondElement.Id)
            {
                errorMessage = "Please select two different elements.";
                return false;
            }

            if (!SmartConnectSelectionFilter.TryGetSupportedCategory(secondElement, out BuiltInCategory secondCategory))
            {
                errorMessage = "Second selection must be a supported MEP element.";
                return false;
            }

            if (firstCategory != secondCategory)
            {
                errorMessage = "Both selections must be from the same category.";
                return false;
            }

            return true;
        }
    }
}
