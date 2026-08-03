---
name: auto_split_ducts
description: Automatically splits main ducts 500mm downstream of air terminal connections and inserts transition union fittings.
---

# Auto Split Ducts

This skill automatically splits horizontal main ducts downstream of air terminal branch connections and inserts `M_Rectangular Union` fittings to prepare the duct network for sizing transitions.

## How it works
1. **Flow Direction Analysis**: The script identifies the true upstream and downstream directions of every duct by inspecting the `FlowDirectionType.In` connector.
2. **Takeoff Grouping**: It identifies all takeoffs (branches) along the duct and groups any that are within 2.0 feet of each other (e.g., opposite-side takeoffs) into a single logical connection point.
3. **Geometric Offset**: It measures exactly 500 mm downstream of the connection point along the exact `LocationCurve` of the duct.
4. **Skipping the End**: It skips the very last connection point on the duct, as the duct typically terminates shortly after and requires no further transition.
5. **Break and Union**: It slices the duct using `MechanicalUtils.BreakCurve` and inserts the Union fitting.

## Execution
Run the provided script using the AJ AI Bridge:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .claude\tools\invoke-aj-ai-bridge.ps1 -CodeFile .agents\skills\auto_split_ducts\scripts\split-downstream.cs -AllowDestructive
```
