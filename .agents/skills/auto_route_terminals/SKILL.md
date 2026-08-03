---
name: Auto Route Air Terminals
description: Automatically routes unconnected air terminals to the nearest horizontal main duct using a proper architectural L-shape layout (horizontal branch, elbow, and vertical drop).
---

# Auto Route Air Terminals

This skill is used whenever the user asks to connect air terminals, route ducts to diffusers, or connect diffusers to the main duct network.

## Context
When routing air terminals (diffusers) to a main duct, a direct connection often results in an invalid diagonal duct. The proper way to route them is to create an L-shaped branch:
1. A horizontal branch duct originating from the main duct.
2. A vertical drop duct connecting the branch to the terminal.
3. An elbow fitting connecting the branch to the drop, and a takeoff fitting connecting the branch to the main duct.

## Instructions
1. We have a pre-written C# script that implements this algorithm perfectly.
2. The script is located at `.agents\skills\auto_route_terminals\scripts\route-L-shape.cs` relative to the workspace root.
3. To trigger the skill, simply run the script using the AJ AI Bridge tool:
```bash
powershell -NoProfile -ExecutionPolicy Bypass -File .claude\tools\invoke-aj-ai-bridge.ps1 -CodeFile .agents\skills\auto_route_terminals\scripts\route-L-shape.cs -AllowDestructive
```
4. The script will automatically skip any terminals that are already connected, so it is safe to run multiple times.
