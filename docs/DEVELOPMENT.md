# Development and builds

## Building

The Windows frontend uses .NET Framework 4.8's compiler. The Linux backend must first be built from the pinned WinDrop source and product patch.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build-WinDrop.ps1 -BackendDirectory <backend-output-directory>
```

Runtime and source payloads are built in the provisioned Linux environment in [build notes](SETUP.md):

```bash
bash scripts/build-extra-radio-modules.sh
bash scripts/build-runtime-payload.sh
bash scripts/build-corresponding-source.sh
```

Then build the redistributable setup on Windows:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build-WinDropSetup.ps1
```

`Install-WinDrop.ps1` remains a developer app-only installer; it does not provision a fresh PC. Older console `AirDropLab` scripts are development helpers tied to the original test hardware. New kernels require matching driver builds and regression testing, not forced module loading.

