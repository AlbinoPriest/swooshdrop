#!/usr/bin/env bash
set -euo pipefail
export LOCALVERSION=
lab=/opt/airdrop-lab
tree="$lab/work/kernel-shipping"
mkdir -p "$lab/work"
if [[ ! -d "$tree" ]]; then
    git -C "$lab/kernel" worktree add --detach "$tree" c21a03b2943d147c280bdf32530d4fe6badfd6bd
fi
cd "$tree"
zcat /proc/config.gz > .config
scripts/config --keep-case --module MT76x2U --module MT76x0U --module MT7601U
make olddefconfig
[[ "$(make -s kernelrelease)" == "$(uname -r)" ]] || { echo 'Kernel mismatch'; exit 1; }
make -j8 modules_prepare
cp "$lab/kernel/Module.symvers" Module.symvers
make -j8 M=drivers/net/wireless/mediatek/mt76
make -j8 M=drivers/net/wireless/mediatek/mt7601u
echo 'Extra USB radio modules built; phone compatibility remains experimental.'
