# WinDrop PC

Receive photos, files, and web links from your iPhone's native **Share → AirDrop** menu on Windows. Includes a received-files gallery, image and web-address previews, notification approval buttons, automatic opening of accepted links, a tray app, and optional startup at sign-in.

**0.3 is an installer beta.** Download **[WinDropSetup.exe](https://github.com/AlbinoPriest/windrop-pc/releases/tag/v0.3.0)**, connect a compatible **dedicated external USB Wi-Fi adapter**, and run setup. The adapter is reserved for AirDrop while receiving; Windows keeps its separate internet connection. Setup creates its own `WinDropRuntime` WSL environment. No iPhone app is needed.

## Requirements and compatibility

- Windows 10 22H2 or Windows 11, Intel/AMD x64, virtualization, and WSL 2. The development Windows 11 PC has been tested; a fresh second PC and Windows 10 are unverified.
- This beta's radio modules require **6.18.33.2-microsoft-standard-WSL2**. Setup refuses incompatible kernels; it does not downgrade WSL or boot a custom kernel.
- A compatible dedicated USB Wi-Fi adapter and separate internet connection. **TP-Link TL-WN725N, USB ID `0bda:8179` (RTL8188EU)** has passed phone transfers. Other revisions may differ.
- Administrator permission for initial prerequisites and sharing the selected USB device. Everyday receiving runs as your normal user.
- Internet and several GB of free space for initial setup. The approximately 256 MiB installer includes source materials; setup downloads a verified Ubuntu image and runtime packages.

The catalog contains **86 USB device IDs** covered by bundled Realtek and MediaTek drivers. All except the tested ID are **experimental**, not guaranteed AirDrop compatibility. Arbitrary adapters, built-in PCI Wi-Fi, sharing Windows' internet adapter, ARM/32-bit PCs, and Contacts Only are unsupported. See [compatibility](docs/COMPATIBILITY.md).

## Install and receive

1. Run **WinDropSetup.exe**, then **Check prerequisites**. Restart manually if Windows requests it, then rerun setup. Existing WSL distributions and Docker are preserved.
2. Select your dedicated USB adapter. Setup shows its device ID and tested/experimental status. Choose optional startup, then **Install WinDrop**.
3. Open WinDrop. On iPhone enable Wi-Fi and Bluetooth, choose **Everyone for 10 Minutes**, then **Share → AirDrop → WinDrop PC**.
4. Review the sender and preview and click **Accept** or **Decline** in the notification or app. Unanswered requests expire after 55 seconds. Sender display names are unverified.

Files default to **Downloads\WinDrop**. Thumbnails are separate cached previews. WinDrop does not resize or recompress received file streams; the iPhone controls what it shares. Exact equality with iPhone originals has not been verified by hashes. Accepted HTTP/HTTPS links are saved and open in the default browser; disable that behavior in Settings if desired.

Closing the window leaves WinDrop receiving in the tray. **Stop receiving** returns the adapter to Windows. Tray **Quit** stops the session and exits. **Settings → Adapter setup** stops receiving and opens setup to change hardware or repair the runtime.

## Updates and removal

Quit the running app before rerunning setup. Gallery history and preferences are preserved, except for the explicitly chosen adapter and startup setting. Remove **WinDrop PC** through Windows Installed Apps. Removal keeps received files, history, source, cache, and the dedicated runtime for reinstall; shared WSL/usbipd prerequisites remain installed. See [installation details](docs/DISTRIBUTION.md) for paths and cleanup.

## Validation and limits

Earlier versions received single photos, three-photo batches, HEIC/PNG files, and Safari links from an iPhone 16 Pro Max on iOS 27.0. The protocol suite had **224 passing tests** and OWL **52 passing tests**. This version's setup engine provisioned a clean Ubuntu runtime, installed and launched the app on the development PC, and advertised the receiver through that runtime. Native COM notification callbacks and startup registration were checked again.

Discovery remains intermittent. Sustained throughput, large files, Live Photos, metadata fidelity, prolonged use, reboot recovery, experimental adapters, and fresh second-PC installation need testing. A successful setup on the development PC does not establish universal compatibility. Builds are currently unsigned.

## Building

The Windows frontend uses .NET Framework 4.8's compiler. The Linux backend must first be built from the pinned WinDrop source and product patch.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build-WinDrop.ps1 -BackendDirectory <backend-output-directory>
```

Runtime and source payloads are built in the provisioned Linux environment in [build notes](docs/SETUP.md):

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

## Attribution and licenses

The Windows frontend is MIT-licensed integration work. The AirDrop protocol implementation is adapted from [UvejsGj/WinDrop](https://github.com/UvejsGj/WinDrop); [OWL](https://github.com/seemoo-lab/owl) implements AWDL. This project does not claim to have invented either implementation.

Setup embeds OWL, GPL kernel/driver modules, minimal firmware, notices, and their corresponding source. Those sources are installed under `Sources`. Ubuntu/codec packages are downloaded separately. See [notices](THIRD-PARTY-NOTICES.md) and [pinned source revisions](source-versions.json). Personal media, captures, credentials, logs, and Linux home directories are excluded from packages and source control.
