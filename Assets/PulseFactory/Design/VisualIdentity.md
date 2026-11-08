# Pulse Factory Visual Identity

## Direction

**Miniature industrial world:** a precise, friendly tabletop logistics machine.
Use dimensional, tactile component shapes rather than flat dashboard panels.
The simulation remains a deterministic 2D graph; decoration never implies a
connection or route that is absent from level data.

## Tokens

| Role | Token | Use |
|---|---|---|
| Deep ink | `#0E141F` | Screen and board surround |
| Graphite | `#161F2E` | Recessed factory deck |
| Steel | `#475E73` | Conveyor rails, chassis, control collars |
| Pale steel | `#94B0C2` | Mechanical highlights and inactive details |
| Belt | `#111A24` | Recessed conveyor tread |
| Mint | `#4FE8B3` | Selected route, live switch, successful delivery |
| Brass | `#D69C4D` | Hardware accents and earned stars |
| Cargo red | `#FF5C61` | Red package, always paired with a plus |
| Cargo blue | `#479FFF` | Blue package, always paired with a cross |
| Text | `#F2F7FF` | Primary text |
| Secondary text | `#A1B3C9` | Supporting labels; do not use for essential actions |

Color is never the sole identifier: cargo has a shape/symbol, switch groups
have letter labels, and active routing has both a directional marker and tint.

## Component anatomy

- **Deck:** inset graphite panel, a fine steel rim, sparse fasteners, no
  decorative grid that could be mistaken for a path.
- **Conveyor:** raised steel side rails, recessed dark belt, repeated tread
  plates, and a distinct arrow toward the actual destination node. The
  selected branch is mint; dormant branches remain visible in steel.
- **Crossing:** an opaque, raised bridge deck masks the lower belt; no junction
  node is added. Only graph-defined nodes permit package transfer.
- **Diverter:** round bolted chassis, central pivot, group label, live-state
  lamp, and a physical pointer aimed at the route selected by the simulation.
- **Source:** recessed loading port with an inward direction mark.
- **Destination:** raised dock with a cargo-kind stamp, receiving slot, and a
  mint arrival confirmation.
- **Package:** bevelled crate silhouette, top inset, cargo strap, and plus/cross
  mark. Red/blue remain redundant cues rather than the only distinction.
- **Controls:** large raised operator keys with a command glyph and label;
  utility actions are smaller and lower contrast. Disabled controls stay
  legible and visibly inactive.

## Type, layout, and access

- Use the bundled Noto Sans font. Level title: 22–27 UI points; action labels:
  at least 14; supporting board labels: at least 11 where available.
- Keep essential text contrast at or above 4.5:1 against its immediate surface.
  Do not rely on muted copy for commands, status, or cargo identity.
- Keep a minimum 44-point primary touch target and 40-point secondary target.
- Preserve the existing safe-area root and normalized portrait anchors for
  9:16 through 19.5:9 layouts. Keep board, controls, and status in that order.
- Base spacing on 4-point increments; use 8-point board insets and 12-point
  control/card insets.
- Resolve text direction from the first strong character, falling back to the
  selected locale for neutral-only symbols. Never reverse strings manually.

## Motion and state feedback

- Mechanical response uses short eased motion (120–340 ms), with one shared
  timing token for synchronized switch-group activation.
- A pulse shows an immediate control press, one group signal, all affected
  diverters moving together, then the resulting package beat.
- Packages follow their actual previous/next node positions. Delivery confirms
  only after the package reaches the dock.
- Victory reveals the earned stars with a brief stagger and keeps the next
  level action prominent. Failure uses a restrained amber warning.
- Reduced motion skips movement and stagger animations while preserving all
  state, labels, and immediate success/failure feedback.
