#!/usr/bin/env bash
set -euo pipefail
repo=$(cd -- "$(dirname "$0")/.." && pwd)
lab=/opt/airdrop-lab
stage=$(mktemp -d "$lab/work/product-backend.XXXXXX")
# Build from the pinned upstream tree plus the shipped patch. Keep the small
# source stage for build diagnostics rather than relying on local source edits.
git -C "$lab/windrop" archive 5a5dc52ce6eda298789622cb92d548d64d5673cc | tar -x -C "$stage"
git -C "$stage" apply "$repo/patches/prototype-changes.patch"
dotnet build "$stage/src/WinDrop.Cli" -c Release -o "$lab/receiver-product"
if [[ "${1:-}" == --test ]]; then
    dotnet test "$stage/tests/WinDrop.Protocol.Tests" -c Release
fi
echo "Product backend: $lab/receiver-product"
