# WinDrop PC

Receive photos, files, and web links from your iPhone's native **Share → AirDrop** menu on Windows. WinDrop 0.2.2 provides a native Windows app, a received-files gallery, image previews, notification approval buttons, and optional startup at sign-in.

This is a hardware beta for a provisioned PC. It dedicates a TP-Link TL-WN725N USB adapter to the separate **AirDropLab** WSL distribution. OWL supplies AWDL, WinDrop handles AirDrop, and the Windows app saves files. The EXE embeds the protocol runtime and app resources; usbipd-win, WSL, Linux image codecs, and the matching radio driver remain separate prerequisites. See [provisioning notes](docs/SETUP.md).

## Using the app

1. Launch **WinDrop** from the desktop or Start menu with the dedicated adapter connected. Receiving starts automatically by default.
2. On iPhone, enable Wi-Fi and Bluetooth, use **Everyone for 10 Minutes**, and choose **Share → AirDrop → WinDrop PC**.
3. Check the sender and preview, then click **Accept** or **Decline** in the Windows notification. You can also approve in the app. Unanswered requests expire after 55 seconds.
4. Browse received items in the gallery. **Open** opens a file or a saved HTTP/HTTPS link; **Show in folder** selects its saved file.

Files default to **Downloads\WinDrop**. Settings lets you change this folder, select receiving on launch, and enable **Start WinDrop when I sign in to Windows**. Sign-in startup is off unless enabled. Closing the window leaves WinDrop in the tray; **Stop receiving** returns the adapter to Windows, and tray **Quit** stops receiving and exits.

Photo thumbnails are separate cached previews. Received files are not resized or recompressed by WinDrop. The app requests unconverted media; the sender ultimately controls what it shares. Web links are shown before approval, stored as text files, and opened in the default browser after an accepted transfer finishes. Disable **Open accepted web links in my browser** in Settings to save links without automatically opening them; **Open** remains available in the gallery. History, settings, previews, and diagnostics live under `%LOCALAPPDATA%\Programs\WinDrop\Data`.

Discovery is still intermittent and can take time. Contacts Only and Apple identity integration are not implemented; sender names are unverified. Native notifications depend on Windows notification settings. A custom approval popup is used when notification delivery reports an error or previews are disabled.

## Build and install

On a PC with the backend and radio already provisioned, use Windows PowerShell 5.1:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build-WinDrop.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-WinDrop.ps1 -Launch
```

The build uses the Windows .NET Framework 4.8 WPF compiler and embeds the Linux .NET 8 backend from `/opt/airdrop-lab/receiver-product`. Output is `dist\WinDrop.exe`. The installer places it under `%LOCALAPPDATA%\Programs\WinDrop`, creates shortcuts, and registers notification activation. Notification buttons use a registered COM desktop callback; their arguments are passed directly to the running app, without asking Windows to open a URI. Quit the running app before updating. `Build-WinDropTray.ps1` remains a compatibility wrapper for the original build command.

These are unsigned local builds, not a signed installer for fresh PCs. The current scripts deliberately target USB identity `0bda:8179`, serial `00E04C0001`; another adapter requires driver validation and configuration. Windows' main internet adapter stays separate.

Console fallback: run `Start-AirDropLab.ps1`. It asks for console approval and runs for 15 minutes. `Stop-AirDropLab.ps1` stops the recorded lab session and detaches the exact USB device; it does not shut down WSL or Docker. Run only one receiver, including console copies.

## Validation

Real single-photo and three-photo transfers succeeded with an iPhone 16 Pro Max on iOS 27.0. HEIC and PNG files were received with the new gallery and preview system. Safari-link reception was recorded in the gallery, and a fresh photo transfer completed after a native notification Accept callback. COM Show/Accept/Decline routing and optional startup enable/disable were checked. The protocol suite has **224 passing tests**, including accepted/declined links without an upload and rejection of unsafe URL schemes; OWL has **52 passing tests**. Exact equality with iPhone originals has not been checked against source hashes.

A small encrypted exchange averaged about 0.33 MB/s including metadata and preview. Sustained throughput, large files, Live Photos, metadata fidelity, prolonged use, and fresh-machine installation remain unverified.

## Source and licenses

The repository contains the Windows UI, build/install and radio helpers, pinned dependency revisions, and protocol/driver patches. Received media, credentials, diagnostics, and build artifacts are excluded from source control.

Original app code and WinDrop are MIT licensed. OWL is GPL-3.0; the vendor driver is GPL-2.0-only. The EXE embeds MIT-licensed WinDrop assemblies and original app resources, not OWL or driver binaries. Include [LICENSE](LICENSE), [WinDrop's notice](licenses/WinDrop-LICENSE.txt), and [third-party notices](THIRD-PARTY-NOTICES.md) when distributing builds.
