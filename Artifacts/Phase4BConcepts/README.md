# Pulse Factory Phase 4B — concept review gate

## Status: local generation verified; concepts need revision

The approved SDXL 1.0 Base checkpoint is downloaded, imported, and converted to
8-bit in Draw Things 26.1020.0. The project's local MCP adapter reports the
loaded model as `sd_xl_base_1.0_q8p.ckpt`; its API remains bound to
`127.0.0.1:7860`. A 512×512 smoke image and one 576×1024 portrait for each of
the three written art directions were generated locally. Each output has a
provenance JSON file in [`Generated/`](Generated/).

The generation pipeline works, but the three portraits are **not successful
gameplay concepts**: they do not clearly depict the specified six-node board,
route state, marked packages, or reserved UI, and the three styles are not
distinct enough to choose between. They are exploratory outputs only; no
direction or image is approved for production or Unity integration. The smoke
image also does not clearly depict its requested red-plus crate. See the
[integration assessment](integration-options.md) and
[model candidate review](model-candidate-review.md).

All generation used the local Draw Things API; no project screenshots or other
references were sent to it or to an external service. No subscription, cloud
compute, or generation charge was used. The reference allowlist remains empty.
The first portrait request exceeded the MCP SDK client's default 60-second
timeout and produced no file; retrying with an explicit 10-minute client
timeout succeeded. The MCP tools were exercised through a local stdio client;
their availability in this Copilot chat is not independently confirmed.
Animation generation remains unavailable through the current tool surface.

The [generation prompt pack](generation-prompts.md) records the prompts, and
the [provenance manifest](generation-manifest.json) now records all four PNGs
and their metadata.

The follow-up [asset-first experiment](AssetExperiment/README.md) contains
three isolated component candidates, their evaluation, provenance, and a
Unity assembly proposal. Those candidates are also exploratory only and were
not integrated.

## Verified Level 2 reference

The structural reference is
[`shared-control.json`](../../Assets/Resources/Levels/shared-control.json),
the level catalog, and the Phase 4A render
[`05-gameplay-level-two.png`](../Phase4AScreenshots/05-gameplay-level-two.png).
The screenshot is a local project artifact; it has not been uploaded anywhere.

Level 2 contains exactly six graph nodes and six edges:

- Two sources, S1 and S2, along the bottom; each feeds its corresponding
  middle diverter, J1 or J2.
- Two destinations at the top: R accepts red and B accepts blue.
- Each diverter has one route to each destination. J1→B and J2→R form the
  crossing paths; their visual intersection is **not** a graph node or
  connection.
- Both diverters belong to switch group A. J2 is inverted.
- There are no group B diverters in this level. B remains OFF and PULSE B is
  unavailable.

All three concept directions must depict this exact comparison state:

> Immediately after `PULSE A` on beat 1: beat **1/4**, delivered **0/2**,
> A **ON**, B **OFF**, red package at J1, blue package at J2. The selected
> routes are J1→R and J2→B. Both packages have spawned and moved from their
> sources during that beat.

For the synchronized-effect view, show the paired transition from the
pre-command state (beat 0, A OFF, no active packages) to that beat-1 state.
The source hoppers may show queued red/blue cargo as staging detail, clearly
separate from active packages and the graph. The selected routes before the
pulse are J1→B and J2→R; after the pulse, both diverters flip together to the
routes listed above. Do not depict packages delivered at beat 1.

## Three art directions

These written directions were used for one portrait attempt each. The actual
outputs are not visually approved and did not follow the common composition
and state constraints reliably. See the output links and review notes below.

| Direction | Intended identity | Expected strengths | Main risks |
|---|---|---|---|
| A — Miniature Factory | A carefully crafted tabletop logistics machine: painted steel, brushed metal, small rollers, solid rails, soft studio illumination, tactile toy-scale construction. | Closest to the established industrial identity; physical parts can explain routing; convincing material depth without changing the game’s tone. | Fine metal detail can become visual noise or muddy at phone scale; matching separately generated machine parts may take more image editing. |
| B — Future Tech | Restrained precision machinery: modular ceramic/metal housings, subtle signal lamps, crisp route indicators, controlled cool/warm accents, no holographic clutter. | Pulse and active routes can read clearly; modular assets may be easier to reuse consistently. | Generic sci-fi appearance; glow can overpower package identity and reduce contrast; may feel less warm and distinctive. |
| C — Toy Factory | Premium, friendly tabletop equipment: rounded painted housings, tactile plastics, soft metal details, bright but controlled colors, pleasant light. | Strong silhouette readability and approachability; rounded forms can work well for small touch-screen presentation. | Can look childish or inexpensive if materials and proportions are not restrained; package and machine colors may compete. |

### Preliminary recommendation

Direction A was the preliminary written-brief recommendation, not an output
selection. The generated images do not provide a reliable basis to select A, B,
or C: their board topology and UI are unclear, and the visual differences are
weak. No direction is approved.

## Proposed visual review batch

The prompt package specifies four views for each direction:

1. A portrait gameplay mockup (9:16 target), with the board dominant and only
   essential HUD/control zones.
2. A close-up of the six-node machinery and the non-connecting crossing.
3. A red-plus package, blue-cross package, and group-A diverter close-up.
4. A synchronized before/after Pulse A visual explanation.

Only the first, portrait view has been generated for each direction (3 of the
proposed 12 views). The outputs are 576×1024 PNGs; each has prompt and
parameter provenance. The generated artwork did not reliably follow the
requested topology, package state, or UI composition, so the remaining nine
views should wait until the prompt/model approach is revised and approved.
Animation capabilities and alpha output have not been tested.

The machine-readable status is in
[`generation-manifest.json`](generation-manifest.json). It distinguishes
successful local generation from visual approval and records actual
dimensions, hashes, prompts, and parameters.

