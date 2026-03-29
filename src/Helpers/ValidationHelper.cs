// Tool Name: Validation Helper
// Description: Centralized validation utilities for common Revit document/view checks.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-11
// Revit Version: 2020
// Dependencies: Autodesk.Revit.DB, Autodesk.Revit.UI

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AJTools.Utils
{
    /// <summary>
    /// Provides common validation checks for Revit documents and views.
    /// </summary>
    internal static class ValidationHelper
    {
        /// <summary>
        /// Validates that an active UIDocument exists.
        /// </summary>
        /// <param name="uidoc">The UIDocument to validate.</param>
        /// <param name="message">Error message if validation fails.</param>
        /// <returns>True if valid, false otherwise.</returns>
        public static bool ValidateUIDocument(UIDocument uidoc, out string message)
        {
            if (uidoc == null)
            {
                message = "No active document.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        /// <summary>
        /// Validates that an active, non-template view exists.
        /// </summary>
        /// <param name="view">The view to validate.</param>
        /// <param name="message">Error message if validation fails.</param>
        /// <returns>True if valid, false otherwise.</returns>
        public static bool ValidateActiveView(View view, out string message)
        {
            if (view == null)
            {
                message = "No active view.";
                return false;
            }

            if (view.IsTemplate)
            {
                message = "Please run this tool in a normal project view.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        /// <summary>
        /// Validates both UIDocument and its active view.
        /// </summary>
        /// <param name="uidoc">The UIDocument to validate.</param>
        /// <param name="message">Error message if validation fails.</param>
        /// <returns>True if both are valid, false otherwise.</returns>
        public static bool ValidateUIDocumentAndView(UIDocument uidoc, out string message)
        {
            if (!ValidateUIDocument(uidoc, out message))
                return false;

            return ValidateActiveView(uidoc.Document?.ActiveView, out message);
        }

        /// <summary>
        /// Validates that the document is not read-only.
        /// </summary>
        /// <param name="doc">The document to validate.</param>
        /// <param name="message">Error message if validation fails.</param>
        /// <returns>True if document is editable, false otherwise.</returns>
        public static bool ValidateEditableDocument(Document doc, out string message)
        {
            if (doc == null)
            {
                message = "No document available.";
                return false;
            }

            if (doc.IsReadOnly)
            {
                message = "The document is read-only. Please open an editable document.";
                return false;
            }

            if (doc.IsFamilyDocument)
            {
                message = "This tool cannot be used in family documents.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        /// <summary>
        /// Validates that a view supports a specific view type.
        /// </summary>
        /// <param name="view">The view to validate.</param>
        /// <param name="allowedTypes">Array of allowed view types.</param>
        /// <param name="message">Error message if validation fails.</param>
        /// <returns>True if view type is supported, false otherwise.</returns>
        public static bool ValidateViewType(View view, ViewType[] allowedTypes, out string message)
        {
            if (view == null)
            {
                message = "No active view.";
                return false;
            }

            foreach (var allowedType in allowedTypes)
            {
                if (view.ViewType == allowedType)
                {
                    message = string.Empty;
                    return true;
                }
            }

            message = $"This tool only works in {string.Join(", ", allowedTypes)} views.";
            return false;
        }

        /// <summary>
        /// Validates that a view has crop enabled.
        /// </summary>
        /// <param name="view">The view to validate.</param>
        /// <param name="message">Error message if validation fails.</param>
        /// <returns>True if crop is active, false otherwise.</returns>
        public static bool ValidateCropBoxActive(View view, out string message)
        {
            if (view == null)
            {
                message = "No active view.";
                return false;
            }

            if (!view.CropBoxActive)
            {
                message = "Enable 'Crop View' for the active view and try again.";
                return false;
            }

            message = string.Empty;
            return true;
        }
    }
}
