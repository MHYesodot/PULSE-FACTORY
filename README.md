# PULSE FACTORY — Phase 3: UX/UI Polish Vertical Slice

A deterministic, turn-based conveyor-puzzle vertical slice with eight authored
levels, local progression, anonymous aggregate playtest statistics, and
English/Hebrew UI. Phase 3 polishes the retained level-selection and gameplay
screens without changing simulation or progression semantics.

## Status

Unity 6000.0.62f1 imports and compiles the project. The retained uGUI/TextMeshPro
interface now has a stronger level-card hierarchy, a larger board, clearer
package/route/switch indicators, touch-sized command controls, and terminal
feedback. The Phase 3 regression results and portrait 540×960 Unity test-player
screenshots are recorded below. They confirm rendered UI and the tested
gameplay paths; they are not an iPhone/device test or a claim of commercial
readiness. Hebrew support remains basic RTL text handling, not full bidi.

**iOS is an open gate:** this Editor has no iOS Build Support module. No Xcode
project was generated, no iOS build was executed, and no device was tested.
There are no backend, accounts, ads, purchases, multiplayer, or network
dependencies.

## Phase 3 screenshots

Captured from the Unity macOS test player at 540×960 portrait resolution.
The Unity **Development Build** watermark is from the test player.

| Screen | Screenshot |
|---|---|
| Level selection | [01-level-selection.png](Artifacts/Phase3Screenshots/01-level-selection.png) |
| Level 1 gameplay, Hebrew | [02-gameplay-hebrew.png](Artifacts/Phase3Screenshots/02-gameplay-hebrew.png) |
| Level 1 gameplay, English | [03-gameplay-english.png](Artifacts/Phase3Screenshots/03-gameplay-english.png) |
| Level 1 victory | [04-victory.png](Artifacts/Phase3Screenshots/04-victory.png) |
| Level 2 gameplay | [05-gameplay-level-two.png](Artifacts/Phase3Screenshots/05-gameplay-level-two.png) |
| Progressed level selection | [06-level-selection-progress.png](Artifacts/Phase3Screenshots/06-level-selection-progress.png) |

## Open and play

