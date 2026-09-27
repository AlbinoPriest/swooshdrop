# WinDrop PC

An experimental Windows tray app for receiving files from an iPhone's native **Share → AirDrop** menu.

The prototype dedicates a TP-Link TL-WN725N USB adapter to a separate WSL distribution. OWL supplies the Apple Wireless Direct Link (AWDL) interface, WinDrop handles discovery and file reception, and the Windows tray app prompts for consent and saves files to Windows.

```text
iPhone AirDrop → USB Wi-Fi adapter → OWL / WSL → WinDrop → Windows files
                                                           ↑
                                                   Tray Accept/Decline
```

## Current status

Real single-photo and three-photo transfers succeeded with an iPhone 16 Pro Max on iOS 27.0. The Windows approval dialog and completion notification also worked. HEIC files arrived with conversion disabled, and the receiver does not recompress media. Exact equality with the phone originals has not been verified against source hashes.

Discovery is intermittent. The latest protocol patch repeats initial announcements, refreshes every 30 seconds, answers targeted queries, serializes bridge writes, and uses a stable tray service name. These changes pass tests; their effect on discovery reliability still needs phone testing. There are 216 passing WinDrop protocol tests and 52 passing OWL tests.

A small encrypted exchange averaged approximately 0.33 MB/s, including request metadata and preview. Sustained throughput, large files, Live Photos, metadata fidelity, cancellation, unplug/replug recovery, and prolonged use remain unverified. Contacts Only and Apple account identity integration are not implemented.

## Run on an already provisioned PC

This repository is source and setup documentation, **not a portable installer**. It requires the provisioned `AirDropLab` WSL distribution, matching driver modules, and usbipd-win. See [setup notes](docs/SETUP.md).

Build with **Windows PowerShell 5.1**, while the tray app is closed:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build-WinDropTray.ps1
.\WinDropTray.exe
```

1. Plug in the dedicated adapter and click **Start receiving**.
2. On iPhone, enable Wi-Fi and Bluetooth, select **Everyone for 10 Minutes**, and open **Share → AirDrop → WinDrop PC**.
3. Approve the expected transfer in the Windows dialog. Requests expire after 60 seconds; there is no automatic acceptance.
4. Use **Open received files** to access the `received` folder beside the executable.

Closing the window keeps the app in the tray. **Stop** returns the USB adapter to Windows; the tray menu's **Quit** exits. No automatic Windows startup is configured.

The current scripts deliberately target the test adapter's exact USB identity (`0bda:8179`, serial `00E04C0001`). A different adapter needs configuration and driver validation. The Intel Windows internet adapter is separate.

Console fallback:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Start-AirDropLab.ps1
```

That mode runs for 15 minutes and asks for approval in the console. Recovery:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Stop-AirDropLab.ps1
```

The helper targets the recorded lab process group and exact USB device; it does not shut down WSL or Docker. Run only one copy of the receiver, including copies launched from other folders.

## Repository contents

- `WinDropTray.cs`: native Windows Forms tray app and transfer consent UI.
- PowerShell and Bash scripts: USB attachment, radio startup, receiver supervision, and cleanup.
- `patches/`: WinDrop protocol/discovery changes and vendor driver compatibility changes.
- `scripts/`: source preparation and matching driver build helpers.
- `source-versions.json`: exact dependency revisions used for the prototype.
- `licenses/`: upstream license texts; [third-party notices](THIRD-PARTY-NOTICES.md).

Received photos, packet captures, local logs, WSL disks, credentials, and binaries are excluded from version control.

## License

Original wrapper and launcher code is MIT licensed. WinDrop is MIT licensed, OWL is GPL-3.0, and the vendor driver is GPL-2.0-only. Patches retain the licenses of their respective upstream projects. See [LICENSE](LICENSE) and [third-party notices](THIRD-PARTY-NOTICES.md).
