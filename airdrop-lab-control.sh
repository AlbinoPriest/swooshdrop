#!/usr/bin/env bash
set -euo pipefail
case "${1:-}" in
    available)
        mkdir -p /run/airdrop-lab /mnt/wsl/windrop
        flock -n /mnt/wsl/windrop/session.lock -c true
        ;;
    attached)
        vendor=${2:-0bda}; product=${3:-8179}; serial=${4:-}
        [[ "$vendor" =~ ^[0-9a-f]{4}$ && "$product" =~ ^[0-9a-f]{4}$ ]] || exit 2
        for dev in /sys/bus/usb/devices/*; do
            [[ -f "$dev/idVendor" && -f "$dev/idProduct" ]] || continue
            if [[ "$(cat "$dev/idVendor")" == "$vendor" && "$(cat "$dev/idProduct")" == "$product" ]] && { [[ -z "$serial" ]] || [[ -f "$dev/serial" && "$(cat "$dev/serial")" == "$serial" ]]; }; then
                exit 0
            fi
        done
        exit 1
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
    *) echo 'Expected available, attached, or stop.' >&2; exit 2 ;;
esac
