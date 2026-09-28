#!/usr/bin/env bash
set -euo pipefail
repo=$(cd -- "$(dirname "$0")/.." && pwd)
lab=/opt/airdrop-lab
stage=$(mktemp -d "$lab/work/source.XXXXXX")
trap 'rm -rf -- "$stage"' EXIT
mkdir -p "$stage/kernel" "$stage/owl" "$stage/rtl8188eus" "$stage/windrop" "$stage/packaging" "$repo/obj"
git -C "$lab/kernel" archive c21a03b2943d147c280bdf32530d4fe6badfd6bd | tar -x -C "$stage/kernel"
cp "$lab/kernel/.config" "$stage/kernel/windrop.config"
cp "$lab/kernel/Module.symvers" "$stage/kernel/Module.symvers"
cp "$lab/work/kernel-shipping/.config" "$stage/kernel/windrop-mediatek.config"
git -C "$lab/owl" archive da255a70f221784c836d943dd3f243bc798f223b | tar -x -C "$stage/owl"
git -C "$lab/owl" submodule foreach --quiet 'git archive HEAD | tar -x -C "'"$stage"'/owl/$sm_path"'
git -C "$lab/rtl8188eus" archive af3bf004458f76b7aec33e9ba552cd382ed1f5c3 | tar -x -C "$stage/rtl8188eus"
git -C "$lab/rtl8188eus" diff HEAD > "$stage/rtl8188eus/windrop-compatibility.patch"
git -C "$lab/windrop" archive 5a5dc52ce6eda298789622cb92d548d64d5673cc | tar -x -C "$stage/windrop"
cp "$repo/patches/prototype-changes.patch" "$stage/windrop/windrop-product.patch"
cp -a "$repo/scripts" "$repo/licenses" "$repo/docs" "$repo/THIRD-PARTY-NOTICES.md" "$stage/packaging/"
cat > "$stage/README.txt" <<'EOF'
Corresponding source for SwooshDrop 0.3.2 runtime.
Kernel: Microsoft WSL Linux c21a03b2943d147c280bdf32530d4fe6badfd6bd.
Copy windrop.config to .config, use LOCALVERSION= and make olddefconfig;
make -j8 vmlinux modules to generate matching symbol versions and modules.
MediaTek USB modules use windrop-mediatek.config; packaging/scripts/build-extra-radio-modules.sh records the additional build.
Vendor driver: apply windrop-compatibility.patch with git apply, then make KSRC=<kernel-source-path> CONFIG_MP_INCLUDED=n CONFIG_LOAD_PHY_PARA_FROM_FILE=n -j8.
OWL: cmake -S owl -B owl/build -DCMAKE_BUILD_TYPE=Release; cmake --build owl/build -j8.
Ubuntu 24.04 dependencies: build-essential cmake pkg-config libpcap-dev libev-dev libnl-3-dev libnl-genl-3-dev libnl-route-3-dev flex bison libssl-dev libelf-dev bc dwarves.
WinDrop protocol: apply windrop-product.patch then dotnet build src/WinDrop.Cli -c Release.
Licenses are retained within each source tree. The Windows frontend source and release packaging are at https://github.com/AlbinoPriest/swooshdrop.
EOF
tar -C "$stage" -czf "$repo/obj/corresponding-source.tar.gz" .
echo 'Corresponding source archive ready.'
