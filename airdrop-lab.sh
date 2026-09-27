#!/usr/bin/env bash
# Local compatibility prototype: WinDrop (MIT) + OWL (GPL-3.0) + rtl8xxxu.
set -euo pipefail
lab=/opt/airdrop-lab
out=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
mkdir -p "$out/logs" "$out/received" /run/airdrop-lab
exec 9>/run/airdrop-lab/session.lock
flock -n 9 || { echo 'An AirDrop lab session is already running.' >&2; exit 75; }

case "${1:-8188eu}" in
    rtl8xxxu) driver="$lab/kernel/drivers/net/wireless/realtek/rtl8xxxu/rtl8xxxu.ko"; driver_module=rtl8xxxu; usb_driver=rtl8xxxu ;;
    8188eu) driver="$lab/rtl8188eus/8188eu.ko"; driver_module=8188eu; usb_driver=8188eu ;;
    *) echo 'Unknown driver selection.' >&2; exit 2 ;;
esac
[[ -f "$driver" ]] || { echo 'The matching radio driver has not finished building.' >&2; exit 1; }
[[ "$(modinfo -F vermagic "$driver")" == "$(uname -r) "* ]] || {
    echo 'WSL kernel changed. Rebuild the driver before testing.' >&2; exit 1;
}
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
}
trap cleanup EXIT
trap 'exit 130' INT TERM
echo $$ > /run/airdrop-lab/session.pid

# WSL firmware loads use the shared init namespace, not the Ubuntu root filesystem.
# Supply uncompressed firmware in a shared mount, then restore the search path after probe.
firmware_shared=/mnt/wsl/airdrop-lab-firmware
mkdir -p "$firmware_shared/rtlwifi"
cp /lib/firmware/rtlwifi/rtl8188eufw.bin "$firmware_shared/rtlwifi/"
cp /lib/firmware/regulatory.db /lib/firmware/regulatory.db.p7s "$firmware_shared/"
printf '%s' "$firmware_shared" > "$firmware_parameter"
modprobe mac80211
modprobe led-class
if ! grep -q "^$driver_module " /proc/modules; then
    insmod "$driver"
fi
    # A previous probe may have failed before firmware was available. Retry only this USB device.
    for usb in /sys/bus/usb/devices/*; do
        [[ -f "$usb/idVendor" && -f "$usb/idProduct" && -f "$usb/serial" ]] || continue
        [[ "$(cat "$usb/idVendor")" == 0bda && "$(cat "$usb/idProduct")" == 8179 && "$(cat "$usb/serial")" == 00E04C0001 ]] || continue
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
        [[ -f "$usb/idVendor" && -f "$usb/idProduct" && -f "$usb/serial" ]] || continue
        [[ "$(cat "$usb/idVendor")" == '0bda' && "$(cat "$usb/idProduct")" == '8179' && "$(cat "$usb/serial")" == '00E04C0001' ]] || continue
        candidates+=("${net##*/}")
    done
    [[ ${#candidates[@]} -eq 1 ]] && { radio=${candidates[0]}; break; }
    sleep 0.2
done
[[ -n "$radio" ]] || { echo 'The expected TP-Link USB adapter is not present.' >&2; exit 1; }
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
stdbuf -oL -eL "$lab/owl/build/daemon/owl" -i "$radio" -c 6 -v -f -N > "$out/logs/owl.log" 2>&1 &
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
    dotnet "$lab/receiver-refresh/windrop.dll" receive --bridge ::1 --name 'WinDrop PC' --dir "$out/received" --ui-events 2>&1 | tee "$out/logs/receiver.log"
else
    echo 'Starting receiver. Transfers require approval. This test session stops after 15 minutes.'
    timeout --foreground 15m dotnet "$lab/windrop/src/WinDrop.Cli/bin/Release/net8.0/windrop.dll" receive --bridge ::1 --name 'WinDrop PC' --dir "$out/received" 2>&1 | tee "$out/logs/receiver.log"
fi
