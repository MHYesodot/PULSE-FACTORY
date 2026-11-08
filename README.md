# Pulse Factory — phase 1

A deterministic, turn-based conveyor puzzle prototype. No backend, accounts,
analytics, monetization, or real-time reaction mechanics.

## Status

Implemented: isolated simulation, validated JSON levels, tutorial scene and
placeholder rendering, command controls, animation lockout, undo/restart,
versioned local progression, English/Hebrew resources, Edit Mode regression
tests, and a Play Mode scene smoke test.

**Unity is not installed on the implementation machine.** The shared C# suite
was compiled and run using .NET SDK 8.0.427: **60 passed, 0 failed**. This verifies
the actual domain/services sources and tutorial JSON, not a parallel simulation.
Core and Services also compiled against **.NET Standard 2.1 / C# 9** with zero
warnings/errors. JSON/assembly configuration and scene GUID references passed
structural checks.
Unity import/compilation, scene appearance, mouse/touch interaction, Play Mode
tests, and an iOS/IL2CPP build remain **unverified**. This is a source-ready
prototype, not a certified device build.

## Open and play

1. Install Unity Hub and **Unity 6 LTS 6000.0.62f1** (the declared project target).
   No Editor version was installed or detected when this project was created.
2. Add this repository folder as a project and open it. Allow Unity to resolve
   the Test Framework and built-in modules. Unity will generate default project
   settings, remaining `.meta` files, and the package lockfile on first import;
   retain these in source control after a successful import.
3. Open [`Assets/Scenes/Tutorial.unity`](Assets/Scenes/Tutorial.unity) and press
   **Play**. It is also registered as the first build scene.
4. Use **MOVE, MOVE, MOVE, PULSE A**. Red arrives on beat 2, blue on beat 4,
   earning three stars. Four MOVE commands instead fail on beat 4.
5. **UNDO** restores the last logical snapshot, including after failure/victory.
   **RESTART** clears history and restores beat 0 and initial switches.
6. Language and animation settings are available below the gameplay controls.
   PULSE B is disabled in this tutorial because there are no group B diverters.

Each command costs exactly one beat. Pulses toggle their group **before**
movement. Commands, undo, restart, and settings are locked during the 0.32-second
animation; disabling animation makes transitions immediate. Gameplay has no
timer and never depends on animation speed. The immediate-mode controls accept
mouse/touch events; actual device interaction still needs verification.

For an iOS build, install Unity's iOS Build Support module, switch the build
target to iOS, configure bundle ID/signing, and build through Xcode. The
prototype requests portrait orientation and fits its board inside the screen
safe area. No signing credentials or shipping player settings are included.

## Structure and dependencies

| Location | Responsibility |
|---|---|
| [`Assets/PulseFactory/Core`](Assets/PulseFactory/Core) | Immutable domain models, validation/JSON codec, simulation engine, undo/restart session |
| [`Assets/PulseFactory/Services`](Assets/PulseFactory/Services) | Local save storage and localization; no Unity API dependency |
| [`Assets/PulseFactory/Runtime`](Assets/PulseFactory/Runtime) | Unity input/UI controller and replaceable geometric board renderer |
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
Batch-mode gameplay uses a separate `progress-batch.json`.

## Localization and presentation

All interface copy is in the two locale JSON resources. Missing/duplicate keys
and mismatched key/placeholder sets are errors. Color and `+`/`X` symbols identify
packages and matching destinations independently of color alone.

Hebrew mirrors control order and uses visual ordering for the supplied
unpointed Hebrew strings. Paragraphs wrap in logical order before their lines
are reordered for IMGUI. ASCII tokens/numbers retain their internal order.
The graph itself is not mirrored. A system font with Hebrew glyphs is requested;
if unavailable, the UI remains English and reports the limitation.

This is a deliberately small RTL foundation, **not** a complete Unicode bidi
engine, Arabic shaper, or font-distribution solution. Hebrew font availability,
mixed punctuation, line layout, safe-area fit, and minimum touch-target sizes
must be checked on target devices. Final artwork and production UI can replace
BoardRenderer without changing simulation.

## Run tests

### Unity

Open **Window > General > Test Runner** and run Edit Mode, then Play Mode.
The Play Mode test loads the real tutorial scene, exercises the input lockout,
and verifies undo/restart state. It does not claim screenshot or physical
touch validation.

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

This compiles Core/Services with C# 9 and runs all 60 shared tests against copied
JSON resources. It does **not** compile Runtime or execute the Play Mode test.
The first run restores NUnit and the test runner; these dependencies are only
for this standalone development harness, not the game.

Coverage includes movement/spawn timing, A/B groups and inversion, simultaneous
group routing, vacated-node movement, collisions, destination failures,
victory/final-beat precedence, unavailable commands, undo/restart, deterministic
replay, malformed content, tutorial success/failure, save round-trips/recovery,
and locale parity/RTL line ordering.

## Next phase (not implemented)

1. Import/run in Unity, resolve any Editor-specific issues, execute both test
   suites, and validate tutorial input and Hebrew rendering on an iPhone.
2. Replace IMGUI with production mobile UI and a verified font/bidi solution;
   add visual regression checks and accessible sizing.
3. Add a small curated level set, level catalog/selection, and localized
   per-level instructions with solution regressions.
4. Polish movement feedback/art/audio, then profile and prepare signed device
   builds before considering commercial systems.
