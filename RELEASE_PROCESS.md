# Release Process

`AJ Tools` and `AJ-Tools-Installer` have different jobs:

- `AJ Tools`: source code, assembly version, package creation, source changelog
- `AJ-Tools-Installer`: public release zip, checksum, GitHub Release page, installer support

## Release Rules

- Use one version number across `AssemblyInfo.cs`, the packaged zip, the source tag, and the installer tag.
- Do not create installer-only product version numbers.
- Do not push a source tag before the package and installer repo are ready.
- Release from a clean working tree.

## Release Standards

- Source tag: `vX.Y.Z`
- Installer tag: `vX.Y.Z`
- GitHub Release title: `AJ Tools vX.Y.Z`
- Installer asset: `AJ-Tools-vX.Y.Z.zip`
- Checksum file: `SHA256SUMS.txt`
- Historical legacy tags with other formats remain in Git history and should not be reused.

## Release Checklist

1. Update release metadata in this repository:
   - `src\Properties\AssemblyInfo.cs`
   - `CHANGELOG.md`
   - any affected README or install documentation
2. Commit the source-repo release changes.
3. Build the package:

```powershell
powershell -ExecutionPolicy Bypass -File .\dist\package.ps1 -Configuration Release
```

4. Confirm the output file exists and matches the intended version:

- `dist\release\AJ-Tools-vX.Y.Z.zip`

5. Prepare the installer repository:

```powershell
Set-Location ..\AJ-Tools-Installer
powershell -ExecutionPolicy Bypass -File .\tools\prepare-release.ps1 -SourceRepoPath "..\AJ Tools" -Version X.Y.Z
```

6. Review the installer payload:

- `releases\AJ-Tools-vX.Y.Z.zip`
- `releases\SHA256SUMS.txt`

7. Publish the installer repository release:

```powershell
git add releases CHANGELOG.md README.md INSTALL.md RELEASE_PROCESS.md SUPPORT.md SECURITY.md .github tools
git commit -m "release: vX.Y.Z"
git tag vX.Y.Z
git push origin main --tags
```

8. Push the source repository branch and matching tag:

```powershell
Set-Location ..\AJ Tools
git push origin HEAD
powershell -ExecutionPolicy Bypass -File .\dist\create-tag.ps1 -Version X.Y.Z -Push
```

## Important Notes

- `dist\create-tag.ps1` validates that the requested tag matches `AssemblyVersion`.
- The installer repository should contain installer assets only, not source-code artifacts.
- GitHub Releases for end users should be created from `AJ-Tools-Installer`, not from this repository.
