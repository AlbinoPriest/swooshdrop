#!/usr/bin/env bash
# Local hardware beta: WinDrop (MIT) + OWL (GPL-3.0) + rtl8188eus.
set -euo pipefail
lab=/opt/airdrop-lab
out=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
destination=${3:-$out/received}
vendor=${5:-0bda}
product=${6:-8179}
serial=${7:-}
[[ "$vendor" =~ ^[0-9a-f]{4}$ && "$product" =~ ^[0-9a-f]{4}$ ]] || { echo 'Invalid adapter identity'; exit 2; }
adapter_matches() {
    local dev=$1
    [[ -f "$dev/idVendor" && -f "$dev/idProduct" ]] || return 1
    [[ "$(cat "$dev/idVendor")" == "$vendor" && "$(cat "$dev/idProduct")" == "$product" ]] || return 1
    [[ -z "$serial" ]] || [[ -f "$dev/serial" && "$(cat "$dev/serial")" == "$serial" ]]
}
mkdir -p "$out/logs" "$destination" /run/airdrop-lab /mnt/wsl/windrop
exec 9>/mnt/wsl/windrop/session.lock
flock -n 9 || { echo 'An AirDrop lab session is already running.' >&2; exit 75; }

case "${1:-8188eu}" in
    rtl8xxxu) driver="$lab/kernel/drivers/net/wireless/realtek/rtl8xxxu/rtl8xxxu.ko"; driver_module=rtl8xxxu; usb_driver=rtl8xxxu ;;
    8188eu) driver="$lab/rtl8188eus/8188eu.ko"; driver_module=8188eu; usb_driver=8188eu ;;
    mt76x0u|mt76x2u|mt7601u) driver_module=$1; usb_driver=$1; driver='' ;;
    *) echo 'Unknown driver selection.' >&2; exit 2 ;;
esac
module_root="$lab/drivers"
if [[ -d "$module_root/lib/modules/$(uname -r)" ]]; then
    driver=$(modinfo -b "$module_root" -k "$(uname -r)" -n "$driver_module")
fi
[[ -f "$driver" ]] || { echo 'No radio driver package matches this WSL kernel. Run SwooshDrop Setup to check compatibility.' >&2; exit 1; }
[[ "$(modinfo -F vermagic "$driver")" == "$(uname -r) "* ]] || { echo 'Radio driver and WSL kernel do not match.' >&2; exit 1; }
radio=''
owl_pid=''
bridge_pid=''
firmware_parameter=/sys/module/firmware_class/parameters/path
firmware_original=$(cat "$firmware_parameter")
restore_firmware() { printf '%s\n' "$firmware_original" > "$firmware_parameter"; }
cleanup() {
    trap - EXIT INT TERM
    restore_firmware
    [[ -z "$bridge_pid" ]] || kill "$bridge_pid" 2>/dev/null || true
    [[ -z "$owl_pid" ]] || kill "$owl_pid" 2>/dev/null || true
    wait 2>/dev/null || true
    if [[ -n "$radio" ]]; then
        ip link set "$radio" down 2>/dev/null || true
        iw dev "$radio" set type managed 2>/dev/null || true
    fi
    rm -f /run/airdrop-lab/session.pid
    rm -f /run/airdrop-lab/session.owner
}
trap cleanup EXIT
trap 'exit 130' INT TERM
echo $$ > /run/airdrop-lab/session.pid
printf '%s\n' "${4:-console}" > /run/airdrop-lab/session.owner

# WSL firmware loads use the shared init namespace, not the Ubuntu root filesystem.
# Supply uncompressed firmware in a shared mount, then restore the search path after probe.
firmware_shared=/mnt/wsl/airdrop-lab-firmware
mkdir -p "$firmware_shared/rtlwifi"
if [[ -d "$lab/firmware" ]]; then
    cp -a "$lab/firmware/." "$firmware_shared/"
else
    cp /lib/firmware/rtlwifi/rtl8188eufw.bin "$firmware_shared/rtlwifi/"
    cp /lib/firmware/regulatory.db /lib/firmware/regulatory.db.p7s "$firmware_shared/"
