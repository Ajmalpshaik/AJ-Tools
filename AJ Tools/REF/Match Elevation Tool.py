# -*- coding: utf-8 -*-
"""
Match Elevation Tool
Matches the middle elevation from one MEP element to others
Similar to Match Properties but for elevation only
Compatible with Revit 2020
"""

__title__ = "Match\nElevation"
__author__ = "Your Name"
__doc__ = """Select a source element, then click target elements to match elevation.
Each click immediately changes the elevation. Press ESC to finish."""

# Import Required Libraries
from Autodesk.Revit.DB import *
from Autodesk.Revit.DB.Plumbing import *
from Autodesk.Revit.DB.Mechanical import *
from Autodesk.Revit.DB.Electrical import *
from Autodesk.Revit.UI.Selection import *
from pyrevit import revit, DB, UI
from pyrevit import forms

# Get current document and UI application
doc = revit.doc
uidoc = revit.uidoc

# Custom filter for MEP elements
class MEPElementFilter(ISelectionFilter):
    def AllowElement(self, element):
        # Check if element is a pipe, duct, cable tray, or conduit
        categories = [
            BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_DuctCurves,
            BuiltInCategory.OST_CableTray,
            BuiltInCategory.OST_Conduit,
            BuiltInCategory.OST_FlexDuctCurves,
            BuiltInCategory.OST_FlexPipeCurves
        ]
        
        for cat in categories:
            if element.Category.Id.IntegerValue == int(cat):
                return True
        return False
    
    def AllowReference(self, reference, position):
        return False

def get_element_middle_elevation(element):
    """Get the middle elevation of an MEP element"""
    try:
        # Get location curve
        location = element.Location
        if not isinstance(location, LocationCurve):
            return None
        
        curve = location.Curve
        
        # Get start and end points
        start_point = curve.GetEndPoint(0)
        end_point = curve.GetEndPoint(1)
        
        # Calculate middle elevation (average of start and end Z coordinates)
        middle_elevation = (start_point.Z + end_point.Z) / 2.0
        
        return middle_elevation
    except:
        return None

def set_element_elevation(element, target_elevation):
    """Set the elevation of an MEP element while maintaining its slope"""
    try:
        # Get location curve
        location = element.Location
        if not isinstance(location, LocationCurve):
            return False
        
        curve = location.Curve
        
        # Get current start and end points
        start_point = curve.GetEndPoint(0)
        end_point = curve.GetEndPoint(1)
        
        # Calculate current middle elevation
        current_middle = (start_point.Z + end_point.Z) / 2.0
        
        # Calculate elevation difference
        elevation_diff = target_elevation - current_middle
        
        # Create new points with adjusted elevation
        new_start = XYZ(start_point.X, start_point.Y, start_point.Z + elevation_diff)
        new_end = XYZ(end_point.X, end_point.Y, end_point.Z + elevation_diff)
        
        # Create new line
        new_line = Line.CreateBound(new_start, new_end)
        
        # Set the new curve
        location.Curve = new_line
        
        return True
    except:
        return False

def main():
    """Main function to run the match elevation tool"""
    
    # Create selection filter
    mep_filter = MEPElementFilter()
    
    try:
        # Select source element
        source_ref = uidoc.Selection.PickObject(
            ObjectType.Element,
            mep_filter,
            "Select SOURCE element to copy elevation from"
        )
        
        source_element = doc.GetElement(source_ref)
        
        # Get source elevation
        source_elevation = get_element_middle_elevation(source_element)
        
        if source_elevation is None:
            forms.alert("Could not get elevation from source element", exitscript=True)
            return
        
        # Convert to project units for display (mm for metric)
        source_elev_display = source_elevation * 304.8
        # For imperial projects, comment line above and uncomment below:
        # source_elev_display = source_elevation
        
        # Continuously select and change target elements
        counter = 0
        
        while True:
            try:
                # Select target element
                target_ref = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    mep_filter,
                    "Click elements to match elevation (ESC to finish) - Elevation: {:.0f}mm".format(source_elev_display)
                )
                
                target_element = doc.GetElement(target_ref)
                
                # Apply elevation change in a transaction
                with revit.Transaction("Match Elevation"):
                    set_element_elevation(target_element, source_elevation)
                
                counter += 1
                
            except:
                # User pressed ESC or cancelled
                break
    
    except:
        # User cancelled source selection
        pass

# Run the tool
if __name__ == "__main__":
    main()