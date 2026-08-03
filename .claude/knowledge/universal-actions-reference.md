# Universal Revit Actions — reference list (v3 — full Revisions lifecycle added)

Plain-language index of generic, category-agnostic Revit actions available (or genuinely buildable)
through the bridge. Every action works on **any category/element** — variables in `[brackets]` are
always supplied per request, never hardcoded. See
[`../scripts/README.md`](../scripts/README.md) for the ones already built as fragments. **NEEDS_REVIEW**
= real question mark on how cleanly the standard Revit API supports it, or genuinely risky/complex — not
something to build without checking further first.

**14 of these (2026-07-22) are also available as real, individually schema-validated MCP tools** —
`list_elements`, `count_elements`, `hide_elements`, `unhide_elements`, `isolate_elements`,
`reset_isolation`, `set_color`, `reset_graphic_overrides`, `set_transparency`, `select_elements`,
`set_parameter_value`, `report_parameters`, `move_elements`, `delete_elements` — added directly to this
project's `mcp-server/index.js`, so they're already live on this project's own bridge connection too, no
extra setup needed. Prefer these over composing the matching fragment when one exists — faster,
protocol-validated, no code generation needed.

**Note on the brief's own numbers** (from the request that produced v2): "minimum 100" in one place,
"minimum 200" in the output format. This list totals **182** real, distinct, non-duplicate actions —
clears the 100 minimum, short of 200. Padding to 200 would mean inventing filler, which the same brief
ruled out. Honest stopping point over a round number.

**What changed in v3**: pulled Revisions out of the old combined "Revisions & Phases" group into its own
full lifecycle — create, edit, delete, add/remove from a sheet, revision schedules, the works — per a
direct follow-up request. Phases stays separate. **Read
[`live-model/revisions.md`](live-model/revisions.md) before building Create/Delete Revision** — there's a
real, confirmed gotcha there: an unattached revision can silently vanish (auto-purged) the next time
something touches sheet-revision association, and its Sequence Number isn't stable to rely on either.

---

