# Installation and distribution

`SwooshDropSetup.exe` is a per-user GUI installer using .NET Framework 4.8. It contains the app, Linux runtime payload, adapter catalog, bootstrap, notices, and corresponding GPL source archive. Copy it to another PC without this development folder. Internet is required for initial runtime provisioning.

Requires Windows 10 22H2 or Windows 11, Intel/AMD x64, virtualization and WSL 2, a supported dedicated USB Wi-Fi adapter, and a separate internet connection. Only the development Windows 11 PC has been checked; clean Windows 10 and a second PC remain unverified.

Prerequisite downloads use pinned SHA-256 hashes. WSL/usbipd MSI files also require valid Authenticode signatures. Ubuntu's image comes from the dated `20240423` release snapshot, with its independently checked official checksum. Ubuntu runtime packages use APT repository verification and receive current repository updates during provisioning. Checksum failures stop installation.

| Item | Location |
| --- | --- |
| App and setup | `%LOCALAPPDATA%\Programs\WinDrop` (internal path retained for upgrades) |
| History, preferences, previews, app logs | `Data` under that folder |
| Fresh runtime VHD | `Data\WSL` under that folder |
| Prerequisite downloads | `SetupCache` under that folder |
| Corresponding GPL source and notices | `Sources` under that folder |
| Received files | `Downloads\SwooshDrop` on new installs; existing users keep their selected folder |
| WSL distribution | `WinDropRuntime` |

The approximately 256 MiB installer embeds the source archive; first setup additionally downloads an approximately 340 MB Ubuntu image and Linux packages. Allow several GB of free space. No personal photos, captures, credentials, or development Linux home directories are packaged.

Setup will not overwrite an existing same-named distro without its `/etc/windrop-owned-distro` marker. The bootstrap also checks its managed runtime marker. Drivers use a private module tree in `/opt/airdrop-lab/drivers`; the existing kernel and WSL module files are preserved.

Administrator steps enable features, install signed prerequisites, and share the exact selected USB device after rechecking its identity. WSL import, settings, shortcuts, notification registration, and startup use the original Windows user's process. Setup does not install a custom kernel, downgrade WSL, terminate Docker, shut down all WSL distributions, or reboot automatically. Restart manually if requested, then rerun setup.

Quit the app before repair or update, and detach the selected USB device. History is preserved; adapter and startup selections are updated explicitly. A failed setup does not mean the app is ready. Interrupted unmarked imports are never automatically deleted.

Windows Installed Apps can remove the frontend and Windows registrations. Received files, history, source, cache, and runtime are retained for reinstall; shared WSL/usbipd prerequisites are retained. For complete cleanup, quit SwooshDrop, back up anything wanted, then remove only its own data/cache/source folders. Verify the distro ownership marker before explicitly unregistering **WinDropRuntime** with WSL. Unregister permanently deletes that distro's Linux filesystem; it does not remove files in Downloads. Never unregister another distro or remove shared WSL merely to remove SwooshDrop.

The beta is unsigned. Signing, fresh-PC validation, more kernel packages, and demonstrated adapter reliability are needed before a broad production release.

## WSL memory and background use

Task Manager's `VmmemWSL` is the shared WSL 2 virtual machine, so its memory includes other running distributions and Linux file cache. It is not SwooshDrop's process memory alone. On the development PC, the running receiver, OWL, and bridge together used about 95 MiB of resident memory while `VmmemWSL` showed more than 2 GiB after build work; roughly 1.5 GiB inside WSL was reclaimable buffer/cache at that point. The VM's working set later dropped to about 1 GiB without restarting the receiver. When setup provisions or repairs the dedicated runtime, it disables Ubuntu's optional Windows Ubuntu Pro bridge, which otherwise retries without an agent, and caps its system journal at 64 MiB. These changes reduce unnecessary background work and disk-backed cache, but do not impose a global WSL memory limit or change other distributions. WSL can still use more memory while receiving or while other WSL workloads run.
