#region Metadata
/*
 * Tool Name     : Connect MEP Elements (Smart Connect)
 * File Name     : SmartConnectCommand.cs
 * Purpose       : Connects two same-category MEP elements (Pipe, Duct, or Cable Tray) with a routed run
 *                 using the routing mode and bend angle chosen in the Smart Connect window.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.1.0
 *
 * Created Date  : 2026-03-25
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Services.SmartConnect, AJTools.UI, AJTools.Utils
 *
 * Input         : Active project - routing settings from the window, then two same-category MEP elements
 *                 picked per connection (Esc to finish).
 * Output        : A connecting run built between each pair; each connection commits on its own.
 *
 * Notes         :
 * - Targets Revit 2020 through latest.
 * - Project-only tool; validates an editable, non-family document before picking.
 * - Esc during a pick ends the session silently; an invalid pair shows the reason and lets the user retry.
 * - Production-ready implementation.
 *
 * Changelog     :
 * v1.0.0 (2026-03-25) - Initial release.
 * v1.1.0 (2026-07-01) - Refactor/audit: added full metadata block. Connect behaviour unchanged.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

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

                    using (Transaction transaction = new Transaction(document, "AJ Tools - Smart Connect"))
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
