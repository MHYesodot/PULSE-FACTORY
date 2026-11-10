# Pulse Factory Phase 4C procedural art proof

Blender Python creates the proof geometry and the Level 2 — Shared Control
render from the actual Unity level JSON and level catalog. Blender output stays
under this workspace, outside Unity `Assets/`. No SDXL image is used as
production geometry, and no Unity gameplay/UI/simulation files are modified.

## Workspace

- `src/` — reproducible `bpy` build script.
- `materials/palette.json` — shared physical-scale material palette.
- `generated/models/` — native `.blend` source scene and models.
- `rendered/` — portrait board, transparent component previews, diverter state
  stills, and animation proof.
- `exports/` — glTF 2.0 binary model exports for compatibility inspection.
- `validation/` — validation script and machine-readable build/test reports.
- [Level 2 board render](./rendered/level2_shared_control.png), [diverter
  transition](./rendered/diverter_transition.mp4), and
  [asset manifest](./asset-manifest.json).

Generated files are rebuildable and should not be hand-edited. Change the
Python source or palette, then rebuild.

## Blender install and build

The project was prepared for Blender **5.2.4 Apple Silicon** using the official
Blender Foundation distribution. The per-user application path is
`~/Applications/Blender.app`. Its SHA-256 and code signature should be checked
against the build report/environment record before use.

From the repository root:

```sh
Artifacts/Phase4CProceduralArt/build.sh
```

Or set `BLENDER_BIN` to a different compatible Blender executable. The build
script reads:

- `Assets/Resources/Levels/shared-control.json` for node positions,
  connectivity, diverter groups/inversion, and beat-one spawns.
- `Assets/Resources/Levels/catalog.json` for the first actual solution
  command, used to derive the pictured post-command switch state.

The script uses Blender's bundled Python and `bpy`; it installs no Python
packages. macOS Swift and AVFoundation encode the animation proof. The build
creates a `.blend`, four glTF 2.0 models, four FBX candidates, a 1200×1800 board
render, 768×768 isolated RGBA previews, two 640×640 diverter-state stills, and
a 25-frame H.264 proof sampled from the 48-frame Blender timeline.

The independent validator checks level references, exact repeated module
counts, pivots, non-empty mesh geometry, package visibility and materials,
export checksums, and actual transparent/opaque alpha pixels. It imports both
the GLB and FBX exports back into Blender and checks the diverter hierarchy;
this is not a Unity import or performance test.

## Mechanical conventions

- One Blender unit equals one meter.
- XY is the board plane; Z is mechanical height.
- The fixed conveyor module is 0.75 m long. Level edges are an integer number
  of these modules; rollers, rails, and end geometry are never stretched.
- The diverter arm's origin is the defined rotation pivot; the arm is separate
  from the stationary base. The animation shows both states, and both
  group-A diverters respond simultaneously, including J2's logical inversion.
- Red/blue crate material variants share a blank cream identity plate. Unity
  may overlay the actual package-kind symbols independently.
- The Level 2 preview shows packages logically at J1/J2 but stages their
  visible crate meshes 0.90 m onto each input-side approach to keep the crates
  from being occluded by the routing arms. The preview-only colored swatch
  represents package identity; the reusable crate model's plate remains
  neutral.
- Level 2's crossing is grade-separated in Blender geometry: J1→B uses the
  raised bridge and J2→R passes below it. These are still the original six
  graph edges; the visual overpass adds no simulation node or connection.

## Unity assembly blueprint

- The simulation remains authoritative for graph connectivity, switch state,
  package kind, and package node. Unity should assemble the six level edges
  from fixed 0.75 m conveyor prefabs using the existing level data.
- The raised J1→B bridge is only a visual height offset. It must not create a
  simulation connection or node.
- Bind each diverter's `ArmPivot` transform to the current simulation switch
  state, including the level's group inversion. Animation changes transforms
  only; it must not mutate simulation state.
