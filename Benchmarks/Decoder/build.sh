#!/usr/bin/env bash
# Builds the PC round-trip decoder in all four delta-option variants.
# The decoder sources are taken unmodified from /Decompression; only the mode
# #define in a temporary copy of the header is switched.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
src="$here/../../Decompression"
out="${1:-$here/bin}"
mkdir -p "$out"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

for mode in FIXED_WINDOW_ONLY ADAPTIVE_FLOATING_WINDOW_ONLY FIXED_WINDOW_FIRST ADAPTIVE_FLOATING_WINDOW_FIRST; do
  dir="$tmp/$mode"
  mkdir -p "$dir"
  cp "$src/Decompression.cpp" "$dir/"
  # Disable all mode defines, enable the requested one, and allow up to 4 channels
  sed -E -e 's@^(#define DECOMPRESSION_(FIXED_WINDOW_ONLY|ADAPTIVE_FLOATING_WINDOW_ONLY|FIXED_WINDOW_FIRST|ADAPTIVE_FLOATING_WINDOW_FIRST))@//\1@' \
         -e "s@^//#define DECOMPRESSION_${mode}\$@#define DECOMPRESSION_${mode}@" \
         -e "s@^//\#define DECOMPRESSION_${mode}([[:space:]])@#define DECOMPRESSION_${mode}\1@" \
         -e 's@^(#define DECOMPRESSION_MAX_NUMBER_OF_CHANNELS).*@\1 4@' \
         "$src/Decompression.h" > "$dir/Decompression.h"
  grep -q "^#define DECOMPRESSION_${mode}" "$dir/Decompression.h" || { echo "failed to select $mode" >&2; exit 1; }
  name="$(echo "$mode" | tr 'A-Z_' 'a-z-')"
  g++ -O2 -std=c++17 -I"$dir" "$here/roundtrip.cpp" "$dir/Decompression.cpp" -o "$out/roundtrip-$name"
done
echo "Decoders built in $out"
