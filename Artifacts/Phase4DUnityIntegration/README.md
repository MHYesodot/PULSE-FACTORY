# Pulse Factory Phase 4D — Unity Mechanical Integration

This package adds an optional Unity 3D presentation for **Level 2 — Shared
Control**. The existing UI board remains available, is kept synchronized, and
continues to render every other level. The simulation, level JSON, routing
rules, progression, and save semantics are unchanged.

## What is included

- Four Unity-imported Blender FBX models in
  `Assets/PulseFactory/Mechanical/Models/`.
- Seven generated prefabs, shared materials, and Resources settings in
  `Assets/PulseFactory/Mechanical/`.
- `MechanicalPrefabBuilder`, which checks the Unity FBX imports, validates the
  diverter pivot hierarchy, creates the prefabs/settings, and writes the
  [Unity import report](./unity-import-report.json).
- A graph-driven 3D board presenter, enabled only when the settings asset is in
  `Mechanical3DLevel2` mode and the selected level ID is `shared-control`.
- Play Mode coverage for the Level 2 route graph, both group-A diverters,
  route-state changes, input lockout, Undo/Restart, package delivery, and the
  existing renderer fallback.
- Unity-rendered screen captures at both requested portrait sizes.

To rebuild prefabs in Unity 6000, use **Pulse Factory → Build Mechanical
Prefabs**. The equivalent batch command is:

```sh
"/Applications/Unity/Hub/Editor/6000.0.62f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -projectPath "$PWD" \
  -executeMethod PulseFactory.Editor.MechanicalPrefabBuilder.Build \
  -logFile /tmp/pulse-phase4d-prefabs.log -quit
```

## Presentation boundaries

- `SimulationEngine` and `Assets/Resources/Levels/shared-control.json` remain
  authoritative. The presenter reads the actual node/edge collections and
  calls `SimulationEngine.Route` for diverter state; it does not duplicate
  routing or win/fail rules.
- The six authored edges are all rendered. Only the J1→B visual route is
  elevated at the non-connecting crossing. The inverted J2 diverter follows
  the engine's resolved route, so the shared Pulse A state remains correct.
- Conveyor modules are placed edge-by-edge without stretching the imported
  model. Cargo positions follow simulation movement events and settle at their
  logical node positions. The original 2D board is updated alongside the 3D
  board and becomes visible automatically when the mechanical path is not
  selected or its resources are unavailable.
- A dedicated orthographic camera renders into a `RenderTexture` inside the
  existing board viewport. HUD, text, controls, localization, and outcome
  feedback remain Unity UI.

The FBX import report records Unity's measured dimensions and mesh hierarchy.
The conveyor import measures 0.75 m along its module length, so the repeated
module spacing is aligned to the model rather than scaling the rollers or end
caps. The diverter's imported arm is nested under the preserved
`ASSET_DIVERTER_ArmPivot` transform.

## Screenshots

All captures are produced by Unity Play Mode tests. For deterministic batch
capture, a temporary camera renders the Unity Canvas and its mechanical-board
RenderTexture into PNGs; these are not Blender renders or device screenshots.

**1080×1920**

- [Level selection](./Screenshots-1080/01-level-selection.png)
- [Level 2 — English](./Screenshots-1080/05-gameplay-level-two.png)
- [Level 2 — Hebrew](./Screenshots-1080/09-level-two-hebrew.png)
- [Pulse A state](./Screenshots-1080/07-level-two-after-pulse.png)
- [Packages in motion](./Screenshots-1080/08-package-moving.png)
- [Victory](./Screenshots-1080/04-victory.png)

**360×640**

- [Level selection](./Screenshots-360/01-level-selection.png)
- [Level 2 — English](./Screenshots-360/05-gameplay-level-two.png)
- [Level 2 — Hebrew](./Screenshots-360/09-level-two-hebrew.png)
- [Pulse A state](./Screenshots-360/07-level-two-after-pulse.png)
- [Packages in motion](./Screenshots-360/08-package-moving.png)
- [Victory](./Screenshots-360/04-victory.png)

The screenshots were visually inspected at both sizes. They establish the
Unity Canvas/board composition and localization, but not final iPhone behavior.
The 1080×1920 package-motion capture still crowds/crops the board's end
stations after Pulse A; changing the camera margin did not remove the mismatch
between that active-state capture and the resting frame. The camera adjustment
was reverted rather than making the resting/mobile board unnecessarily small.

## Validation

| Validation | Result |
|---|---:|
| Unity FBX import and prefab generation | Passed — 4 models, 7 prefabs |
| Unity Edit Mode tests | 68 passed, 0 failed, 0 skipped |
| Unity Play Mode tests | 17 passed, 0 failed, 0 skipped |
| Focused screenshot test at 1080×1920 | 1 passed |
| Focused screenshot test at 360×640 | 1 passed |
| .NET DomainChecks NUnit tests | 67 passed, 0 failed, 0 skipped |

Unity tests used 6000.0.62f1. The repository's .NET project targets net8.0;
because only the .NET 10 runtime is installed, its tests were run with
`DOTNET_ROLL_FORWARD=Major` after restoring the net8 targeting pack.

The mechanical folder contains 20 non-metadata assets totaling 383,109 bytes.
An Editor Play Mode measurement counted 462 Unity renderers and averaged
0.57 ms of CPU time per explicit `Camera.Render` call after warm-up, with a
580×169 board RenderTexture in the batch test's 640×480 screen environment.
An earlier cold run measured 8.32 ms per call. This is only a local Editor
submission-time sample—not a stable frame-rate, GPU, draw-call, or mobile
performance benchmark. iPhone performance and thermals remain unverified.

## Remaining gaps

- No standalone/mobile build or physical iPhone test has been performed.
- The 1080×1920 active-state package-motion frame does not yet keep every board
  endpoint comfortably in view; this needs a live-device framing pass.
- The board is a readable 2.5D proof, not a fully optimized production renderer;
  renderer count, batching, lighting, and shadows need a device profiling pass.
- Gameplay screenshots use Unity's off-screen Canvas render path in batch tests,
  not a live device display capture.
- The asset materials and station prefabs are generated placeholders around the
  approved FBX models and will need visual review before production-level polish.