- Instantiate the red/blue crate material variant from the package kind and
  draw identity symbols on the neutral plate in Unity.
- Use an orthographic camera for the board. Keep HUD, text, controls,
  localization, and interaction in Unity UI rather than baked into artwork.

## 3D versus sprite integration

| Consideration | Modular 3D geometry | Pre-rendered transparent sprites |
|---|---|---|
| Visual result | The board render shows coherent lighting, contact shadows, depth, and a readable grade-separated crossing. | The four isolated 768×768 previews have genuine alpha and fixed lighting; they are simpler to display but cannot change camera/light response. |
| State and animation | The routing arm is a separate pivot child; Unity can animate it from the simulation state. | The arm must be exported as a separate layer or rendered into animation frames; rotation and arbitrary orientation are less flexible. |
| Modularity | The 20 fixed-size conveyor modules are assembled without stretching; the four GLBs total 536 KiB and four FBXs total 312 KiB. | Repeated placement is straightforward, but edge caps, turns, overpasses, and arm layers need carefully authored sprite variants. The four previews total 960 KiB. |
| Runtime/build | Geometry gives Unity depth and material control. Actual mobile frame time and build size were not measured. | Predictable 2D rendering and no model-import workflow; texture memory and atlas behavior were not benchmarked. |
| Import path | FBX is the Unity-native candidate, with GLB retained as an interchange format. Both formats round-trip in Blender; Unity has not imported them in this phase. | PNG sprites use Unity's built-in image import path, but would need additional direction/state layers for this board. |

**Recommendation:** use lightweight modular geometry with an orthographic
Unity camera for the board and state-driven diverter pivots. It best preserves
the verified topology, mechanical depth, and transform-based animation without
a runtime glTF dependency. Use Blender's FBX exports as the first Unity-native
import candidate; verify materials, scale, hierarchy, and runtime performance
in Unity before integration approval. Use sprites for static decorative
elements only if they prove cheaper or clearer in the actual game.

## Provenance and license

The models and materials are generated by `src/build_scene.py` and
`materials/palette.json`; there are no third-party meshes or textures. The
repository has no separate asset license, so distribution terms remain subject
to the project's eventual license. `asset-manifest.json` records source,
dimensions, pivot conventions, exports, and SHA-256 checksums.

## Validation and acceptance

- **Environment:** Blender 5.2.4 LTS, macOS 27.0.1, Apple Silicon (`arm64`),
  16 GiB unified memory. The official per-user Blender installation is
  `~/Applications/Blender.app`.
- **Rebuild:** completed successfully with the checked-in builder. Latest
  Blender build step took 88.765 s (excluding the separate MP4 encode and
  validation); the board render took 16.835 s, the sampled diverter frames
  56.586 s, and Blender's peak resident memory was about 2.27 GB on this Mac.
- **Structural validation:** 13 checks passed. All four PNG previews have real
  alpha values spanning 0–1. Both GLB and FBX sets round-trip imported in
  Blender; the FBX conveyor remains 0.75 m and the diverter arm retains its
  pivot parent. The two rendered switch states differ across 10,995 pixels.
- **Animation file:** H.264 MP4, 25 frames at 12 fps, 2.083 s duration.
- **Visual review:** the 1200×1800 board was also reviewed at 360×540. Sources,
  six routes, both diverters, destinations, and red/blue packages remain
  distinguishable at that scale. The art-proof acceptance gate passes for
  this limited procedural study, not as final production art.
- **Unity/gameplay:** no Unity import, scene integration, gameplay execution,
  mobile performance test, or gameplay test suite was run. Do not treat this
  render as a validated playable board.

Remaining art limitations include simplified mechanical detail and some
visual density where the four diverter branches meet. The preview has no HUD,
interactive feedback, gameplay-driven package movement, or Unity lighting.

Do not integrate these models into Unity before explicit approval.
