#!/usr/bin/env bash
set -x

echo "Running cleanup"

TEMP_DIR="/root/tmp"
mkdir -p "$TEMP_DIR"

# Persist
FILES=(
    ".boltz-backend/boltz.conf"
    ".boltz-backend/seed.dat"
    ".boltz-client/boltz.toml"
)

for file in "${FILES[@]}"; do
    cp "/root/$file" "$TEMP_DIR" 2>/dev/null || true
done

# Wipe data dirs
WIPE_DIRS=(
    ".lightning-1"
    ".lightning-2"
    ".lnd-1"
    ".lnd-2"
    ".boltz-backend"
    ".boltz-client"
    ".arkd"
    ".fulmine"
)

for dir in "${WIPE_DIRS[@]}"; do
    rm -rf "/root/$dir"/*
done

# Restore persisted files
for file in "${FILES[@]}"; do
    cp "$TEMP_DIR/$(basename "$file")" "/root/$file" 2>/dev/null || true
done

echo "Cleanup complete"
