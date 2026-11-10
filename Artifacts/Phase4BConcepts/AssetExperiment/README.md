# Phase 4B.1 — isolated asset experiment

## Result

**Three local candidate renders were saved; none is production-ready.** The
new asset-first prompts were a useful test of object-only generation, but the
results are still concept illustrations rather than reusable Unity sprites:

- The conveyor is recognizable, but reads as a long industrial roller chassis
  rather than a short repeatable module.
- The diverter does not read as a routing fork with a clearly pivoting arm.
- The cargo is recognizable as a red container, but acquired wheel-like
  attachments and is not a clean crate sprite.
- The three outputs do not maintain a coherent camera angle or a convincingly
  stylized miniature-factory finish.
- All are 768×768 RGB PNGs on opaque gray backgrounds. **None has alpha
  transparency.**

No image was imported into Unity. The previous four smoke/gameplay experiment
images remain unchanged as failed evidence; their PNGs and generation metadata
were not removed. A second-candidate conveyor request timed out, and its local
stdio retry failed before producing a file. There are exactly three saved
outputs in this experiment, one candidate per asset.

## Visual comparison and usability

| Asset | Candidate | Recognition / silhouette | Camera and style | Isolation and sprite fitness | Assessment |
|---|---|---|---|---|---|
| Conveyor module | [768×768 PNG](../Generated/asset-conveyor-a1-2026-11-10T100709838Z-9d22fb05.png) · [metadata](../Generated/asset-conveyor-a1-2026-11-10T100709838Z-9d22fb05.json) | **3/5.** The dark belt, rails, and rollers are recognizable. It is elongated and resembles a conveyor cart/roller chassis more than a compact tile. | **2/5.** Near-horizontal low three-quarter view; grayscale/metal-heavy rather than the requested painted miniature look. | Complete and separated from the background by contrast, but the background is baked in and there is no alpha. Not seamless or end-to-end repeatable. Rotation/tile reuse would expose perspective and lighting inconsistencies. | **Concept artwork only.** Rebuild a short modular belt/rail tile by hand. |
| Diverter module | [768×768 PNG](../Generated/asset-diverter-a1-2026-11-10T100857669Z-0261fe30.png) · [metadata](../Generated/asset-diverter-a1-2026-11-10T100857669Z-0261fe30.json) | **1/5.** A pedestal/machinery assembly is visible, but not a clear two-way conveyor fork or selector arm. | **1/5.** Tall vertical composition differs from the conveyor and crate; the arm/fork shape is ambiguous. | Entire object is on the background, no alpha. The route arm and base are fused into one image, so it cannot support two Unity-controlled routing states without manual reconstruction. | **Unusable as a switch sprite.** Create the base and pivoting arm as separate authored layers/meshes. |
| Cargo package | [768×768 PNG](../Generated/asset-cargo-a1-2026-11-10T101047695Z-20f23c2f.png) · [metadata](../Generated/asset-cargo-a1-2026-11-10T101047695Z-20f23c2f.json) | **3/5.** Reads as a red container, but its wheels/feet make it look like a small trolley rather than cargo. The blank face is large enough for a programmatic mark after redesign. | **2/5.** Near-frontal, comparatively photoreal product render; not consistent with the other views or the desired casual-game finish. | Fully in frame with a light-gray background baked in; RGB only. A square crop is possible, but the shadow/background would remain. | **Concept artwork only.** A cleaned, simpler crate could be a static sprite; add the mark/color variants in Unity. |

Ratings are a qualitative review of these specific renders, not benchmark
scores for all SDXL output.

## Exact generation record

All three candidates used local Draw Things 26.1020.0 with
`sd_xl_base_1.0_q8p.ckpt`, on the user's Apple M1 Pro with 16 GB unified
memory. Each was generated sequentially at 768×768, 30 steps, CFG 6.5.
Seeds were 4102101, 4102102, and 4102103 respectively. The exact positive and
negative prompts, timestamp, output name, and parameters are preserved in each
sidecar JSON and in [`asset-experiment-manifest.json`](asset-experiment-manifest.json).

The second-candidate conveyor attempt used a different, more stylized prompt,
768×768, 34 steps, CFG 6.5, seed 4102201. The MCP client timed out and a local
stdio retry returned `fetch failed`; no PNG or metadata file was written. No
further attempt was made, and it does not count as a candidate render.