## Visibility & Graphics
1. Hide Elements – [element/elements], [view], temp/permanent
2. Unhide Elements – reverse permanent hide
3. Reset Temporary Hide/Isolate – [view]
4. Isolate Elements – [element/elements] in [view]
5. Show/Zoom to Elements
6. Set Color – [color] on [element/elements], optional [view] (defaults to active — targets any view directly, not just what's on screen)
7. Color by Group – by [parameter], optional [view]
8. Highlight vs Rest – optional [view]
9. Reset Graphic Overrides – optional [view]
10. Set Transparency – [value]%, optional [view]
11. Report Graphic Overrides – optional [view]
12. Section Box & Zoom – optional [target 3D view]
13. Set View Crop – optional [view]
14. Toggle Category Visibility – [category], [view]
15. Set Visibility/Graphics Override by Category
16. Set Halftone/Transparency by Category

## Parameters & Data
17. Set Parameter Value
18. Copy Parameter Value
19. Report Parameters
20. Rename Element
21. Renumber Sequentially
22. Change Type
23. Pin/Unpin
24. Count & Report
25. Report Location
26. Report Bounding Box
27. Length by Size
28. Material Takeoff
29. Find Duplicates
30. Set Type Parameter
31. Report Family/Type Usage Count
32. Set Room-Bounding Flag
33. Set Workset of Element
34. Report Element Owner (worksharing)

## Selection & Filtering
35. Filter by Category
36. Filter by Category + Family
37. Filter by Category + Numeric Parameter
38. Filter by Category Name
39. Filter by Room
40. Filter by System Type
41. Filter by Current Selection
42. Filter by Region
43. Filter by Multiple Categories
44. Filter by Parameter Text
45. Filter by Workset
46. Filter by Sheets
47. Filter by Phase
48. Filter by Element ID List
49. Select Elements
50. Save Selection Set
51. Load Selection Set
52. Update Selection Set

## Geometry / Edit
53. Move Elements
54. Copy Elements
55. Rotate Elements
56. Delete Elements – confirm count first, needs destructive access allowed
57. Mirror Elements
58. Create Group from Elements
59. Ungroup
60. Place Group Instance
61. Report Group Members
62. Join Geometry
63. Unjoin Geometry
64. Array – NEEDS_REVIEW
65. Align Elements – NEEDS_REVIEW
66. Edit Group Contents – NEEDS_REVIEW

## Creation
67. Create Levels
68. Create Room
69. Create Point-Based Element
70. Create Material
71. Create Grid
72. Create Grid System
73. Create Area Plan
74. Create Area Boundary Lines
75. Load Family

## Annotation
76. Tag Elements in View
77. Create Text Note
78. Create Dimension
79. Create Detail Line
80. Create Spot Elevation
81. Create Spot Coordinate
82. Create Keynote
83. Create Revision Cloud – [region/points], [revision], [view] (the cloud itself; see Revisions group for the revision record it references)
84. Add/Remove Leader on Tag
85. Set Annotation Type
86. Report Annotations in View

## Dimensions & Constraints
87. Lock/Unlock Dimension
88. Create EQ Constraint
89. Report Dimension Value
90. Override Dimension Text
91. Create Alignment (Reference Lock)
92. Delete Constraint

## Levels & Grids
93. Set Level Elevation
94. Set Grid Extents
95. Toggle Grid Bubble
96. Report Levels List
97. Report Grids List

## Views & View Templates
98. Create View (Plan/Section/3D/Elevation/Ceiling)
99. Duplicate View
100. Apply View Template
101. Create View Template from View
102. Set View Template Parameter Include/Exclude
103. Set View Scale
104. Set View Detail Level
105. Set View Discipline
106. Set View Phase
107. Set View Phase Filter
108. Set View Range
109. Set Underlay
110. Report View List

## View Filters (rule-based)
111. Create View Filter
112. Edit View Filter Rule
113. Delete View Filter
114. Duplicate View Filter
115. Apply Filter to View with Override
116. Report View Filters in Project

## Sheets & Titleblocks
117. Create Sheet – [number], [name], [title block]
118. Set Sheet Number – [sheet], [new number]
119. Set Sheet Parameter – [sheet], [parameter], [value]
120. Place Viewport on Sheet – [view] → [sheet]
121. Place Schedule on Sheet – [schedule] → [sheet]
122. Report Sheet List – [number]/[name]/[revision] per sheet
123. Report Sheets with No Placed Views

## Schedules
124. Create Schedule – [category], [field list]
125. Add/Remove Schedule Field
126. Set Schedule Sort/Group Field
127. Set Schedule Filter
128. Export Schedule to Text/CSV
129. Report Schedule Row Count

## Revisions (full lifecycle)
130. Create Revision – [description], [date] (defaults to today if not given), [issued to], [issued by] — read `live-model/revisions.md` first (auto-purge gotcha if not attached to a sheet)
131. Edit Revision – [revision], [field: description/date/issued to/issued by], [new value]
132. Delete Revision – [revision] — confirm first; if it was never attached to any sheet, deleting it (or even just touching another sheet's revisions afterward) can already be moot — check `live-model/revisions.md`
133. Reorder Revision Sequence – NEEDS_REVIEW (no confirmed simple "set order" API distinct from date/numbering)
134. Set Revision Numbering/Sequence Type – NEEDS_REVIEW (project-wide setting, exact API surface varies by Revit version)
135. Report Revisions List – every project revision with its properties
136. Add Revision to Sheet – [revision], [sheet/sheets]
137. Remove Revision from Sheet – [revision], [sheet/sheets]
138. Assign Revisions by Sheet Date – scan [sheets] TextNotes for dates, auto-attach the matching [revision]
139. Report Revisions on Sheet – [sheet]
140. Show/Hide Revision Cloud on Sheet – [sheet/view], [revision], [visible/hidden]
141. Create Revision Schedule – the titleblock-style schedule listing every revision, [placement]

## Phases
142. Create Phase – [phase name], [insert position]
143. Set Element Phase Created/Demolished – [element/elements], [phase]
144. Reorder Phases – [phase], [new position]
145. Report Elements by Phase – [phase], [category]

## Worksharing & Worksets
146. Create Workset – [name]
147. Rename Workset – [workset], [new name]
148. Open/Close Workset – [workset], [open/closed]
149. Set Workset Visibility in View – [workset], [view], [visible/hidden]
150. Checkout Elements (borrow) – [element/elements]
151. Relinquish Ownership – [element/elements] or [worksets]
152. Report Worksharing Status – on/off, worksets, owners
153. Synchronize with Central – NEEDS_REVIEW (real API, high-risk/slow — confirm explicitly first)

## Links
154. List Linked Models
155. Reload Link
156. Unload Link
157. Pin/Unpin Link
158. Move/Rotate Link
159. Set Link Visibility
160. Remove Link
161. Report Link Status
162. Bind Link – NEEDS_REVIEW
163. Copy/Monitor Link Elements – NEEDS_REVIEW

## Export
164. Export to DWG
165. Export to IFC
166. Export to PDF
167. Export Image from View
168. Export Room/Area Report

## Model Health & Cleanup
169. Report All Warnings
170. Report Unused Elements
171. Purge Unused – NEEDS_REVIEW (no single clean documented API equivalent to the UI command)

## Project / Document Level
172. Report Project Information
173. Set Project Information Parameter
174. Report Project Location/Coordinates
175. Report Design Options
176. Set Active Design Option
177. Set Shared Coordinates – NEEDS_REVIEW (multi-step, order-sensitive, genuinely risky to automate blind)

## Model Info & Orientation (read-only)
178. Active View Snapshot
179. Project Units
180. Workset Info
181. Model Categories
182. Used Families

---

## Not included here on purpose
The bespoke, multi-stage HVAC/MEP recipes (FCU placement, duct routing, MEP tracing, terminal layout,
family creation) are real and working, but they're **not** universal/category-agnostic actions — fixed
workflows for one specific job. They live in `scripts/recipes/` and their own skills, not this list.
