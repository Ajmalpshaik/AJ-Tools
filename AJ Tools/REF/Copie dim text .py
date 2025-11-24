# -*- coding: utf-8 -*-
"""
Copies the 'Text Above', 'Text Below', 'Prefix', and 'Suffix' values from a
source dimension and pastes them to subsequently selected dimensions in a
continuous loop. This script is compatible with Revit 2020.

This tool streamlines the process of duplicating dimension text. It copies
the text from the four main text fields of a selected source dimension
and then enters a loop. In the loop, you can click on any number of
dimensions one by one, and the text will be pasted instantly. Press the
'Escape' key to end the command.

How to use:
1. Click the pushbutton in the PyRevit toolbar.
2. Follow the prompt to select the 'source' dimension with the text to copy.
3. The script will now wait for you to select a target dimension.
4. Click on a target dimension, and all four text fields will be pasted.
5. Continue clicking on other dimensions to paste to them as well.
6. Press the 'Esc' key on your keyboard to finish the command.
"""

# Import necessary libraries from the Revit API and PyRevit
import Autodesk.Revit.DB as DB
from Autodesk.Revit.UI.Selection import ObjectType, ISelectionFilter
from Autodesk.Revit.Exceptions import OperationCanceledException

from pyrevit import revit, forms

# Get the current document and UI document
doc = revit.doc
uidoc = revit.uidoc

class DimensionSelectionFilter(ISelectionFilter):
    """
    Custom selection filter to allow the user to select only Dimension elements.
    """
    def AllowElement(self, elem):
        return isinstance(elem, DB.Dimension)

    def AllowReference(self, reference, position):
        return False

def main():
    """
    Main function to execute the dimension text copy/paste logic.
    """
    try:
        # --- 1. Prompt User to Select Source Dimension ---
        source_ref = uidoc.Selection.PickObject(ObjectType.Element, DimensionSelectionFilter(), "Select SOURCE dimension to copy text from")
        source_dim = doc.GetElement(source_ref.ElementId)

        if not source_dim:
            return

        # --- 2. Extract Text from Source Dimension ---
        # Use the direct API properties for all four text fields
        text_above = source_dim.Above
        text_below = source_dim.Below
        text_prefix = source_dim.Prefix
        text_suffix = source_dim.Suffix

        # Check if there is any text to copy. If not, alert the user and exit.
        if not text_above and not text_below and not text_prefix and not text_suffix:
            forms.alert("The selected source dimension has no text in the 'Above', 'Below', 'Prefix', or 'Suffix' fields to copy.", title="No Text Found")
            return
        
        # --- 3. Start Continuous Loop for Pasting ---
        updated_count = 0
        while True:
            try:
                # Prompt user to select a single target dimension in each loop iteration
                target_ref = uidoc.Selection.PickObject(ObjectType.Element, DimensionSelectionFilter(), "Select a TARGET dimension to paste to (Press ESC to finish)")
                
                # Apply Changes in a mini-transaction for instant update
                with DB.Transaction(doc, 'Paste Dimension Text') as t:
                    t.Start()
                    
                    target_dim = doc.GetElement(target_ref.ElementId)
                    
                    # Apply all four copied text values to the target dimension
                    target_dim.Above = text_above
                    target_dim.Below = text_below
                    target_dim.Prefix = text_prefix
                    target_dim.Suffix = text_suffix
                    
                    t.Commit()
                
                # Refresh the view to ensure the change is visible immediately
                uidoc.RefreshActiveView()
                updated_count += 1

            except OperationCanceledException:
                # User pressed ESC, which is the intended way to exit the loop
                break # Exit the while loop
            except Exception as e:
                # Handle other potential errors during the loop
                forms.alert("An error occurred during selection: {}. Exiting loop.".format(e), title="Error")
                break

        # --- 4. Provide Final Feedback ---
        if updated_count > 0:
            success_message = "Finished. Successfully copied text to {} dimension(s).".format(updated_count)
            forms.alert(success_message, title="Success")
        else:
            forms.alert("Operation finished. No dimensions were updated.", title="Finished")

    except OperationCanceledException:
        # This catches cancellation during the initial SOURCE selection
        forms.alert("Operation cancelled by user.", title="Cancelled")
    except Exception as e:
        forms.alert(
            "An unexpected error occurred: {}".format(str(e)),
            title="Error",
            warn_icon=True
        )

if __name__ == "__main__":
    main()
