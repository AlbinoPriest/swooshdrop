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
        if [[ -n "${2:-}" ]] && [[ ! -f /run/airdrop-lab/session.owner || "$(cat /run/airdrop-lab/session.owner)" != "$2" ]]; then
            # Startup may fail before recording an owner. A free session lock
            # proves no receiver is running, so the caller may detach its USB.
            if [[ ! -f /run/airdrop-lab/session.owner ]] && flock -n /run/airdrop-lab/session.lock -c true; then
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