fi
# Reject ambiguity before touching a radio, including devices without real USB serials.
matches=()
for usb in /sys/bus/usb/devices/*; do adapter_matches "$usb" && matches+=("$usb"); done
[[ ${#matches[@]} -eq 1 ]] || { echo 'Expected exactly one selected USB adapter in WSL.' >&2; exit 1; }
printf '%s' "$firmware_shared" > "$firmware_parameter"
if [[ -d "$module_root/lib/modules/$(uname -r)" ]]; then
    modprobe -d "$module_root" mac80211
    modprobe -d "$module_root" led-class
    modprobe -d "$module_root" "$driver_module"
else
    modprobe mac80211
    modprobe led-class
    if ! grep -q "^$driver_module " /proc/modules; then insmod "$driver"; fi
fi
    # A previous probe may have failed before firmware was available. Retry only this USB device.
    for usb in /sys/bus/usb/devices/*; do
        adapter_matches "$usb" || continue
        for usbif in "$usb":*; do
            [[ -d "$usbif" ]] || continue
            current_driver=''
            if [[ -L "$usbif/driver" ]]; then current_driver=$(readlink -f "$usbif/driver"); fi
            if [[ -n "$current_driver" && "${current_driver##*/}" != "$usb_driver" ]]; then
                printf '%s' "${usbif##*/}" > "$current_driver/unbind"
            fi
            if [[ ! -L "$usbif/driver" ]]; then
                printf '%s' "${usbif##*/}" > "/sys/bus/usb/drivers/$usb_driver/bind"
            fi
        done
    done

for pass in {1..30}; do
    candidates=()
    for net in /sys/class/net/*; do
        usb=$(readlink -f "$net/device/.." 2>/dev/null || true)
        adapter_matches "$usb" || continue
        candidates+=("${net##*/}")
    done
    [[ ${#candidates[@]} -eq 1 ]] && { radio=${candidates[0]}; break; }
    sleep 0.2
done
[[ -n "$radio" ]] || { echo 'The selected USB adapter could not start. Check the adapter and driver in SwooshDrop Setup.' >&2; exit 1; }
restore_firmware

ip link set "$radio" down
iw dev "$radio" set type monitor
if iw dev "$radio" set monitor active > "$out/logs/active-monitor.log" 2>&1; then
    echo 'Active monitor requested; ACK behavior still needs a real transfer test.'
else
    echo 'Active monitor unsupported. Testing plain monitor; speed may be limited.'
    iw dev "$radio" set monitor none
fi
if ! ip link set "$radio" up; then
    echo 'Driver could not start active monitor. Retrying plain monitor.'
    iw dev "$radio" set monitor none
    ip link set "$radio" up
fi
iw dev "$radio" set channel 6 HT20
iw dev "$radio" info | tee "$out/logs/radio-info.txt"
owl_flags=(-f -N)
[[ "${2:-}" == --ui-events ]] || owl_flags+=(-v)
stdbuf -oL -eL "$lab/owl/build/daemon/owl" -i "$radio" -c 6 "${owl_flags[@]}" > "$out/logs/owl.log" 2>&1 &
owl_pid=$!
for pass in {1..50}; do
    kill -0 "$owl_pid" 2>/dev/null || { tail -n 30 "$out/logs/owl.log"; exit 1; }
    [[ -e /sys/class/net/awdl0 ]] && break
    sleep 0.2
done
[[ -e /sys/class/net/awdl0 ]] || { echo 'OWL did not create awdl0.' >&2; exit 1; }
sleep 2
iw dev "$radio" set channel 6 HT20
ip -6 addr show dev awdl0 | tee "$out/logs/awdl-info.txt"

python3 "$lab/windrop/tools/windrop-bridge.py" --interface awdl0 --bind-host ::1 > "$out/logs/bridge.log" 2>&1 &
bridge_pid=$!
for pass in {1..50}; do
    kill -0 "$bridge_pid" 2>/dev/null || { tail -n 30 "$out/logs/bridge.log"; exit 1; }
    ss -lnt6 | grep -q '\[::1\]:7788' && break
    sleep 0.2
done
if [[ "${2:-}" == --ui-events ]]; then
    dotnet "$out/windrop.dll" receive --bridge ::1 --name 'SwooshDrop' --dir "$destination" --ui-events --preview-decoder "$out/render-preview.py" 2>&1 | tee "$out/logs/receiver.log"
else
    echo 'Starting receiver. Transfers require approval. This test session stops after 15 minutes.'
    timeout --foreground 15m dotnet "$lab/windrop/src/WinDrop.Cli/bin/Release/net8.0/windrop.dll" receive --bridge ::1 --name 'SwooshDrop' --dir "$out/received" 2>&1 | tee "$out/logs/receiver.log"
fi
