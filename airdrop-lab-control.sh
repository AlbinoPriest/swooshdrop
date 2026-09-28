#!/usr/bin/env bash
set -euo pipefail
adapter_count() {
    local vendor=$1 product=$2 serial=$3 dev matches=0
    for dev in /sys/bus/usb/devices/*; do
        [[ -f "$dev/idVendor" && -f "$dev/idProduct" ]] || continue
        if [[ "$(cat "$dev/idVendor")" == "$vendor" && "$(cat "$dev/idProduct")" == "$product" ]] && { [[ -z "$serial" ]] || [[ -f "$dev/serial" && "$(cat "$dev/serial")" == "$serial" ]]; }; then
            matches=$((matches + 1))
        fi
    done
    printf '%s\n' "$matches"
}
case "${1:-}" in
    available)
        mkdir -p /run/airdrop-lab /mnt/wsl/windrop
        flock -n /mnt/wsl/windrop/session.lock -c true
        ;;
    count|attached)
        vendor=${2:-0bda}; product=${3:-8179}; serial=${4:-}
        [[ "$vendor" =~ ^[0-9a-f]{4}$ && "$product" =~ ^[0-9a-f]{4}$ ]] || exit 2
        if [[ "$1" == count ]]; then adapter_count "$vendor" "$product" "$serial"; exit 0; fi
        # usbipd may report Attached before WSL has finished enumerating the USB device.
        for pass in {1..40}; do
            matches=$(adapter_count "$vendor" "$product" "$serial")
            [[ "$matches" -eq 1 ]] && exit 0
            [[ "$matches" -gt 1 ]] && { echo 'More than one selected USB adapter appeared in WSL.' >&2; exit 1; }
            sleep 0.5
        done
        echo 'Selected USB adapter did not appear in WSL after 20 seconds.' >&2
        exit 1
        ;;
    release)
        vendor=${2:-}; product=${3:-}; serial=${4:-}
        [[ "$vendor" =~ ^[0-9a-f]{4}$ && "$product" =~ ^[0-9a-f]{4}$ ]] || exit 2
        mkdir -p /mnt/wsl/windrop
        exec 8>/mnt/wsl/windrop/session.lock
        flock -n 8 || { echo 'The receiver is still using the adapter.' >&2; exit 75; }
        if [[ -z "$serial" && $(adapter_count "$vendor" "$product" "$serial") -gt 1 ]]; then
            echo 'Multiple matching USB adapters lack a unique serial; refusing cleanup.' >&2
            exit 1
        fi
        for dev in /sys/bus/usb/devices/*; do
            [[ -f "$dev/idVendor" && -f "$dev/idProduct" ]] || continue
            [[ "$(cat "$dev/idVendor")" == "$vendor" && "$(cat "$dev/idProduct")" == "$product" ]] || continue
            [[ -z "$serial" ]] || [[ -f "$dev/serial" && "$(cat "$dev/serial")" == "$serial" ]] || continue
            for status in /sys/devices/platform/vhci_hcd.*/status; do
                [[ -f "$status" ]] || continue
                while read -r port; do
                    [[ "$port" =~ ^[0-9]+$ ]] || continue
                    printf '%s\n' "$((10#$port))" > "${status%/status}/detach"
                done < <(awk -v id="${dev##*/}" '$7 == id {print $2}' "$status")
            done
        done
        ;;
    stop)
        if [[ -n "${2:-}" ]] && [[ ! -f /run/airdrop-lab/session.owner || "$(cat /run/airdrop-lab/session.owner)" != "$2" ]]; then
            # Startup may fail before recording an owner. A free session lock
            # proves no receiver is running, so the caller may detach its USB.
            if [[ ! -f /run/airdrop-lab/session.owner ]] && flock -n /mnt/wsl/windrop/session.lock -c true; then
                exit 0
            fi
            echo 'This receiver session belongs to a different instance.' >&2
            exit 75
        fi
        if [[ -f /run/airdrop-lab/session.pid ]]; then
            labpid=$(cat /run/airdrop-lab/session.pid)
            [[ "$labpid" =~ ^[0-9]+$ ]] || exit 1
            if [[ -r "/proc/$labpid/cmdline" ]] && tr '\0' ' ' < "/proc/$labpid/cmdline" | grep -q 'airdrop-lab.sh' && [[ "$(ps -o pgid= -p "$labpid" | tr -d ' ')" == "$labpid" ]]; then
                kill -TERM -- "-$labpid"
            else
                echo 'Stale lab PID; not terminating an unrelated process.'
            fi
        fi
        ;;
    *) echo 'Expected available, count, attached, release, or stop.' >&2; exit 2 ;;
esac
