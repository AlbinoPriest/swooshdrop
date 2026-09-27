#!/usr/bin/env bash
set -euo pipefail
[[ $(uname -m) == x86_64 && $(uname -r) == 6.18.33.2-microsoft-standard-WSL2 ]] || { echo 'This beta requires the supported x64 WSL kernel 6.18.33.2. Do not force-load its drivers.'; exit 1; }
payload=$1
marker=/opt/airdrop-lab/.windrop-managed-runtime
if [[ -d /opt/airdrop-lab && ! -f "$marker" ]]; then echo 'Existing unmanaged runtime; refusing to overwrite it.'; exit 1; fi
export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y dotnet-runtime-8.0 libpcap0.8t64 libev4t64 libnl-3-200 libnl-genl-3-200 libnl-route-3-200 python3-pil libheif-examples libheif-plugin-libde265 iw kmod iproute2 ca-certificates zstd
tar -tzf "$payload" | python3 -c 'import sys; from pathlib import PurePosixPath as P; names=sys.stdin.read().splitlines(); assert names and all((n in ("opt/", "opt/airdrop-lab/") or n.startswith("opt/airdrop-lab/")) and ".." not in P(n).parts for n in names), "Invalid payload paths"'
tar -xzf "$payload" -C /
printf 'SwooshDrop dedicated runtime (WinDropRuntime compatibility identity)\n' > "$marker"
depmod -b /opt/airdrop-lab/drivers "$(uname -r)"
ldd /opt/airdrop-lab/owl/build/daemon/owl | tee /tmp/windrop-link-check.txt
! grep -q 'not found' /tmp/windrop-link-check.txt
dotnet --list-runtimes | grep -q 'Microsoft.NETCore.App 8\.'
python3 -c 'from PIL import Image; assert Image.new("RGB", (1,1)).size == (1,1)'
echo 'SwooshDrop runtime ready.'