1. Install [Unity Hub](https://unity.com/download) using its official macOS
   installer. Sign in and activate an eligible license yourself. On Apple
   silicon, install Rosetta only if Unity requests it; do not disable Gatekeeper
   or other OS protections.
2. Install **Unity 6 LTS 6000.0.62f1, Apple silicon**, from the
   [release page](https://unity.com/releases/editor/whats-new/6000.0.62f1) or
   `unityhub://6000.0.62f1/f99f05b3e950`. Add this repository folder in Hub and
   open it with that version. Allow package resolution. The project includes
   uGUI 2.0.0, the Unity Test Framework, and the official TextMeshPro essential
   resources needed by the packaged font. No rendering-pipeline replacement is
   required.
3. Open [`Assets/Scenes/Tutorial.unity`](Assets/Scenes/Tutorial.unity) and press
   **Play**. Choose an unlocked route on the level-selection screen; Level 1 is
   the original tutorial.
4. The tutorial solution is **MOVE, MOVE, MOVE, PULSE A**. The simulation tests
   confirm both packages reach their matching destinations and win on beat 4.
   An incorrect route fails atomically; use **UNDO** to restore the previous
   state or **RESTART** to reset the level.
5. Complete a level to unlock the next. The eight solutions are authored and
   replayed by regression tests; they are not shown in the player UI.
6. Language and reduced-motion controls are available on the level screen and
   during gameplay. Packages use both color and distinct `+`/`X` marks. Hebrew
   strings remain logical text and are rendered through TextMeshPro RTL mode;
   this is basic Hebrew support, not full Unicode bidirectional-text support.
7. **EXPORT PLAYTEST DATA** writes local aggregate counters to
   `Application.persistentDataPath/exports/pulse-factory-playtest-v1.json`.
   Saves and statistics stay on the device; there is no account, session
   identifier, telemetry endpoint, or network transfer.

The **Pulse Factory > Play Tutorial** menu opens the authored scene and enters
Play Mode. **Validate Tutorial Scene** checks enabled build references, missing
script components, the camera/controller, and the tutorial JSON. These Editor
tools refuse to replace a dirty scene; save or close changes first.

Each accepted command advances exactly one logical beat. Pulses toggle their
group **before** movement. Input is locked while packages animate; reduced
motion skips movement animation without changing the simulation. The prototype
requests portrait orientation and anchors its interface to the device safe
area. No signing credentials or shipping player settings are included. Portrait
test-player screenshots were visually reviewed, but physical touch, device
safe-area behavior, and hardware-specific scaling remain unverified. See the
separate iOS build instructions below.

## Structure and dependencies

| Location | Responsibility |
|---|---|
| [`Assets/PulseFactory/Core`](Assets/PulseFactory/Core) | Immutable simulation, level validation/catalog, and bounded shortest-solution solver |
| [`Assets/PulseFactory/Services`](Assets/PulseFactory/Services) | Local progression, aggregate playtest statistics, and localization; no Unity API dependency |
| [`Assets/PulseFactory/Runtime`](Assets/PulseFactory/Runtime) | Retained uGUI/TMP controls, board and package views, input lockout, and safe-area layout |
| [`Assets/PulseFactory/Editor`](Assets/PulseFactory/Editor) | Scene validation, macOS build, and static bilingual font generation |
| [`Assets/Resources/Levels`](Assets/Resources/Levels) | Eight data-authored levels, catalog, and intended command sequences |
| [`Assets/Resources/Localization`](Assets/Resources/Localization) | English/Hebrew UI and level strings with synchronized keys/placeholders |
| [`Assets/Fonts`](Assets/Fonts) | Noto Sans Hebrew source and SIL Open Font License |
| [`Assets/PulseFactory/Tests`](Assets/PulseFactory/Tests) | Shared regression, TMP font, and Play Mode integration tests |
| [`Tools/DomainChecks`](Tools/DomainChecks) | Standalone NUnit runner linking shared C# sources and domain/service tests |

Core and Services have `noEngineReferences` assembly definitions. Runtime
references both plus Unity UI and TextMeshPro; neither domain assembly
references Runtime. The simulation remains pure C#. The graph and package views
are prototype geometry rather than final artwork.

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
This level-authoring validation checks structural correctness, not puzzle
solvability. The existing catalog and gameplay screens are polished in Phase 3;
see the screenshot and validation records below.

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
tests use a unique temporary save path per test and remove only their own
temporary data. The test override is compiled only for Editor/test builds.
Tests never read or overwrite ordinary player progress.

The default generated company/product settings currently place macOS saves at
`~/Library/Application Support/DefaultCompany/PULSE-FACTORY/progress.json`.
Editor and standalone player use this same location for ordinary gameplay.
Changing company/product identifiers later requires considering save migration.

## Localization and presentation

All interface copy is in the two locale JSON resources. Missing/duplicate keys
and mismatched key/placeholder sets are errors. Startup also checks the keys
actually required by the UI: removing a key from both locales cannot pass
parity validation and then throw repeatedly during rendering. Bad content
blocks initialization with a logged error. Red packages use a plus and blue
packages use an X, so type is not conveyed by color alone. UI strings and
instructions are data-driven.

The retained interface uses TextMeshPro with `isRightToLeftText` for the
unpointed Hebrew translations. A static SDF font is generated from Noto Sans
Hebrew; its source and SIL Open Font License are retained under
[`Assets/Fonts`](Assets/Fonts). This is basic Hebrew rendering, **not** a full
Unicode bidi engine, Arabic shaper, or comprehensive mixed-direction text
solution. Hebrew layout was reviewed in the portrait test-player screenshot;
mixed-direction edge cases, device safe-area fit, and physical touch-target
usability remain unverified. The board graph itself is not mirrored.

## Run tests

### Unity

Open **Window > General > Test Runner** and run Edit Mode, then Play Mode. The
Edit Mode suites cover simulation/domain/service behavior and the packaged
font glyphs. The Play Mode suite loads the real scene and covers the tutorial
solution/failure, animation lockout, undo/restart, local progression,
statistics, scene reloads, language/motion settings, and rendered layout
anchors. They do not prove physical touch behavior. Close other Editor
instances using this project before running command-line tests. Scene tests
are not an on-device player suite.

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

The portrait screenshot flow can be captured in a macOS test player:

```sh
mkdir -p Artifacts/Phase3Screenshots
PULSE_FACTORY_SCREENSHOTS="$PWD/Artifacts/Phase3Screenshots" "$UNITY" \
  -batchmode -projectPath "$PWD" -runTests -testPlatform StandaloneOSX \
  -testFilter PhaseThreeScreensShowLevelSelectionBilingualGameplayVictoryAndLevelTwo \
  -testResults "$PWD/TestResults/phase3-visual.xml" \
  -logFile "$PWD/TestResults/phase3-visual.log"
```

This builds/runs a Unity development test player and adds its watermark to
screenshots. It validates rendered portrait screens and scripted test paths,
not a shipping build, physical device, or manual touch interaction.

### Without Unity

With .NET SDK 8 installed:

```sh
dotnet test Tools/DomainChecks/DomainChecks.csproj
```

This compiles Core/Services with C# 9 and runs the shared domain/service tests
against copied JSON resources. It does **not** compile Runtime, test the TMP
asset, or execute the Play Mode suite.
The first run restores NUnit and the test runner; these dependencies are only
for this standalone development harness, not the game.

Coverage includes movement/spawn timing, A/B groups and inversion, simultaneous
group routing, vacated-node movement, collisions, destination failures,
victory/final-beat precedence, unavailable commands, undo/restart, deterministic
replay, malformed content, tutorial success/failure, save round-trips/recovery,
and locale parity/required keys/RTL line ordering.

## macOS validation player

The following produces a development player without publishing or configuring
production signing. The current Phase 2 player build succeeded; it has not been
manually launched or visually reviewed.

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
   Support**. This module is absent from the current Editor installation and
   was not installed during Phase 2.
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

## Phase 2 validation record — 2026-11-08

Environment: macOS 27.0.1, Apple M1 Pro/arm64; Unity Hub 3.22.6; Unity
6000.0.62f1 (f99f05b3e950); .NET SDK 8.0.427; Xcode 27.1 with iOS 27.1 SDK.
Unity Test Framework 1.6.0.

| Suite/check | Total | Passed | Failed | Skipped | Environment/evidence |
|---|---:|---:|---:|---:|---|
| Shared .NET regressions | 67 | 67 | 0 | 0 | .NET 8.0.427, Phase 3 final regression run |
| Unity Edit Mode | 68 | 68 | 0 | 0 | Unity 6000.0.62f1, batch/null graphics |
| Unity Play Mode | 12 | 12 | 0 | 0 | Unity 6000.0.62f1, batch mode; scene, controls, persistence, and level flows |
| Portrait screenshot flow | 1 | 1 | 0 | 0 | StandaloneOSX Unity test player; captures all six 540×960 screenshots |
| Tutorial/build-scene validation | — | Pass | — | — | Editor validation: enabled scene, camera/controller, components, tutorial data |
| Screenshot review | — | Pass | — | — | Level selection, Hebrew/English gameplay, victory, progress, and Level 2 captures visually reviewed |
| iOS export/Xcode/device | — | Not run | — | — | Unity iOS Build Support absent; no project export or device test |

Automated solver and replay tests verify all eight catalogued solutions and
shortest solution lengths. Play Mode verifies the tutorial's four-command win,
an incorrect-route failure, command lockout, undo/restart, and progression into
Level 2's authored `PULSE A`, `MOVE` solution. The separate StandaloneOSX
flow test captures rendered screens; the screenshots were visually reviewed.
These checks do not prove enjoyment or physical touch usability. The local
playtest data schema and corruption recovery are covered by the shared service
tests.

The static font and TMP essential resources are included in the project; the
font's source file and SIL Open Font License are retained under `Assets/Fonts`.
No iOS settings, production signing, publishing, or backend services were
configured. The screenshots come from a Unity development test player and carry
its watermark; they are not a shipping build or device test. Physical touch,
device safe-area behavior, hardware-specific scaling, and full bidirectional
text layout remain open checks.

## Historical Phase 1.1 validation record — 2026-11-08

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
