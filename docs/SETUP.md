# Provisioning notes

For ordinary installation use **WinDropSetup.exe** and the [distribution guide](DISTRIBUTION.md). These notes describe the original developer build environment. Shipping builds use `scripts/build-extra-radio-modules.sh`, `scripts/build-runtime-payload.sh`, and `scripts/build-corresponding-source.sh`, then `Build-WinDropSetup.ps1`. MediaTek adapters remain experimental.

The working machine was provisioned interactively. These notes preserve the sources and build steps; they are not an unattended installer or a claim that every WSL kernel/adapter works.

## Windows and WSL

Use a separate Ubuntu 24.04 WSL 2 distribution named **AirDropLab**. Install usbipd-win from its [official releases](https://github.com/dorssel/usbipd-win/releases). See [Microsoft's USB passthrough instructions](https://learn.microsoft.com/en-us/windows/wsl/connect-usb).

The tested kernel is `6.18.33.2-microsoft-standard-WSL2`. No replacement kernel, `.wslconfig`, or Docker changes were used. WSL distributions share a kernel and loaded modules.

Identify the dedicated USB adapter with `usbipd list` and confirm its hardware ID and serial. The repository's Windows launchers, tray source, and Linux helpers currently match `0bda:8179` / `00E04C0001`. Keep those checks consistent if adapting to another device. Do not pass through the Windows internet adapter.

Once the identity is correct, run `Bind-TPLink.ps1` in an elevated PowerShell session to share that device. Attachment is performed by Start receiving; administrative sharing and routine attachment are separate steps.

## Linux dependencies and protocol sources

Inside AirDropLab as root:

```bash
apt-get update
apt-get install -y --no-install-recommends \
  build-essential bc flex bison libssl-dev libelf-dev dwarves \
  cmake git ca-certificates libpcap-dev libev-dev \
  libnl-3-dev libnl-genl-3-dev libnl-route-3-dev \
  iw usbutils kmod python3 iproute2 dotnet-sdk-8.0 \
  linux-firmware wireless-regdb zstd tcpdump libarchive-tools \
  python3-pil libheif-examples libheif-plugin-libde265
```

From the mounted repository directory, run:

```bash
bash scripts/prepare-protocol.sh
```

This builds the pinned OWL source and applies the WinDrop patch before building `/opt/airdrop-lab/receiver-product`, with a copy in `receiver-refresh` for older helpers. Run it only while receiving is stopped: rebuilding live dependency files is not supported. Existing source at another revision is rejected rather than overwritten. Pillow normalizes incoming previews; `heif-convert` generates gallery thumbnails from HEIC files without changing the saved originals.

## Matching radio driver

The stock `rtl8xxxu` driver recognized the device, but its firmware failed to start. Actual phone transfers used the adapted aircrack-ng `8188eu` driver instead. Its upstream is deprecated, and this compatibility work is experimental.

For the tested kernel, fetch these exact sources into the expected locations:

```bash
git clone --depth 1 --branch linux-msft-wsl-6.18.33.2 \
  https://github.com/microsoft/WSL2-Linux-Kernel.git /opt/airdrop-lab/kernel
git -C /opt/airdrop-lab/kernel rev-parse HEAD
# Expected: c21a03b2943d147c280bdf32530d4fe6badfd6bd

git clone https://github.com/aircrack-ng/rtl8188eus.git /opt/airdrop-lab/rtl8188eus
git -C /opt/airdrop-lab/rtl8188eus checkout --detach af3bf004458f76b7aec33e9ba552cd382ed1f5c3

bash scripts/build-radio-driver.sh
git -C /opt/airdrop-lab/rtl8188eus apply "$PWD/patches/driver-compatibility.patch"
make -C /opt/airdrop-lab/rtl8188eus -j8 \
  KSRC=/opt/airdrop-lab/kernel CONFIG_MP_INCLUDED=n CONFIG_LOAD_PHY_PARA_FROM_FILE=n
```

The build helper derives configuration from `/proc/config.gz` and checks the release against `uname -r`. Building `vmlinux` generates matching symbol versions; it can consume substantial time and disk space. It does **not** install or boot that kernel. The runtime expects the resulting `8188eu.ko` in the vendor source folder. A new WSL kernel requires matching source and a fresh compatibility assessment, not forced module loading.

Matching `cfg80211`, `mac80211`, and `led-class` modules must also be available through the running WSL module tree. Check `modinfo mac80211` and `modinfo led_class`; if they are absent or incompatible, stop and provision matching dependencies. The driver build helper also produces those modules, but their installation depends on the WSL module layout. Do not overwrite another distribution's module files blindly.

The launcher expects uncompressed `rtl8188eufw.bin`, `regulatory.db`, and `regulatory.db.p7s` under `/lib/firmware`. On the tested system the packaged firmware was `.zst`; it was decompressed without removing the packaged file:

```bash
zstd -dc /lib/firmware/rtlwifi/rtl8188eufw.bin.zst \
  > /lib/firmware/rtlwifi/rtl8188eufw.bin
```

Do this only if the compressed source exists and the uncompressed file is missing. Startup copies firmware into a shared WSL mount and temporarily sets the kernel firmware search path, then restores it after initialization. Other WSL distributions could observe that brief change.

## Checks and limitations

WinDrop protocol tests:

```bash
cd /opt/airdrop-lab/windrop
WINDROP_PYTHON=/usr/bin/python3 dotnet test \
  tests/WinDrop.Protocol.Tests/WinDrop.Protocol.Tests.csproj -c Release
```

Some upstream archive oracle tests look for a tool named `tar.exe`. To exercise those on Linux, supply a test-only `tar.exe` symlink to `bsdtar` in a directory added to the test process's PATH. The normal `tar` command does not satisfy that check. The prototype test results included the Python and libarchive oracles.

Build with `Build-WinDrop.ps1`, then run `Install-WinDrop.ps1 -Launch` in Windows PowerShell 5.1. Windows .NET Framework 4.8 supplies WPF and the compiler; a Windows .NET SDK is unnecessary. The installer creates desktop/Start menu shortcuts, a COM notification callback CLSID, and an optional `windrop:` URI handler. Native notifications use foreground activation and the COM callback, not protocol launching. The running app keeps its class factory registered while receiving; callbacks dispatch to its UI thread. Files default to Downloads\WinDrop. Optional sign-in startup is controlled in Settings and uses the current user's Run registry key. Start only one receiver; the Linux helper also uses an exclusive session lock.

Relay/control listeners bind to IPv6 loopback. AirDrop listens on the AWDL interface. Transfer consent is essential: the display name is supplied by the sender and is not verified Apple identity. The app requires an explicit decision for each transfer and rejects unanswered requests.

This TL-WN725N has only 2.4 GHz, and active monitor mode was unsupported. Discovery and throughput are experimental. Start/Stop works on the provisioned test PC; a complete fresh-machine installation, automatic recovery, and long-running behavior still need validation.
