# Pulse Factory Phase 4C.1 — Mechanical Readability

This is an additive refinement package. It reads the Phase 4C Blender scene,
creates new renders/exports under this directory, and leaves the Phase 4C
source scene and outputs unchanged. The Unity project, Level 2 JSON, simulation,
and progression were not modified.

## Rebuild

From the repository root:

```sh
bash Artifacts/Phase4C1MechanicalReadability/build.sh
```

The script uses the configured per-user Blender 5.2.4 installation and macOS
Swift/AVFoundation. It renders three 1080×1920 camera comparisons, a
18-frame/12-fps synchronized switch transition, generates the recommended
360×640 preview, exports FBX and GLB model candidates, and runs an independent
Blender validation pass.

## Camera comparison

- [A — Phase 4C baseline composition](./rendered/comparison_A_baseline.png)
- [B — Near-top-down orthographic](./rendered/comparison_B_near_top_down.png)
- [C — Refined elevated three-quarter](./rendered/comparison_C_refined_three_quarter.png)
- [Recommended 1080×1920 mobile preview](./rendered/mobile_preview_1080x1920.png)
- [Recommended 360×640 mobile preview](./rendered/mobile_preview_360x640.png)

**Recommendation: B, near-top-down orthographic.** It keeps the complete board
inside the portrait frame with practical margins for a HUD and controls, makes
all six connections and the active/inactive route strips easiest to scan, and
still shows the raised bridge through shadows and support geometry. C has
stronger depth, but its diagonal view makes the crossed routes harder to scan
and leaves less room for HUD composition at phone width. A preserves the prior
dramatic camera placement and is useful as a baseline, but the 9:16 framing
leaves less edge margin.

All three comparison renders use the same refined board scene, materials,
post-PulseA state, packages, and six graph connections; only the camera differs.
A reuses the archived Phase 4C camera placement, so it is a camera baseline,
not a second copy of the old unrefined image. The original Phase 4C artifacts
remain preserved separately.

## Visual before/after

| Area | Phase 4C baseline | Phase 4C.1 |
|---|---|---|
| Center crossings | Dense rails and diverter arms compete visually. | Lower rails and a shortened arm root expose the switch hubs and package markers; the raised J1→B bridge remains above J2→R. |
| Route state | Selected-path accents are static. | Every edge has an inactive lane mark; graph-derived active marks crossfade when Pulse A is activated. |
| Package location | Crates are staged 0.90 m before their recorded node. | Crates are centered on their logical J1/J2 nodes and scaled for the hub. Red diamond / blue circle marks identify packages and matching destinations independently of text. |
| Portrait layout | The old view is tightly framed in a 9:16 crop. | Camera B fits the whole board with HUD/control margins at 1080×1920 and remains legible at 360×640. |

The graph remains the authority. The builder reads the Level 2 JSON and the
catalog solution (`PulseA`), then derives the before/after routing states:
before pulse J1→B and J2→R; after pulse J1→R and J2→B. All six original
connections remain present: S1J1, S2J2, J1R, J1B, J2R, and J2B. The package
meshes are placed exactly at J1/J2 in the post-PulseA preview state. The board
geometry only represents those logical edges; it does not add graph nodes or
connections.

## Synchronized switch proof

[View the 1.5-second synchronized Pulse A transition](./rendered/synchronized_pulse_transition.mp4).
Both routing arms animate from the initial routes to the graph-derived
post-pulse routes. Matching active-route strips change state at the same time,
and restrained paired halos mark the activation. Animation is a Blender
transform proof, not gameplay logic.

## Models and validation

- [Refined Blender source scene](./generated/models/PulseFactory_Phase4C1.blend)
- [Asset manifest and checksums](./asset-manifest.json)
- [Refinement/build report](./validation/refinement-report.json)
- [Validation report](./validation/validation-report.json)
- [Builder source](./src/refine_board.py)
- [Build command](./build.sh)

Blender 5.2.4 LTS on macOS 27.0.1 / Apple Silicon was used. The final validation
passes eight checks covering the unchanged level hash, graph-derived routes,
all six module paths and route indicators, package-node alignment, synchronous
switch transitions, 9:16/mobile dimensions, animation frames, and Blender
round-trip imports of all eight FBX/GLB exports. The raised J1B bridge was
checked above the crossing J2R deck. Validation does not establish Unity
import, runtime performance, or gameplay integration.

The final procedural build took 249.1 seconds, including the three camera
renders (55.8 s, 45.4 s, and 32.2 s) and 18-frame animation rendering (108.7 s).
Blender's reported peak resident memory was about 1.42 GB. These timings are
from this Mac and are not mobile-runtime performance measurements.

The technical approach and assets are **not approved for Unity integration by
this build alone**. Integration remains gated on review and explicit approval
of the rendered result.
