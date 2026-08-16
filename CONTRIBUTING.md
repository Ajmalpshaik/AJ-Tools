# Contributing

## Repository Purpose

This is the development source repository for the `AJ Tools` add-in for Autodesk Revit. It is primarily used to:

- maintain source code for the AJ Tools add-in
- track feature requests and bug reports
- manage pull requests and code reviews
- publish tagged releases

## What To Open Here

Open a GitHub issue in this repository when you have:

- a reproducible bug in the add-in
- a feature request or improvement suggestion
- a question about add-in behavior or development

## Before Opening An Issue

Please include the practical details needed to investigate the problem:

- Revit version
- Windows version
- AJ Tools version
- a short reproduction sequence
- screenshots or error text if available

## Pull Requests

Pull requests are welcome for:

- bug fixes
- feature additions
- documentation improvements
- code quality enhancements
- build and packaging improvements

## Pull Request Expectations

Before opening a pull request:

1. Keep the scope focused and related to a single issue or feature.
2. Update documentation if behavior or usage changes.
3. Ensure code follows the existing style and conventions.
4. Test changes locally when possible.
5. Provide a clear description of the changes and why they are needed.

## Development Setup

1. Install Visual Studio 2022 with .NET desktop build tools. No local Revit install is needed to
   *build* — the Revit API is restored from NuGet; Revit is only needed to *run* the add-in.
2. Clone this repository.
3. Open `AJ Tools.sln` in Visual Studio (the .NET 9 SDK covers the 2020–2026 configs; the
   `Release R27` config needs the .NET 10 SDK, shipped with VS 2022 17.12+).
4. Pick the configuration for the Revit version you're targeting — `Debug`/`Release` (2020) or
   `Debug R21`…`Release R27` (2021–2027) — and build. See [README.md](README.md) Compatibility.
5. A normal build auto-deploys the add-in into the local Revit Addins folders for that version —
   pass `-p:SkipAjToolsAutoDeploy=true` for a compile-only build.

## Release Metadata

If your changes affect release packaging or versioning:

- update the assembly version in `src/Properties/AssemblyInfo.cs`
- update `CHANGELOG.md` with clear entries

## Support Routing

- **Source code issues**: use this repository
- **Download or installation issues**: use [AJ-Tools-Installer Issues](https://github.com/Ajmalpshaik/AJ-Tools-Installer/issues)
- **Security concerns**: email `ajmalnattika@gmail.com` privately
