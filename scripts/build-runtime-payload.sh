#!/usr/bin/env bash
set -euo pipefail
repo=$(cd -- "$(dirname "$0")/.." && pwd)
lab=/opt/airdrop-lab
release=$(uname -r)
[[ "$release" == 6.18.33.2-microsoft-standard-WSL2 ]] || { echo 'Unsupported build kernel'; exit 1; }
stage=$(mktemp -d "$lab/work/payload.XXXXXX")
trap 'rm -rf -- "$stage"' EXIT
base=$stage/opt/airdrop-lab
mkdir -p "$base/owl/build/daemon" "$base/windrop/tools" "$base/drivers/lib/modules/$release/extra" "$base/firmware/rtlwifi" "$base/firmware/mediatek" "$base/notices" "$repo/obj"
cp "$lab/owl/build/daemon/owl" "$base/owl/build/daemon/"
cp "$lab/windrop/tools/windrop-bridge.py" "$base/windrop/tools/"
for file in "$lab/kernel/net/wireless/cfg80211.ko" "$lab/kernel/net/mac80211/mac80211.ko" "$lab/kernel/lib/crypto/libarc4.ko" "$lab/kernel/drivers/leds/led-class.ko" "$lab/rtl8188eus/8188eu.ko" "$lab/kernel/drivers/net/wireless/realtek/rtl8xxxu/rtl8xxxu.ko"; do
    cp "$file" "$base/drivers/lib/modules/$release/extra/"
done
find "$lab/work/kernel-shipping/drivers/net/wireless/mediatek" -name '*.ko' -exec cp '{}' "$base/drivers/lib/modules/$release/extra/" \;
find "$base/drivers" -name '*.ko' -exec strip --strip-debug '{}' \;
strip "$base/owl/build/daemon/owl"
cp /lib/firmware/rtlwifi/rtl8188eufw.bin "$base/firmware/rtlwifi/"
while IFS= read -r name; do
    [[ -n "$name" ]] || continue
    [[ "$name" == rtlwifi/rtl8723bu_bt.bin ]] && continue # Unavailable firmware; exclude those USB IDs below.
    mkdir -p "$base/firmware/$(dirname "$name")"
    if [[ -f "/lib/firmware/$name" ]]; then cp "/lib/firmware/$name" "$base/firmware/$name"; else zstd -dc "/lib/firmware/$name.zst" > "$base/firmware/$name"; fi
done < <(modinfo -F firmware "$lab/kernel/drivers/net/wireless/realtek/rtl8xxxu/rtl8xxxu.ko")
for name in mt7601u.bin mt7662.bin mt7662_rom_patch.bin; do zstd -dc "$(readlink -f "/lib/firmware/$name.zst")" > "$base/firmware/$name"; done
cp /lib/firmware/regulatory.db /lib/firmware/regulatory.db.p7s "$base/firmware/"
for name in mt7601u.bin mt7610u.bin mt7662u.bin mt7662u_rom_patch.bin; do
    zstd -dc "/lib/firmware/mediatek/$name.zst" > "$base/firmware/mediatek/$name"
done
cp -a "$repo/licenses/." "$base/notices/"
cp /usr/share/doc/linux-firmware/copyright "$base/notices/linux-firmware-copyright.txt"
cp /usr/share/doc/wireless-regdb/copyright "$base/notices/wireless-regdb-copyright.txt"
cp "$repo/THIRD-PARTY-NOTICES.md" "$base/notices/"
for name in modules.builtin modules.builtin.modinfo; do cp "$lab/kernel/$name" "$base/drivers/lib/modules/$release/$name"; done
find "$base/drivers/lib/modules/$release" -name '*.ko' -printf 'extra/%f\n' > "$base/drivers/lib/modules/$release/modules.order"
depmod -b "$base/drivers" "$release"
python3 - "$base" "$repo" <<'PY'
import json,re,subprocess,sys
from pathlib import Path
base,repo=map(Path,sys.argv[1:])
profiles={}
for driver in ('8188eu','mt76x0u','mt76x2u','mt7601u','rtl8xxxu'):
    module=next((base/'drivers').rglob(driver+'.ko'))
    for line in subprocess.check_output(['modinfo','-F','alias',str(module)],text=True).splitlines():
        m=re.match(r'usb:v([0-9A-F]{4})p([0-9A-F]{4})',line)
        if m:
            vid,pid=(s.lower() for s in m.groups())
            profiles.setdefault((vid,pid),dict(Vendor=vid,Product=pid,Driver=driver,Status='tested' if (vid,pid)==('0bda','8179') else 'experimental'))
for key in (('0bda','b720'),('7392','a611')): profiles.pop(key,None)
(repo/'adapters.json').write_text(json.dumps(list(profiles.values()),indent=2)+'\n')
print('Catalog entries:',len(profiles))
(base/'runtime.json').write_text(json.dumps(dict(version='0.3.4',kernel='6.18.33.2-microsoft-standard-WSL2',architecture='x86_64'))+'\n')
PY
tar -C "$stage" -czf "$repo/obj/runtime-payload.tar.gz" opt
echo "Runtime payload: $repo/obj/runtime-payload.tar.gz"
