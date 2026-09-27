#!/usr/bin/env bash
set -euo pipefail
case "${1:-}" in
    available)
        mkdir -p /run/airdrop-lab
        flock -n /run/airdrop-lab/session.lock -c true
        ;;
    attached)
        for dev in /sys/bus/usb/devices/*; do
            [[ -f "$dev/idVendor" && -f "$dev/idProduct" && -f "$dev/serial" ]] || continue
            if [[ "$(cat "$dev/idVendor")" == 0bda && "$(cat "$dev/idProduct")" == 8179 && "$(cat "$dev/serial")" == 00E04C0001 ]]; then
                exit 0
            fi
        done
        exit 1
        ;;
    stop)
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
