#!/bin/bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
BLENDER="${BLENDER_BIN:-$HOME/Applications/Blender.app/Contents/MacOS/Blender}"
PHASE4C="$ROOT/../Phase4CProceduralArt"

if [[ ! -x "$BLENDER" ]]; then
  echo "Blender executable not found: $BLENDER" >&2
  exit 1
fi

rm -f "$ROOT/validation/refinement-report.json" "$ROOT/validation/validation-report.json"
"$BLENDER" --background --python "$ROOT/src/refine_board.py"
if [[ ! -s "$ROOT/validation/refinement-report.json" ]]; then
  echo "Blender did not produce the refinement report; inspect the Python traceback above." >&2
  exit 1
fi
swift "$PHASE4C/src/encode_animation.swift" \
  "$ROOT/rendered/animation_frames" \
  "$ROOT/rendered/synchronized_pulse_transition.mp4" 12
sips -z 640 360 \
  "$ROOT/rendered/mobile_preview_1080x1920.png" \
  --out "$ROOT/rendered/mobile_preview_360x640.png" >/dev/null
"$BLENDER" --background "$ROOT/generated/models/PulseFactory_Phase4C1.blend" \
  --python "$ROOT/validation/validate_refinement.py"
