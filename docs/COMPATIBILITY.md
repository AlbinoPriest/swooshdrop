# Hardware compatibility

The radio needs monitor mode and raw Wi-Fi frame transmission for OWL/AWDL. Chipset, driver, firmware, kernel, and USB passthrough all matter. A Windows adapter being able to connect to a router is not enough.

| Driver | Family | Status |
| --- | --- | --- |
| `8188eu` | RTL8188EU | USB ID `0bda:8179` tested with a TL-WN725N; other IDs experimental |
| `rtl8xxxu` | Supported older Realtek USB chipsets | Experimental; the tested ID uses the vendor driver instead |
| `mt7601u` | MediaTek MT7601U | Experimental |
| `mt76x0u` | MediaTek MT7610U family | Experimental |
| `mt76x2u` | MediaTek MT7612U/MT7662U family | Experimental |

The 86 exact device IDs and selected drivers are in [`adapters.json`](../adapters.json). Manufacturer names can hide different chipsets across hardware revisions. Setup matches USB vendor/product IDs. Two RTL8723BU IDs are excluded because their Bluetooth firmware variant is not included. A catalog match is not proof of successful AirDrop.

Only one receiver may run across WSL distributions. A shared lock prevents competing sessions. WinDrop validates the selected USB identity and rejects ambiguous Linux matches before rebinding radios. It stores the Windows instance identity rather than a hard-coded test-device serial. Adapters without genuine USB serials may need selecting again after changing ports.

The selected adapter is unavailable to Windows while attached to WSL. Use a separate internet connection. Built-in PCI Wi-Fi, sharing the Windows internet adapter, arbitrary USB adapters, ARM, 32-bit PCs, emulated radios, and Contacts Only are not supported by this beta.

This release supports x64 kernel **6.18.33.2-microsoft-standard-WSL2**. Future WSL kernels need matching modules and regression testing. Setup does not downgrade an existing kernel, replace its boot settings, overwrite global `/lib/modules`, or alter `.wslconfig`.

## Second-PC validation

Use a Windows 10 22H2 or Windows 11 x64 PC with a compatible dedicated adapter, internet, and virtualization enabled. Run setup without the development checkout. Check prerequisites, any manual restart, adapter selection, first launch, single/batch photos, a Safari link, notification approval, Stop/Start, optional sign-in startup, repair, and removal. Verify Windows networking and other WSL applications. Record exact USB ID and Windows/WSL versions. Experimental entries stay experimental until real phone tests pass.
