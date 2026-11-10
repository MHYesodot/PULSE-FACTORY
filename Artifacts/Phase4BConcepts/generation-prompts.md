# Phase 4B concept generation prompts

These prompts define a comparison package. One portrait (Shot 1) has been
generated for each concept direction; Shots 2–4 remain ungenerated. Combine
one shared composition prompt, one concept style prompt, and one shot brief
per image. The three portrait attempts used the same dimensions, settings,
seed, board description, and framing. They did not reliably follow the
requested topology or UI and are not approved concepts.

## Shared composition and state prompt

> Create a premium stylized mobile puzzle-game visual concept for the actual
> Pulse Factory Level 2 board, portrait 9:16 composition. Use only the explicit
> topology and state described here; no reference image is provided. The board
> is the dominant central object and shows exactly six
> functional nodes: two source machines along the bottom, two diverters at
> mid-height, and two matching destination docks along the top. Each bottom
> source feeds its corresponding middle diverter. Each diverter has two
> outgoing conveyor routes, one to each top destination. The diagonal routes
> cross in the center but are physically grade-separated: show one clearly
> raised bridge and one lower route, with opaque surfaces and no junction,
> transfer opening, or connection at the crossing. Do not add, remove, or
> relocate any node or connection.
>
> Depict the same post-command game state in every concept: immediately after
> Pulse A on beat 1 of 4, with zero of two packages delivered. Both diverters
> are synchronized members of group A and visibly ON; group B is OFF and has
> no diverter in this level. J2 is mechanically inverted. The red package
> carrying a clear plus mark is at the left diverter J1; the blue package
> carrying a clear cross/X mark is at the right diverter J2. The selected
> routes are J1 to the red destination R at upper left, and J2 to the blue
> destination B at upper right. Show inactive alternative routes as quieter
> machinery, not as selected paths. The packages are not yet at the
> destinations.
>
> Present a tactile, carefully art-directed factory with clear silhouettes,
> legible path hierarchy, controlled contrast, and restrained detail. Keep
> essential interface regions simple: a compact level/title and beat/delivery
> status above, large reserved space for the board, and reserved control
> space below for MOVE, PULSE A, PULSE B, Undo, and Restart. Do not rely on
> generated lettering: use empty label plates or simple neutral placeholders
> for all interface text. Do not draw logos, watermarks, extra package types,
> extra routes, extra switches, or decorative lines that look like graph
> connections. Render as polished original concept art, not a Unity UI
> screenshot or copied game.

## Style prompt — Concept A: Miniature Factory

> Miniature industrial logistics machine, sophisticated tabletop scale,
> painted steel housings, brushed metal edge details, small belt rollers,
> recessed dark belts, solid side rails, tactile bolts used sparingly,
> carefully designed source hoppers and receiving docks, soft warm-neutral
> studio key light from upper left with gentle fill, subtle ambient occlusion,
> believable material depth, precise toy-scale proportions, premium craft
> finish. Industrial but approachable; not photorealistic and not distressed.
> Use a restrained charcoal/steel palette with mint active-route accents,
> brass hardware, red-plus cargo, and blue-cross cargo. Maintain strong
> separation between active routes and crossed inactive routes.

## Style prompt — Concept B: Future Tech

> Restrained near-future logistics machine, precision-engineered modular
> housings, clean ceramic and satin-metal surfaces, crisp mechanical gates,
> subtle signal lamps and thin controlled light accents, elegant engineered
> geometry, soft neutral studio illumination from upper left, refined
> industrial science-fiction design. Keep all route lines physical, readable,
> and subordinate to the packages. Use dark graphite, pale steel, one mint
> active signal color, restrained amber details, red-plus cargo, and
> blue-cross cargo. No hologram clutter, no neon flood, no glowing text,
> screens, fantasy machinery, or excessive luminous edges.

## Style prompt — Concept C: Toy Factory

> Premium collectible toy-factory machinery, friendly rounded housings,
> carefully controlled vibrant painted colors, tactile matte plastic paired
> with small satin-metal details, substantial readable conveyor rails,
> smooth molded belt modules, approachable package docks, bright pleasant
> studio lighting from upper left, soft contact shadows and high-quality
> product-render craft. Keep proportions sophisticated rather than childish;
> make the equipment feel durable and mechanically plausible. Red-plus and
> blue-cross cargo must remain distinguishable from the machine colors.
> Restrained color blocking, not a rainbow palette, no toy characters or
> decorative clutter.

## Shot briefs

Use one shot brief per image, in addition to the shared composition/state
prompt and the selected style prompt.

### Shot 1 — Full portrait gameplay mockup

> Full-bleed portrait gameplay concept at mobile 9:16 aspect. Keep the factory
> board large and central, approximately the central half of the visual
> height. Include quiet reserved UI zones for Level 2, Beat 1/4, Delivered
> 0/2, and the primary/secondary controls described in the common prompt.
> Treat all words as empty text plates, not generated final copy. Make all six
> nodes and all six graph edges understandable at phone scale.

### Shot 2 — Machinery close-up

> Close product-art view of the same Level 2 board and same post-Pulse-A
> state, framed around both diverters and the complete route crossing. Preserve
> both diverters, both destination directions, and enough of both source
> feeds to verify the topology. Emphasize rail height, bridge separation,
> belt construction, mechanical gates, selected-route indicators, and the
> fact that the crossing is not a junction. Do not invent extra tracks or
> machinery nodes.

### Shot 3 — Package and diverter close-up

> Close product-art view showing the red crate with an unmistakable plus mark
> and the blue crate with an unmistakable cross/X mark, beside the synchronized
> group-A diverter hardware. Show both diverter pointers in the post-pulse
> state and make their selected routes consistent with the shared prompt.
> Emphasize consistent object scale, package silhouette, shadow, surface
> materials, and distinct non-color identifiers. No text is baked into the
> artwork.

### Shot 4 — Synchronized Pulse A explanation

> A clean two-panel before/after visual explanation of the same Level 2
> machine, with identical camera, scale, lighting, and geometry in both
> panels. Before: beat 0/4, A OFF, B OFF, no active packages on the graph;
> red and blue cargo may be visible behind source-hopper staging gates only.
> The dormant selected branches are J1→B and J2→R. After: beat 1/4,
> Delivered 0/2, A ON, B OFF, red-plus package at J1 and blue-cross package
> at J2; both group-A diverters respond simultaneously and selected routes
> become J1→R and J2→B. Show one restrained shared signal traveling to both
> diverters, then the state change; do not imply that packages have already
> been delivered. Use simple arrows/labels only if they cannot be confused
> with additional conveyors; otherwise use non-text visual emphasis. No
> unrelated motion or new game mechanic.

## Generation and review notes

- First verify the chosen model/tool can create actual image files. Record
  only parameters the tool exposes; do not assume seed or reference controls.
- Start with one low-cost render per style for Shot 1. Compare at phone scale
  before requesting the remaining nine views. If the service charges for each
  call, show the expected cost and get approval first.
- Use reference-image conditioning only if available and authorized. The
  local Phase 4A screenshot contains project UI and must not be uploaded to a
  hosted service without explicit permission.
- Do not ask the model to generate transparent sprite assets at this
  concept-review stage. Transparency must later be verified from actual file
  alpha, not inferred from a checkerboard appearance.
- Reject any image that changes the six-node topology, selected routes,
  crossing separation, package positions, switch states, or counters.
- Save untouched originals and per-image provenance in the companion
  manifest. Keep concept images distinct from approved production assets.
