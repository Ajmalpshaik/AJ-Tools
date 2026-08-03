# Glossary — Ajmal's terms → Revit/AJ Tools terms

Maps how Ajmal actually says things (often dictated, sometimes garbled) to the exact Revit API / AJ Tools
meaning. Read this when a request uses ambiguous or misheard terms. Update it whenever a new term causes
confusion — that's the whole point of this file.

- **Element ID (Revit)** → a unique number Revit assigns to every single element in a project — walls,
  doors, pipes, rooms, views, sheets, everything. No two elements share the same ID in one model. Used to
  find, select, or track an element individually (e.g. Add-ins → Select by ID, `filter-by-id-list.cs`,
  or in schedules/reports). Stays the same for the life of that element in that model, but changes if the
  element is copied into another project.
  **A second, more stable identifier also exists: `Element.UniqueId`** — a GUID-based string stamped on
  the element at creation, distinct from the integer `Element.Id`. Prefer `Id` for anything within one
  live session (it's what every fragment already uses) — but if a script ever needs to re-identify the
  *same* element reliably across a worksharing sync or a detach-from-central, `UniqueId` is the one that
  survives that; `Id` is not guaranteed to. One-liner for reference: *"Element ID – unique identifier Revit
  assigns to each element in a project, used to locate, track, or reference elements individually (not
  the same as Type ID or Global ID in IFC)."* **Whenever a script/skill reports on specific elements**
  (not a bare count) — always include each one's Element ID in the output, so Ajmal can reference,
  re-select, or track that exact element afterward. The `report-*` action fragments already do this by
  default; keep it that way for any new one.
- **VCD** → Volume Control Damper. A *family* inside the **Duct Accessories** category
  (`OST_DuctAccessory`), not its own category.
- **"Fitting" / "hitting" (dictation)** → **ambiguous — do not assume Duct Fitting by default.**
  Could mean **Duct Fitting** (`OST_DuctFitting`) or **Pipe Fitting** (`OST_PipeFitting`). Check the
  surrounding context (is the conversation about ducts or pipes?) before picking one. If unclear, ask.
- **"Debt accessories" (dictation)** → Duct Accessories (`OST_DuctAccessory`).
- **"Mart mep tag" / "smart mep tag tool" (dictation)** → the **Smart MEP Tags** command
  (`CmdSmartMepTag.cs` → `SmartMepTagService`), tags Mechanical Equipment, Ducts, Pipes, Duct/Pipe
  Accessories, Cable Trays.
- **"HVAC plants" (dictation)** → HVAC / Mechanical **floor plan views** (e.g. "1 - Mech"), not physical
  plant equipment.
- **Sub-Discipline** → a project parameter on views (separate from the built-in `Discipline` parameter)
  used to further classify Mechanical-discipline views as HVAC vs Piping etc. String-valued, not an enum.
- **Units in speech** → numbers like "four thousand" almost always mean millimetres (mm) unless Ajmal
  says otherwise — he works in mm, not feet/inches, despite Revit's internal API using feet.
- **This project's pipe systems** (check `RBS_PIPING_SYSTEM_TYPE_PARAM` on the pipe/fitting to filter by
  one): **CDP** = Condensate Drain (`TCM_M_CDN_CDP - Condensate Drain`). **Water Supply** =
  `TCM_M_CDN_WSP -Water Supply`. **Refrigerant** = **any** system whose name/type contains **"DXS"** — not
  just a Suction+Liquid pair. Corrected 2026-07-08: the model actually has more DXS sub-types than
  originally thought — `DXS-SL` (Suction Line), `DXS-LL` (Liquid Line), plus `DXS-C` and `DXS-S` variants
  also showed up as real, separate system name+number instances (e.g. `DXS-C 1`, `DXS-S 3`). Filter on the
  `"DXS"` prefix broadly rather than hardcoding to SL/LL only.
- **CRAC** = Computer Room Air Conditioning unit — a subset of **Mechanical Equipment** (family name
  contains "CRAC"), not its own category. This project has two CRAC families: "TRG_CRAC_Air Cooled
  Condensing Unit_Vertical" (outdoor condenser) and "TRG_CRAC_Close Control Air Conditioning_NRG1003"
  (indoor unit).
- **CRAC unit identity — use the "Equipment Tag" instance parameter, not "Mark".** `Mark` is inconsistently
  populated on these families (blank on one indoor unit, set to unrelated location-style values like "133"
  or "WO-26" on others) — it does NOT reflect the `CAC*`/`ACU*` naming at all. `Equipment Tag` is the
  reliable one and always matches the `CAC<code>`/`ACU<code>` convention used everywhere else. Confirmed
  2026-07-08 when a fresh trace by `Mark` produced unfamiliar-looking labels (`WO-26`, `WO-50`, a blank) —
  switching to `Equipment Tag` revealed these were the exact same already-documented CAC/ACU units below,
  not a new/different equipment set as first assumed.
