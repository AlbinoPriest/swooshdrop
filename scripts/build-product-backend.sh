#!/usr/bin/env bash
set -euo pipefail
repo=$(cd -- "$(dirname "$0")/.." && pwd)
lab=/opt/airdrop-lab
mkdir -p "$lab/work"
stage=$(mktemp -d "$lab/work/product-backend.XXXXXX")
# Build from the pinned upstream tree plus the shipped patch. Keep the small
# source stage for build diagnostics rather than relying on local source edits.
git -C "$lab/windrop" archive 5a5dc52ce6eda298789622cb92d548d64d5673cc | tar -x -C "$stage"
while IFS= read -r patch || [[ -n "$patch" ]]; do
    patch=${patch%$'\r'}
    [[ -n "$patch" ]] || continue
    [[ "$patch" != */* && "$patch" == *.patch ]] || { echo "Invalid patch name: $patch" >&2; exit 1; }
    git -C "$stage" apply "$repo/patches/$patch"
done < "$repo/patches/series"
dotnet build "$stage/src/WinDrop.Cli" -c Release -o "$lab/receiver-product"
if [[ "${1:-}" == --test ]]; then
    dotnet test "$stage/tests/WinDrop.Protocol.Tests" -c Release
fi
echo "Product backend: $lab/receiver-product"
