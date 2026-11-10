#!/bin/bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
BLENDER="${BLENDER_BIN:-$HOME/Applications/Blender.app/Contents/MacOS/Blender}"

if [[ ! -x "$BLENDER" ]]; then
  echo "Blender executable not found: $BLENDER" >&2
  echo "Set BLENDER_BIN to the official Blender executable." >&2
  exit 1
fi

rm -f "$ROOT/validation/build-report.json"
"$BLENDER" --background --python "$ROOT/src/build_scene.py" -- "$ROOT"
if [[ ! -s "$ROOT/validation/build-report.json" ]]; then
  echo "Blender did not produce a build report; inspect the Python traceback above." >&2
  exit 1
fi
swift "$ROOT/src/encode_animation.swift" \
  "$ROOT/rendered/animation_frames" "$ROOT/rendered/diverter_transition.mp4" 12
"$BLENDER" --background "$ROOT/generated/models/PulseFactory_Phase4C.blend" \
  --python "$ROOT/validation/validate_build.py"
