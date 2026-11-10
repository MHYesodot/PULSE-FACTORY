# Pulse Factory Phase 4D.1 — Mechanical Board Framing

This follow-up fixes the Level 2 board's Canvas-dependent world scale and
revalidates its portrait composition. It changes presentation and test code
only; the deterministic simulation, level definitions, progression, and save
semantics are unchanged.

## What changed

- Detached the mechanical board, orthographic camera, and key light from the
  UI Canvas hierarchy. CanvasScaler and UI rebuilds can no longer resize the
  world geometry or shift the render rig.
- Put mechanical renderers on a dedicated layer. The board camera renders that
  layer into the viewport RenderTexture; other active cameras temporarily
  exclude it, then have their original layer visibility restored when the
  mechanical board is hidden or destroyed.
- Kept board geometry and camera fit stable across all switch combinations,
  package movement, Pulse, Undo, Restart, and locale changes. The test now
  settles the Canvas before recording the initial viewport dimensions.
- Retained the portrait layout and contrast changes across the gameplay HUD,
  board frame, and command controls. The 2D board remains the fallback.

The regression reproduced the prior restart zoom as a Canvas-parent transform
scale change. After detaching the rig, the measured board bounds remain
centered at `(0, 0.55, 0)` with size `(3.90, 1.58, 5.85)` before and after
Restart; the camera orthographic size remains `3.454`.

## Screenshot evidence

These are Unity Play Mode test captures. A temporary camera renders the Canvas
and board RenderTexture into the requested PNG dimensions; they are not device
screenshots. Unity batch reported `Screen.width`/`height` as `640×480`, so the
image dimensions validate the off-screen portrait render path, not native
device orientation or safe-area behavior.

**1080×1920**

- [Level selection](./Screenshots-1080/01-level-selection.png)
- [Level 2 — English](./Screenshots-1080/05-gameplay-level-two.png)
- [Level 2 — Hebrew](./Screenshots-1080/09-level-two-hebrew.png)
- [Pulse A](./Screenshots-1080/07-level-two-after-pulse.png)
- [Packages moving](./Screenshots-1080/08-package-moving.png)
- [Level 2 victory](./Screenshots-1080/11-level-two-victory.png)
- [After Undo](./Screenshots-1080/12-level-two-after-undo.png)
- [After Restart](./Screenshots-1080/13-level-two-after-restart.png)

**360×640**

- [Level selection](./Screenshots-360/01-level-selection.png)
- [Level 2 — English](./Screenshots-360/05-gameplay-level-two.png)
- [Level 2 — Hebrew](./Screenshots-360/09-level-two-hebrew.png)
- [Pulse A](./Screenshots-360/07-level-two-after-pulse.png)
- [Packages moving](./Screenshots-360/08-package-moving.png)
- [Level 2 victory](./Screenshots-360/11-level-two-victory.png)
- [After Undo](./Screenshots-360/12-level-two-after-undo.png)
- [After Restart](./Screenshots-360/13-level-two-after-restart.png)

The refreshed screenshots were visually inspected at both sizes. The entire
board remains inside its frame through the recorded states. At 360×640 the
board is fully visible but mechanical detail and secondary text are naturally
small; the central crossing remains visually dense.

## Validation

| Check | Result |
|---|---:|
| Focused Level 2 framing/state regression | 1 passed |
| Unity Edit Mode | 68 passed, 0 failed |
| Unity Play Mode | 17 passed, 0 failed |
| Unity screenshot test at 1080×1920 | 1 passed |
| Unity screenshot test at 360×640 | 1 passed |
| .NET DomainChecks (`net8.0`) | 67 passed, 0 failed |
| Resolution fit math | 360×640, 390×844, 1080×1920, and 1170×2532 passed |
| RenderTexture resize/release and camera aspect checks | Passed |

Unity tests used 6000.0.62f1. The .NET test project ran with SDK 8.0.428 after
installing the missing .NET 8 SDK/runtime. The focused Editor measurement
reported 461 renderers and 1.44 ms CPU per explicit `Camera.Render` call in
batch mode on an Apple M1 Pro. Unity reported a 640×480 screen and a
593×251 RenderTexture. This is an Editor-side CPU sample, not a GPU, frame
rate, or mobile-device performance benchmark.

## Remaining gaps

- No standalone/mobile build or physical iPhone test has been performed.
- Portrait sizing at the four listed resolutions is validated by projection
  fit math; device safe areas, OS UI, and touch targets still need real-device
  verification.
- The screenshots are off-screen Unity captures, not live device captures.
- The board is a readable 2.5D prototype, not final production art; the
  crossing is still dense at small display sizes, and lighting/materials
  require device and visual-production review.
- The 1.44 ms measurement does not establish runtime performance on a phone.
