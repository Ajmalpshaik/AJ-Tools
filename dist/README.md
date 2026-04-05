# dist Folder

This folder contains packaging and install scripts.

Generated payload files (`AJ Tools.dll`, dependency DLLs, `Resources/`, `AJ Tools.addin`, and `release/`) are created by:

```powershell
powershell -ExecutionPolicy Bypass -File .\dist\package.ps1 -Configuration Release
```

Install scripts:
- `install.cmd` -> current user install
- `install-all-users.cmd` -> current user + all users (run as Administrator)
- `uninstall.cmd` -> current user uninstall
- `uninstall-all-users.cmd` -> current user + all users uninstall (run as Administrator)
