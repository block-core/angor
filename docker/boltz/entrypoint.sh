#!/usr/bin/env bash
set -x

DIRECTORIES=(
    "/root/.lightning-1"
    "/root/.lightning-2"
    "/root/.lnd-1"
    "/root/.lnd-2"
    "/root/.bitcoin"
    "/root/.elements"
    "/root/.boltz-client"
    "/root/.boltz-backend"
)

cleanup() {
    echo 'SIGINT or SIGTERM received, exiting'
    exit 0
}

set_permissions() {
    for dir in "${DIRECTORIES[@]}"; do
        chmod -R 777 "$dir" 2>/dev/null || true
    done
}

main() {
    set_permissions
    trap cleanup SIGINT SIGTERM
    sleep infinity &
    wait
}

main
