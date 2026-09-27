#!/usr/bin/env bash
set -euo pipefail
export LOCALVERSION=
cd /opt/airdrop-lab/kernel
zcat /proc/config.gz > .config
cp .config /opt/airdrop-lab/running-kernel.config
scripts/config --module RTL8XXXU
make olddefconfig
release=$(make -s kernelrelease)
if [ "$release" != "$(uname -r)" ]; then
    echo "Kernel version mismatch: $release != $(uname -r)" >&2
    exit 1
fi
echo "Building matching modules for $release, using eight workers."
make -j8 vmlinux
cp vmlinux.symvers Module.symvers
for module in drivers/usb/common drivers/usb/core; do
    make -j8 M="$module"
    cat "$module/Module.symvers" >> Module.symvers
done
make -j8 M=drivers/leds led-class.ko
cat drivers/leds/Module.symvers >> Module.symvers
for module in lib/crypto net/wireless net/mac80211 drivers/net/wireless/realtek/rtl8xxxu; do
    make -j8 M="$module"
    cat "$module/Module.symvers" >> Module.symvers
done
modinfo drivers/net/wireless/realtek/rtl8xxxu/rtl8xxxu.ko
