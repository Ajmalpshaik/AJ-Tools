# Folder Structure

```text
AJ Tools/
├─ Addin/                 # Manifest template(s)
├─ dist/                  # Packaging and install scripts + generated payload
│  └─ release/            # Generated versioned release zips (ignored by git)
├─ docs/                  # User and developer documentation
│  └─ images/             # README/document images
├─ src/                   # Revit add-in source code
│  ├─ Commands/           # Revit command entry points
│  ├─ Core/               # App startup + ribbon registration
│  ├─ Helpers/            # Shared helper utilities
│  ├─ Models/             # DTOs/enums/state models
│  ├─ Properties/         # Assembly metadata
│  ├─ Resources/          # Icons and UI assets copied next to DLL
│  ├─ Services/           # Business logic per feature
│  └─ UI/                 # XAML windows and styles
├─ AJ Tools.sln
├─ Directory.Build.props
├─ Directory.Build.targets
└─ README.md
```

## Structure Rules
- Keep production source code in `src/`.
- Keep release/install automation under `dist/`.
- Keep end-user documentation under `docs/`.
- Do not commit generated files from `src/bin`, `src/obj`, `.vs`, or `dist/release`.
