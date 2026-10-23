# Development and builds

Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-UpdateChecker.ps1` for update selection and release URL tests; append `--live` to check the public GitHub API too. Update checks use the releases list so prereleases are included, require an uploaded `SwooshDropSetup.exe`, and never start WSL. Successful checks are cached in `Data\updates.json` for 24 hours. Failed automatic checks retry the next day while the app remains open; manual checks are always available.

## Building

Sending starts its own BLE wake session after recipient selection, with a 1.5-second warm-up before opening the transfer. The beacon remains active for the outgoing operation, bounded by five minutes; search-only beacons remain bounded by 32 seconds. Initial TCP establishment may retry twice after a transient connection refusal. TLS, consent and upload are never replayed automatically.

Bluetooth wake-up uses Windows' `BluetoothLEAdvertisementPublisher` during an explicit device search. Building the frontend now requires Windows 10/11 SDK metadata and a .NET Framework targeting pack; end users do not need those build tools. The beacon contains an AirDrop type-0x05 TLV with empty contact hashes, so Everyone mode is required. Advertising ends when the search finishes, is cancelled, the receiver stops, or the app exits, with a 32-second timer as a fallback. A Windows Bluetooth adapter that supports LE advertising is needed for automatic receiver wake-up. The development PC's publisher reached `Started`, and the integrated search subsequently discovered a recipient; iPhone transfer completion remains unvalidated.

The Windows frontend uses .NET Framework 4.8's compiler. The Linux backend must first be built from the pinned WinDrop source and product patch.

In the provisioned development distro, run `bash scripts/build-product-backend.sh --test` from this repository to build `/opt/airdrop-lab/receiver-product` from the pinned upstream commit and shipped patch. Then run `Build-WinDrop.ps1` on Windows to embed those binaries. `backend/OutgoingController.cs` and its tests mirror the new files in `patches/prototype-changes.patch`; keep them in sync when changing the sender.

The Send page shares the running receiver's transport. Its JSON stdin commands are `browse`, `send` (selected peer ID and absolute Linux file paths), and `cancel-outgoing`. Incoming consent remains routed by transfer ID. Peer selection expires after two minutes, searches stop after 30 seconds, and outgoing transfers time out after five minutes. Only regular files are supported initially; folders, duplicate archive names, and more than 100 files are rejected. Successful upload acknowledgement, rather than reaching 100% of source bytes, marks completion. No files are transcoded. Hardware testing on a real receiving iPhone is still required.

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
