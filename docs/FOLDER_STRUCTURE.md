# Folder Structure

```text
AJ Tools/
|-- Addin/                  Manifest template
|-- dist/                   Packaging, install, uninstall, and tag scripts
|   `-- release/            Generated release payloads (ignored by Git)
|-- docs/                   Product and repository documentation
|   `-- images/             README and documentation images
|-- src/                    Revit add-in source code
|   |-- Commands/           Revit command entry points
|   |-- Core/               Startup and ribbon registration
|   |-- Helpers/            Shared helper utilities
|   |-- Models/             DTOs, enums, and state models
|   |-- Properties/         Assembly metadata
|   |-- Resources/          Icons and copied runtime assets
|   |-- Services/           Business logic and feature services
|   `-- UI/                 XAML windows, styles, and code-behind
|-- AJ Tools.sln            Visual Studio solution
|-- Directory.Build.props   Shared MSBuild properties
|-- Directory.Build.targets Shared MSBuild targets
`-- README.md               Repository overview
```

## Structure Rules

- Keep production source code in `src/`.
- Keep packaging and release tooling in `dist/`.
- Keep public-facing repository docs in the repo root or `docs/`.
- Keep generated files out of Git, especially `bin/`, `obj/`, `.vs/`, `dist/release/`, and local build logs.