The shared intent was a consistent elevated three-quarter tabletop product
view, warm studio key light, restrained painted steel and brushed-metal
details, clean silhouette, pale warm-gray background, and no text/logos.
Identical seeds/settings would not by themselves guarantee a consistent style;
these images confirm that the model did not reliably hold that camera/material
direction across different objects.

## Provenance, transparency, licensing, and cost

- **Generation:** local Draw Things API at `127.0.0.1:7860`; loaded model
  confirmed through the local status tool. No cloud service, upload, paid
  generation, or external image reference was used.
- **Output verification:** all three files are valid 768×768 PNGs. PNG IHDR
  color type is RGB (2); no alpha channel or transparency chunk exists. Their
  exact SHA-256 values are in the manifest.
- **Model license:** the pinned SDXL 1.0 Base checkpoint is under CreativeML
  Open RAIL++-M, subject to the license's use restrictions and obligations.
  The model's license does not certify third-party rights in each output.
  Output-specific rights/IP review is not complete; these images are not
  approved for commercial production.
- **Tool/cost:** Draw Things is already installed and the approved local model
  is already downloaded/imported. This run used no paid/cloud service and
  incurred no generation charge. No new model or tool was downloaded.

## Unity assembly proposal — no integration in this phase

Keep the deterministic domain and visual presentation separate:

1. **Domain authority:** `PulseFactory.Core.SimulationEngine` and
   `GameSession` remain authoritative for commands, beats, switch state,
   routes, package positions, deliveries, and terminal results. Artwork never
   decides connectivity, collisions, or outcomes.
2. **Board layout and conveyor rendering:** `BoardRenderer`/the presentation
   layer maps level node coordinates and authored edges to positions and
   segments. Use a short authored center-belt tile with separate rail/end-cap
   pieces, or a Unity tiled image, to render each edge between the existing
   graph endpoints. Keep branch selection and highlights as separate
   runtime-controlled overlays. The crossing remains two independent edges
   with visual layer ordering; do not bake a crossing/junction into a board
   background.
3. **Diverter state:** use a static chassis/base plus a distinct arm/pointer
   pivoted at its mechanical center. `DiverterView` reads the selected route
   and switch state from the simulation-derived view state, then rotates the
   arm and changes a non-authoritative lamp/highlight. A pulse animation is
   presentation only; it does not mutate switch state.
4. **Package appearance and movement:** use a crate body with a blank face;
   Unity draws the package-kind color and plus/cross mark as a separate
   programmatic overlay. `PackageView` is positioned from simulation state
   and animates only along the `Moved` events/actual previous and next node
   positions. Delivery/failure effects must follow domain events.
5. **HUD, localization, and interaction:** existing Unity UI continues to
   own text, localization/RTL, button behavior, and layout. Generated artwork
   contains no labels, symbols, button states, or gameplay-dependent text.
6. **Animation and reset:** animate transforms/overlays from state and events.
   Undo and Restart restore presentation from the restored/current simulation
   state and cancel any pending visual transitions.

The production-oriented kit should consist of independently editable,
properly cropped assets: repeatable conveyor center strips plus rail/end caps;
a diverter base and separate pivoting arm; and a crate shell with a neutral
face. Require human-approved silhouettes, a consistent camera/material recipe,
real alpha (or a validated local matte-removal step), mobile-size review, and
Unity import checks before integration. This experiment supplies none of
those approvals.

## Recommendation

**Do not continue SDXL Base as a direct text-to-production-sprite workflow.**
For this task it produces recognizable object ideas but does not reliably
produce the requested mechanical silhouettes, matching views, or isolated
alpha-ready components. This is not a claim that SDXL cannot create attractive
images; these three results do not meet the project's reuse and animation
requirements.

Use **hand-authored low-poly tabletop components in Blender**, rendered as
separate, consistent orthographic/elevated-3D sprites with a transparent
background, then assemble and animate them in Unity. Blender's current official
requirements list Apple Silicon/macOS 13+ and 8 GB RAM as the macOS minimum,
32 GB as recommended; this Mac's M1 Pro/16 GB exceeds minimum but is below the
recommended RAM, so actual scene/render performance remains to be tested.
Blender is free to use; Blender-authored artwork is the artist's property per
the Blender Foundation's licensing page. Tool cost is $0; production labor is
not. No Blender installation, new model, or further asset production was
started.

Sources: [Blender license and artwork terms](https://www.blender.org/about/license/),
[Blender hardware requirements](https://www.blender.org/download/requirements/),
[SDXL checkpoint license](https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0/blob/462165984030d82259a11f4367a4eed129e94a7b/LICENSE.md).
