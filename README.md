# Pulse Factory — phase 1.1

A deterministic, turn-based conveyor puzzle prototype. No backend, accounts,
analytics, monetization, or real-time reaction mechanics.

## Status

Implemented: isolated simulation, validated JSON levels, tutorial scene and
placeholder rendering, command controls, animation lockout, undo/restart,
versioned local progression, English/Hebrew resources, Edit Mode regression
tests, and a Play Mode scene smoke test.

**The tutorial is confirmed playable in Unity Editor on macOS.** Unity
6000.0.62f1 imports the project and compiles the runtime, Editor tools, and test
assemblies. The standalone macOS development player also builds and runs.
Automated tests and user-performed mouse/visual checks are recorded separately
below. This remains a geometric prototype, not a commercial-quality interface.

**iOS is an open gate:** the configured Editor has no iOS Build Support module.
No Xcode project was generated, no iOS build was executed, and no physical-device
gameplay or touch/Safe Area validation was performed.

## Open and play

1. Install [Unity Hub](https://unity.com/download) using its official macOS
   installer. Sign in and activate an eligible license yourself. On Apple
   silicon, **Rosetta 2 is required even for the native Editor**: use Apple's
   installation prompt or `softwareupdate --install-rosetta` and review/accept
   its terms. Do not disable Gatekeeper or other OS protections.
2. Install **Unity 6 LTS 6000.0.62f1, Apple silicon**, from the
   [release page](https://unity.com/releases/editor/whats-new/6000.0.62f1) or
   `unityhub://6000.0.62f1/f99f05b3e950`. Add this repository folder in Hub and
   open it with that version. No Editor upgrade is necessary. Allow package
   resolution; Unity resolves its bundled Test Framework 1.6.0 and NUnit 2.0.5
   package. The generated lockfile, project settings, and asset metadata are now
   included. No replacement project or rendering pipeline is needed.
3. Open [`Assets/Scenes/Tutorial.unity`](Assets/Scenes/Tutorial.unity) and press
   **Play**. It is also registered as the first build scene.
4. Use **MOVE, MOVE, MOVE, PULSE A**. Red arrives on beat 2, blue on beat 4,
   earning three stars. Four MOVE commands instead fail on beat 4.
5. **UNDO** restores the last logical snapshot, including after failure/victory.
   **RESTART** clears history and restores beat 0 and initial switches.
6. Language and animation settings are available below the gameplay controls.
   PULSE B is disabled in this tutorial because there are no group B diverters.
7. For representative mobile layout, select **9:16** in the Game view aspect
   dropdown (add an Aspect Ratio entry of 9 by 16 if needed) and use **Fit/1x**.
   The interface fits a 480-by-900 portrait canvas; wide Free Aspect windows
   intentionally have horizontal margins. Game view **2x** is Editor zoom, not
   application UI scaling. A 9:16 viewport uses the full height and about 95% of
   the width. Maximize the Game tab if the docked preview is physically small.

The **Pulse Factory > Play Tutorial** menu opens the authored scene and enters
Play Mode. **Validate Tutorial Scene** checks enabled build references, missing
script components, the camera/controller, and the actual level JSON. These
tools refuse to replace a dirty scene; save or close your changes first.

Each command costs exactly one beat. Pulses toggle their group **before**
movement. Commands, undo, restart, and settings are locked during the 0.32-second
animation; disabling animation makes transitions immediate. Gameplay has no
timer and never depends on animation speed. The immediate-mode controls accept
mouse/touch events; mouse operation is confirmed, but actual touch interaction
still needs device verification.

The prototype requests portrait orientation and fits its board inside the
screen safe area. No signing credentials or shipping player settings are
included. See the separate iOS build instructions below.

## Structure and dependencies

| Location | Responsibility |
|---|---|
| [`Assets/PulseFactory/Core`](Assets/PulseFactory/Core) | Immutable domain models, validation/JSON codec, simulation engine, undo/restart session |
| [`Assets/PulseFactory/Services`](Assets/PulseFactory/Services) | Local save storage and localization; no Unity API dependency |
| [`Assets/PulseFactory/Runtime`](Assets/PulseFactory/Runtime) | Unity input/UI controller and replaceable geometric board renderer |
| [`Assets/PulseFactory/Editor`](Assets/PulseFactory/Editor) | Scene validation/launch menu and reproducible macOS validation build |
| [`Assets/Resources/Levels`](Assets/Resources/Levels) | Versioned authored level data |
| [`Assets/Resources/Localization`](Assets/Resources/Localization) | English/Hebrew strings with identical keys and format arguments |
| [`Assets/PulseFactory/Tests`](Assets/PulseFactory/Tests) | Edit Mode domain/services tests and Play Mode scene smoke test |
| [`Tools/DomainChecks`](Tools/DomainChecks) | Standalone NUnit runner linking the same C# sources and Edit Mode tests |

Core and Services have `noEngineReferences` assembly definitions. Runtime
references both; neither domain assembly references Runtime. Modules remain
small: Core combines tightly related simulation and level code rather than
creating an assembly for every namespace. Runtime presentation uses built-in
IMGUI and temporary geometry, with no external UI/art dependency.

## Simulation contract

`SimulationEngine.Step(current, command)` returns a new `SimulationResult`
containing an immutable state and ordered domain events. It never mutates its
input. Only the engine constructs states, and a state belongs to its original
level definition. A `GameSession` owns the current state and undo stack.

Order for each beat:

1. Reject invalid/unavailable commands or terminal/foreign states without
   consuming a beat or changing history.
2. Toggle A or B if requested; MOVE leaves both unchanged.
3. Spawn packages scheduled for the upcoming beat.
4. Route **all** active packages, including the new spawns.
5. Check the complete movement batch for conflicts and invalid deliveries.
6. Apply all movements simultaneously.
7. Resolve destination deliveries.
8. Award victory, otherwise fail if the beat limit has been reached.
9. Return state and events.

Every active package advances one edge. Entering a node vacated during that beat
is legal. Two packages entering the same non-destination node collide.
Destinations accept simultaneous deliveries only when kinds and remaining
capacity match. Victory requires all scheduled packages to be delivered;
future scheduled packages prevent premature victory.

Conflict priority is collision, wrong destination, then destination capacity.
On a rejected movement batch, **no package moves or delivers**. The resulting
failed state retains the consumed beat, toggled switches, and any newly spawned
packages at their sources. Undo returns the exact pre-command state. Beat-limit
failure instead happens after valid movement/delivery, and victory on the final
allowed beat takes precedence over that limit.

Packages/events use ordinal package-ID ordering, never dictionary iteration
order or Unity instance IDs. Switch events precede spawn events; all movement
events precede delivery events. Replays compare both states and events.

## Level format and adding content

[`tutorial.json`](Assets/Resources/Levels/tutorial.json) is the authoritative
complete schema-v1 example:

- `id`, `schemaVersion` (currently `1`).
- `nodes`: unique `id`, exact `type` (`Source`, `Conveyor`, `Diverter`,
  `Destination`), finite logical `x`/`y`. The renderer fits these coordinates to
  the board; positive y is downward.
- `edges`: unique `id`, `from`, `to`; graph must be directed and acyclic.
- Diverters additionally specify `group` (`A`/`B`), `falseEdge`, `trueEdge`, and
  optional `inverted` (defaults to false).
- Destinations specify `accepts` (package kind) and positive `requiredCount`.
- `initialSwitches`: explicit Boolean `a` and `b`.
- `spawns`: unique package `id`, `kind`, source node `source`, 1-based `beat`.
- `maxBeats`, `threeStarBeats`, `twoStarBeats` satisfy
  `0 < threeStarBeats <= twoStarBeats <= maxBeats`.

The selected diverter output is `groupState XOR inverted`: false chooses
`falseEdge`, true chooses `trueEdge`. A pulse affects every diverter in its group
through this shared state; no scene-object traversal implements the rule.

Sources have no incoming edges and exactly one outgoing edge. Conveyors have
one outgoing edge and may have multiple incoming edges (merges). Destinations
have no outgoing edges. A source may spawn at most one package per beat. Total
destination requirements per kind must equal the authored spawn count.
Duplicate IDs, bad references/types/groups/routes, invalid schedules, cycles,
missing destinations, and invalid beat/star limits throw
`LevelValidationException`; invalid content is not repaired.

To add a level:

1. Copy the tutorial JSON to a new file under `Assets/Resources/Levels`.
2. Change IDs, graph, schedules, requirements, and limits.
3. Assign the resource path (without `.json`) and a 1-based level number on the
   scene's GameController component, or duplicate the scene and assign it there.
4. Add a solution/failure regression test before using the level.

The simulation accepts arbitrary package-kind IDs, but the placeholder renderer
explicitly supports **red and blue** only. New visual kinds require palette and
symbol/localization entries, not simulation changes. The UI currently shows
tutorial-specific instructions; replace those resource strings for other
content. A level selection/catalog screen is not part of this phase.
Validation checks structural correctness, not puzzle solvability or visual
layout quality.

## Persistence

`progress.json` under `Application.persistentDataPath` stores schema version 1,
highest unlocked level (1-based), best stars per level ID, locale, and reduced
motion. Completion records are monotonic: replaying for fewer stars does not
lower a record. Undo/restart do not erase already earned progression.

Writes serialize and flush a temporary sibling file, then replace the original
and retain a `.bak` copy. Missing saves initialize normally. Corrupt,
incompatible, or unreadable saves produce an explicit log/UI warning and fresh
in-memory progress without deleting the original; the next successful save
backs it up. Failed writes are reported, not treated as successful persistence.
Backups are not automatically loaded or migrated. Storage is single-process.
Batch-mode gameplay uses a separate `progress-batch.json`. Editor Play Mode
tests additionally use a unique temporary save path per test, including when
run interactively, and remove only their own temporary data. The test override
is compiled only in the Editor. Tests never read or overwrite player progress.

The default generated company/product settings currently place macOS saves at
`~/Library/Application Support/DefaultCompany/PULSE-FACTORY/progress.json`.
Editor and standalone player use this same location for ordinary gameplay.
Changing company/product identifiers later requires considering save migration.

## Localization and presentation

All interface copy is in the two locale JSON resources. Missing/duplicate keys
and mismatched key/placeholder sets are errors. Startup also checks the keys
actually required by the UI: removing a key from both locales cannot pass
parity validation and then throw repeatedly during rendering. Bad content
blocks gameplay with a logged error and the localized load-error screen when
the English error resource is available; a missing English error resource is a
fatal logged initialization error. Color and `+`/`X` symbols identify
packages and matching destinations independently of color alone.

Hebrew mirrors control order and uses visual ordering for the supplied
unpointed Hebrew strings. Paragraphs wrap in logical order before their lines
are reordered for IMGUI. ASCII tokens/numbers retain their internal order.
The graph itself is not mirrored. A system font with Hebrew glyphs is requested;
if unavailable, the UI remains English and reports the limitation.

This is a deliberately small RTL foundation, **not** a complete Unicode bidi
engine, Arabic shaper, or font-distribution solution. Hebrew font availability,
mixed punctuation, line layout, safe-area fit, and minimum touch-target sizes
have been manually checked in the macOS Editor but still require target-device
checks. Final artwork and production UI can replace
BoardRenderer without changing simulation.

## Run tests

### Unity

Open **Window > General > Test Runner** and run Edit Mode, then Play Mode.
The Editor Play Mode suite loads the real tutorial scene and covers the full
solution, wrong routing and recovery, unavailable/terminal commands, animation
lockout, undo/restart, scene reloads, isolated persistence, and Hebrew/reduced
motion settings. Presentation regressions check conveyor endpoints under
scaled/translated viewports, 9:16 coverage, and asymmetric Safe Area geometry.
These are not screenshot or physical-touch tests. Close other Editor instances
using this project before running command-line tests. The scene tests are
Editor-only; they are not an on-device player test suite.

Command-line alternatives (substitute your actual Editor path):

```sh
UNITY="/Applications/Unity/Hub/Editor/6000.0.62f1/Unity.app/Contents/MacOS/Unity"
mkdir -p TestResults
"$UNITY" -batchmode -nographics -projectPath "$PWD" -runTests \
  -testPlatform EditMode -testResults "$PWD/TestResults/editmode.xml" \
  -logFile "$PWD/TestResults/editmode.log"
"$UNITY" -batchmode -projectPath "$PWD" -runTests \
  -testPlatform PlayMode -testResults "$PWD/TestResults/playmode.xml" \
  -logFile "$PWD/TestResults/playmode.log"
```

### Without Unity

With .NET SDK 8 installed:

```sh
dotnet test Tools/DomainChecks/DomainChecks.csproj
```

This compiles Core/Services with C# 9 and runs all 61 shared tests against copied
JSON resources. It does **not** compile Runtime or execute the Play Mode test.
The first run restores NUnit and the test runner; these dependencies are only
for this standalone development harness, not the game.

Coverage includes movement/spawn timing, A/B groups and inversion, simultaneous
group routing, vacated-node movement, collisions, destination failures,
victory/final-beat precedence, unavailable commands, undo/restart, deterministic
replay, malformed content, tutorial success/failure, save round-trips/recovery,
and locale parity/required keys/RTL line ordering.

## macOS validation player

The following produces a development player without publishing or configuring
production signing:

```sh
"$UNITY" -batchmode -nographics -projectPath "$PWD" \
  -executeMethod PulseFactory.Editor.PrototypeValidation.BuildMacValidationPlayer \
  -quit -logFile "$PWD/TestResults/mac-build.log"
open Builds/macOS/PulseFactory.app --args \
  -screen-fullscreen 0 -screen-width 540 -screen-height 960
```

Define `UNITY` as in the test commands and create `TestResults` first.
The generated app is ignored by Git. It is a local development build, not a
notarized distribution. Unity's development-player discovery/profiling traffic
is not a game backend; local saves do not require a network.

## iOS build gate and instructions

1. In Unity Hub, choose **Installs > 6000.0.62f1 > Add modules > iOS Build
   Support**. It was not installed during this phase; the user chose to continue
   macOS validation without it.
2. Open **File > Build Profiles**, select iOS, and switch the active platform.
   Ensure the tutorial scene remains enabled in the scene list.
3. Use a development bundle identifier you control; choose portrait orientation
   in Player Settings and a compatible target SDK. The generated defaults are
   not a shipping configuration. Use **Build**, not Build and Run, and export
   into `Builds/iOS`.
4. Open the generated Xcode project/workspace. For a compile-only device-SDK
   check, with no signing or installation, use the generated Unity-iPhone
   scheme (adjust the project path if Unity emits a workspace):

   ```sh
   xcodebuild -project Builds/iOS/Unity-iPhone.xcodeproj \
     -scheme Unity-iPhone -configuration Debug -sdk iphoneos \
     -destination 'generic/platform=iOS' \
     -derivedDataPath Builds/iOSDerivedData CODE_SIGNING_ALLOWED=NO build
   ```

5. Only with explicit device/development-signing authorization, select the
   authorized team and connected iPhone in Xcode and run a device smoke test.
   Check touch targets, orientation, notches/home indicator, visual scaling,
   Hebrew fonts, save/relaunch, and IL2CPP serialization.

Xcode/SDK presence and a connected device are **not** iOS validation. The export,
compile-only command, signing, deployment, and device checks above have **not**
been executed. Do not publish, archive for distribution, or configure
production signing as part of this phase.

## Phase 1.1 validation record — 2026-11-08

Environment: macOS 27.0.1 (26A434), Apple M1 Pro/arm64; Unity Hub 3.22.6;
Unity 6000.0.62f1 (f99f05b3e950), Metal; .NET SDK 8.0.427/runtime 8.0.33;
Xcode 27.1 (27A9275), iOS/iOS Simulator SDK 27.1. .NET was reused from the
isolated session installation, not installed globally. An iPhone 14 Pro Max
was detected as connected; it was not deployed to or tested.

| Suite/check | Total | Passed | Failed | Skipped | Environment/evidence |
|---|---:|---:|---:|---:|---|
| Shared .NET regressions | 61 | 61 | 0 | 0 | .NET 8, `TestResults/phase-1.1-dotnet.trx` |
| Unity Edit Mode | 61 | 61 | 0 | 0 | Configured Editor/Mono, batch/null graphics, `TestResults/phase-1.1-editmode.xml` |
| Unity Play Mode | 10 | 10 | 0 | 0 | Configured Editor/Mono, Metal, `TestResults/phase-1.1-playmode.xml` |
| Scene/build validation | — | Pass | — | — | Authored scene, component/build references, real resource parse |
| macOS development build/launch | — | Pass | — | — | `Builds/macOS/PulseFactory.app`, Metal |
| iOS export/build/device | — | Not run | — | — | Required Unity iOS module absent |

Results/logs remain in ignored `TestResults`; generated assets/settings and the
package lockfile are retained as reproducible project configuration. A prior
Play Mode run had 9 passes/1 failure because the OS changed its default audio
device and Unity logged an initialization warning. The affected test passed
alone, then all 10 passed together without suppressing logs or weakening tests.

Confirmed project defects fixed:

- Invalid `com.unity.modules.textrendering` dependency blocked package
  resolution. Unity already bundles the text-rendering assembly.
- Unqualified renderer `EventType` conflicted with `UnityEngine.EventType`,
  preventing Runtime compilation; an explicit domain alias resolves it.
- Conveyor rotation used an unscaled GUI pivot with an already transformed
  board. Local line rotation is now composed **before** the viewport transform.
  Lines and arrowheads stay attached at portrait and wide-window scales.
- The original Play Mode test read player settings and conditionally skipped
  the animation assertion. Tests now use fresh isolated saves and always check
  animation lockout.
- Locale parity alone missed a required key removed from both files. Startup
  now validates required UI keys before drawing.

**User-performed runtime checks (not agent screenshot/mouse automation):**
the tutorial loads; MOVE/PULSE A work; the four-command solution wins;
wrong routing reports failure; Undo/Restart work; English and Hebrew remain
readable. After the rendering fix, the user confirmed no stray lines cross the
controls, the 9:16 board/controls fill their portrait area and remain usable,
and the standalone player retains best stars and language/animation settings
after quitting and reopening. The agent's macOS screen-capture/input permissions
were unavailable, so these observations are explicitly attributed to the user.

The blue package remaining at the diverter on wrong-destination failure is
intentional atomic rejection, not an animation defect. Wide Free Aspect
letterboxing and primitive artwork are intentional limitations, not grounds
for a visual redesign in this phase.

No project compiler errors or unhandled gameplay exceptions remained in the
successful checks. Editor logs still contain non-blocking Hub licensing
diagnostics (signature/access-token messages followed by successful Personal
entitlement resolution) and a disabled built-in Animation component diagnostic.
These were not suppressed or treated as project test failures. Investigate
Hub sign-in/support if licensing stops resolving; no OS protection was bypassed.

## Next phase (not implemented)

1. Close the iOS gate: install the matching module, export/compile, then perform
   an authorized iPhone smoke test with real touch, Safe Area, and Hebrew checks.
2. For a scoped polished vertical slice, retain the simulation and replace the
   placeholder presentation with accessible mobile controls, a verified
   font/bidi solution, and a small set of final-quality visual/audio feedback.
3. Add a small curated level set and localized instructions with solution
   regressions; profile on-device before considering commercial systems.
