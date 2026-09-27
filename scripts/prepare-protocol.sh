#!/usr/bin/env bash
# Build the pinned OWL and WinDrop sources. Driver provisioning is separate.
set -euo pipefail
repo=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
lab=/opt/airdrop-lab
mkdir -p "$lab"

checkout() {
    local destination=$1 url=$2 revision=$3
    if [[ ! -d "$destination" ]]; then
        git clone "$url" "$destination"
        git -C "$destination" checkout --detach "$revision"
    fi
    [[ "$(git -C "$destination" rev-parse HEAD)" == "$revision" ]] || {
        echo "Unexpected revision in $destination. Not changing existing source." >&2; exit 1;
    }
}

checkout "$lab/owl" https://github.com/seemoo-lab/owl.git da255a70f221784c836d943dd3f243bc798f223b
git -C "$lab/owl" submodule update --init --recursive
cmake -S "$lab/owl" -B "$lab/owl/build" -DCMAKE_BUILD_TYPE=Release
cmake --build "$lab/owl/build" -j 4

checkout "$lab/windrop" https://github.com/UvejsGj/WinDrop.git 5a5dc52ce6eda298789622cb92d548d64d5673cc
if ! git -C "$lab/windrop" apply --reverse --check "$repo/patches/prototype-changes.patch" 2>/dev/null; then
    git -C "$lab/windrop" apply --check "$repo/patches/prototype-changes.patch"
    git -C "$lab/windrop" apply "$repo/patches/prototype-changes.patch"
fi
dotnet build "$lab/windrop/src/WinDrop.Cli/WinDrop.Cli.csproj" -c Release -o "$lab/receiver-product"
mkdir -p "$lab/receiver-refresh"
cp "$lab/receiver-product/"* "$lab/receiver-refresh/"

echo 'Protocol build complete. The radio driver and USB passthrough must also be provisioned.'