- **CRAC indoor/outdoor pairing — do NOT assume by tag name, the actual wiring is cross-connected.**
  Each indoor unit (`CAC<code>`) connects to **two** outdoor condensers, but **not** the ones with the
  matching code — it's an A↔B cross-connection, verified by physically tracing the refrigerant pipe runs
  (Equipment Tag naming does NOT reflect actual connectivity):
  - `CAC001A` → `ACU001B1` + `ACU001B2` (not ACU001A*)
  - `CAC001B` → `ACU001A1` + `ACU001A2` (not ACU001B*)
  - `CAC002A` → `ACU002B1` + `ACU002B2`
  - `CAC002B` → `ACU002A1` + `ACU002A2`
  This is consistent across all 4 systems, so it reads as deliberate redundancy wiring, not a tagging
  error. If a 5th system ever appears, verify its actual pairing by tracing rather than assuming the same
  A↔B pattern holds — confirm, don't extrapolate blindly. Tracing method: the model's connectors aren't
  Revit-"connected" between equipment and pipes (`Connector.IsConnected` is false, so a connector-graph
  walk fails) — instead, walk the pipe/fitting chain **geometrically**, matching each element's connector
  origin to the nearest other element's connector origin within ~50mm, continuing hop by hop until
  reaching a Mechanical Equipment element (or falling back to nearest-equipment-by-distance if the chain
  goes cold before actually reaching one).
  - **Re-confirmed 2026-07-08** via a second, independent bulk-clustering trace (see
    `live-model/mep-trace.md`): 5 of 8 legs (`CAC001A`↔both `ACU001B*`, `CAC002A`↔both `ACU002B*`,
    `CAC002B`↔`ACU002A2`) matched cleanly and geometrically on their own. The remaining 3 legs
    (`CAC001B`↔`ACU001A1`, `CAC001B`↔`ACU001A2`, `CAC002B`↔`ACU002A1`) share one physical header/manifold —
    the pipe network alone can't split them apart element-by-element without a deeper tee-by-tee walk — so
    those 3 were resolved from this already-documented pattern rather than independently re-derived. Treat
    the pattern as strongly reconfirmed, not just assumed.
- **"Schedule" → ambiguous, two different things — always check which one before acting:**
  1. A real **Revit model schedule** — an actual `ViewSchedule` element created in the document, shows up
     in the Project Browser, persists in the model (e.g. the "Air Terminal Schedule" created earlier).
  2. Just a **schedule-style table shown in the chat reply** — plain formatted text/markdown, nothing
     created in the model at all (e.g. the VCD size breakdown table).
  If Ajmal says "create a schedule" without making clear which, ask. Don't default to one — creating a
  real Revit element when he only wanted a quick chat table is unwanted model clutter; replying with a
  chat table when he wanted a real schedule means he has to ask again.

- **"MEP Color Data Standard"** → the master standards workbook at
  `D:\Ajmal\BIM Resources\NEW\Modeling\03_Standards\MEP Color Data Standard.xlsx`, `Sheet1`, one row per
  MEP system (duct or pipe). Columns: `Discipline_Code, Service_Code, Type, System_Name, System
  Classification, Main_System_Code, Sub_System_Code, System_Code, Abbreviation, System_Flow_Type,
  Element Type, TrueColor (RGB), HEX Code, Color Name, Description`. Same folder also holds
  `Family_Naming_Convention_Documentation.docx`, `Material_Code_Documentation.docx` (a DIFFERENT
  thing — defines a `Material_Abbreviation` shared-parameter code list like AL/GI/PVC for physical
  construction material, not the color-coding `Material` assets used for graphics), and
  `Tagging_Convention_Documentation.docx`. Full sync technique (Excel → Revit System Types → Materials →
  View Filters) is in `live-model/mep-color-standard.md`.
- **"Type" (Excel column) vs "System_Name" (Excel column)** → `Type` is the full compound code Ajmal wants
  as the Revit Type Name (e.g. `HVAC_AC_Return Air Duct System_RAD` = Service_Code + Main_System_Code +
  System_Name + Abbreviation, underscore-joined). `System_Name` is just the descriptive part alone (e.g.
  "Return Air Duct System") — used as the value for both the `System_Name` custom parameter AND the native
  `Description` field (on both the System Type and its Material).

### Log
- 2026-07-08 — Created, seeded from terms that came up during the duct/HVAC tagging session. Corrected:
  "fitting" is NOT always Duct Fitting — pipe fittings exist too, context decides.
- 2026-07-08 — Added: "schedule" is ambiguous between a real Revit `ViewSchedule` and a chat-only table —
  ask which one rather than assuming.
- 2026-07-16 — Added "MEP Color Data Standard" location/structure and the Type-vs-System_Name column
  distinction, from the standard-rollout session (see `live-model/mep-color-standard.md` for the full technique).