## Comparison criteria for human approval

Score each real concept at phone scale, using the same Level 2 state:

| Criterion | Review question |
|---|---|
| Artistic identity | Does it look like a distinctive miniature factory rather than a dashboard or generic AI scene? |
| Game readability | Can a player distinguish the two sources, diverters, destinations, selected routes, and the non-connection at the crossing at a glance? |
| Cargo identity | Are red-plus and blue-cross packages recognizable without color alone? |
| Visual quality | Are lighting, materials, silhouettes, and composition convincing at portrait gameplay scale? |
| Consistency | Do the close-up and component views retain the same camera, materials, palette, and proportions as the full mockup? |
| Mobile suitability | Does the board dominate without crowding the essential HUD and controls? |
| Animation feasibility | Can Unity reproduce the shown switch and package changes with separate assets and native transforms? |
| Integration/performance | Can components be separated into appropriately sized, reusable sprites without large atlases or excessive overdraw? |
| Cost and rights | Are the model, usage charge, output rights, and commercial-use restrictions acceptable and documented? |

The image model must not be relied on for final text, symbols, graph topology,
or route correctness. Human review must check those against the verified level
data before a direction is approved.

## Asset production blueprint after approval

Do not flatten the gameplay board into a background. Build the production set
from independent assets, reusing approved reference images and one shared
style recipe:

| Family | Reusable parts and state variants | Unity use |
|---|---|---|
| Conveyor | Straight belt tile, repeatable roller/tread strip, rail segments, endpoint caps, restrained direction marker, active/inactive overlays. Add diagonal and junction pieces only to match authored graph edges. | Tile/repeat belt surfaces along simulation edges; use end caps and rails as modular transforms. Keep route highlights as separate overlays. |
| Crossing | Separate upper bridge deck and lower route treatment, with rails that make layer order explicit. | Render at the verified intersection only; artwork must not imply a junction. |
| Diverter | Shared chassis, pivot, movable pointer/gate, group-A plate, state lamp, selected-route indication. | Static sprite plus Unity-controlled rotation/scale/lighting; both group-A machines receive one authoritative switch state. |
| Cargo | Shared crate silhouette and shadow; red package with plus; blue with cross; yellow variant reserved for existing levels. | Independent sprite layers or sprites per package kind; animate position between actual graph-node coordinates. |
| Source/destination | Source hopper and gate; receiving dock, accepted-kind mark, empty/receiving/delivered accents. | Reuse by node type and accepted package kind; trigger receiving feedback only from simulation events. |
| UI | Text-free button/card/panel skins, locked state, star shapes, progress fill, subtle factory trim. | Keep all localization, labels, counters, icons with text, and RTL layout in Unity/TMP rather than baked into artwork. |
| Effects | Pulse ring/light sheet if a generated sequence proves valuable; restrained delivery spark/success accent. | Use only as non-authoritative decoration; effects must not hide routes or change simulation timing. |

Use the approved concept as the reference for a small first production batch,
then generate/edit related pieces from that reference rather than prompting
each object independently. Keep camera angle, orthographic perspective,
key-light direction, palette, roughness/material language, object scale, and
outline/shadow treatment fixed. Record actual provider-exposed seed,
reference-strength, model, and version values only when the chosen tool
provides them.

For belt lengths, prefer a repeatable center strip with separately authored
caps and rails; tile/repeat the belt along an edge rather than non-uniformly
stretching the complete conveyor illustration. Use rotations or mirrored
variants only after checking directional details and light consistency.
Import sprites at the minimum resolution that remains crisp at the target
device density; use atlases for shared materials and closely related small
sprites, with alpha trimming and platform compression evaluated in Unity.
Measure actual atlas size, texture memory, overdraw, and draw calls before
increasing resolution or adding variants.

### Manifest and provenance

For each real output, record its asset ID/category, exact intended use, source
tool/model and version, generation date, exact prompt and provider-exposed
parameters, reference IDs, actual pixel dimensions, alpha status verified from
the file, animation/frame metadata where relevant, applicable license/terms
snapshot or link, review owner/status, and optimization/import notes. Keep the
original output and any edited derivative separately; link edits to their
source IDs. Never mark commercial rights approved without reviewing the
selected provider's current terms and the intended distribution.

## Animation strategy

AI-generated motion, if later available and useful, is reference material or a
source for non-interactive effects—not gameplay authority. Unity must own
interactive timing:

1. The command control responds immediately.
2. On `PULSE A`, one group-A signal begins and both diverter visuals react
   together from the simulation's resulting switch state.
3. Route pointers update to the routes returned by the existing
   `SimulationEngine`.
4. Packages animate from their actual previous node to the actual next node.
5. Delivery feedback occurs only on the corresponding delivery event.
6. Undo and Restart restore state from the simulation and cancel/reset pending
   visual transitions. Reduced motion skips travel/stagger effects without
   changing state or outcome.

Use native transforms/Animator or existing presentation logic for package
translation, diverter rotation, button response, and state-dependent effects.
Do not use a pre-rendered video for routes or package positions that must
respond to player commands. Generated sprite frames are optional and should be
used only when stable framing, consistent frame dimensions, and low drift can
be verified.

## Costs, rights, and next gate

- **Spent in this execution:** no generation charge; local Draw Things was used.
- **Commercial rights:** the SDXL model license is documented in the
  [model review](model-candidate-review.md); output rights and intended-use
  review remain open. No generated image is production-approved.
- **External data transfer:** none. The approved-reference list remains empty.
- **Next gate:** revise the composition/prompt strategy or select a different
  local model, then obtain human approval before generating more views or
  integrating any artwork. or Unity integration.

No simulation, Unity runtime, localization, or game asset files were changed
for this concept-discovery package.
