# C# + Forge port — status and setup (handoff)

Read this first on a new machine or in a new session. The decisions live in the spec; this note says where we are and how to get running.

## Where we are

Spec: `docs/superpowers/specs/2026-10-14-csharp-forge-port-design.md` (binding). **The port is complete:** slice 5 ended with the cutover (spec 7.2), so the C# tree now lives at the root (`src/`, `data/`, `tests/`, `tools/`) and the GDScript tree, `godot_gas/` and `addons/GodotGAS` are deleted (see git history before `chore: cut over to the C# tree`).

| Slice | Contents | Plan | State |
|---|---|---|---|
| 0 | scaffold: csproj, Forge 0.4.0 vendored, C# test runner, layer test, `Core/` | `plans/2026-10-14-csharp-port-slices-0-1.md` | done, on `main` |
| 1 | Forge fit gate (attributes, damage, passives, abilities, cleanup) — **GO** | same plan; report `notes/2026-10-14-slice1-forge-gate.md` | done |
| 2a | Defs + Components | `plans/2026-10-14-csharp-port-slice-2a-defs-components.md` | done |
| 2b | Actors (Building, Projectile, Pickup, Enemy, Player) + Volley/Dash + AutoCaster | `plans/2026-10-14-csharp-port-slice-2b-actors.md` | done |
| 2c | Stage (`StageRoot`) + HUD + dev stage scene + first `port/data` `.tres` + swarm benchmark | `plans/2026-10-14-csharp-port-slice-2c-stage-hud.md` | done |
| 3 | shop: `AbilityDef`, `GradeTable`, `Loadout`, `GradeService`, `AbilityInstaller`, Blade Sweep + `AreaStrike`, Bounty, `AbilityBar`, shop data | `plans/2026-10-14-csharp-port-slice-3-shop.md` | done |
| 4 | events (`StageEvent`, `EventDirector`, statue/meteor/elite) + boss (`BossActor`, `BossBrain`, `BossDirector`, four patterns) + HUD banner/arrows/boss bar | `plans/2026-10-14-csharp-port-slice-4-events-boss.md` | done |
| 5 | meta (logo, menu, hub, save, gem, skill tree, settings) + cutover | `plans/2026-10-14-csharp-port-slice-5-meta-cutover.md` | done |

Test totals: before the cutover GDScript 648 and C# 737 passed side by side; after it the single C# suite has 734 (`tools/run_tests.sh`; the three `GlobalClassCollisionTest` tests went with the GDScript tree).

Play: `"$GODOT_BIN" --path .` (main scene `res://src/App/logo.tscn`: logo -> menu -> hub -> stage; in a run click a slot to buy/promote, Ctrl+click to reroll; after the result Enter returns to the hub, R restarts). The cutover was checked with a headless driver through the real autoloads (one failed and one cleared run, gem paid, stage_02 unlocked, save written); a hands-on play-through by the owner is still worth doing.

Swarm benchmark (`"$GODOT_BIN" --path . res://tools/swarm_bench.tscn`, add `--headless` for the physics cost only), 2026-10-14 on the owner's machine:

| Enemies | physics ms (headless / windowed) | process ms | windowed FPS |
|---|---|---|---|
| 100 | 4.93 / 4.41 | 1.0-1.9 | 2069 (vsync off) |
| 300 | 17.63 / 18.91 | 1.6-2.7 | 872 (vsync off) |

100 enemies fit easily (GDScript Phase 1: 145 FPS). 300 enemies exceed the 16.6 ms budget of a 60 Hz physics step, so the full wave load will slow the game. Profiling (slice 2c follow-up) put ~5.7 ms of the 18 in `ActorVisual.SetMoving/Face` called every physics step per enemy and ~1.3 ms in Forge; caching the visual state (only touch the sprite when it changes) is the cheapest fix and is still open.

## How we work (so a new session continues the same way)

- **This project is the owner's textbook for learning Godot (spec section 1).** Prefer a few well-named concepts over clever abstraction, explain the *why* in doc comments, and after every slice update the spec's section 9 table ("what this teaches") with the lessons the slice added.

- Per slice: brainstorm is done (spec); write a plan (`superpowers:writing-plans`), execute it task by task in the main session (`superpowers:executing-plans`, TDD: watch every test fail first), then a fresh-context final review on the most capable model, fix Critical/Important with a test that fails first, merge to `main` and push. Minor review findings are listed as "deferred" in the final message, not fixed.
- Work on a branch per piece of work; never stage the owner's own uncommitted edits; `git add` explicit paths only.
- Decisions still open for later slices (from reviews): `AutoCaster` activate/end churn when nothing is in range (slice 3), `AbilityContext.Wallet` nullability for the boss (slice 4), per-slot cooldowns for two slots holding the same attack (slice 3). See the spec's slice notes.
- Commit the generated `.cs.uid` files before merging (run the tests once more first, Godot creates them on import).

## Setting up another machine

1. Clone the repo. Install the **.NET SDK 8+** (the project targets `net8.0`; `RollForward=Major` lets it run on newer runtimes).
2. Download the **.NET build of Godot 4.7.2** (`Godot_v4.7.2-stable_mono_win64`) from https://github.com/godotengine/godot-builds/releases and unzip it anywhere. The standard build cannot load the C# autoload.
3. Tell the scripts where it is: `export GODOT_BIN=/path/to/Godot_v4.7.2-stable_mono_win64_console.exe` (the default in `tools/run_tests.sh` is `/d/CM/Godot/Godot_v4.7.2-stable_mono_win64/...`).
4. First run: `IMPORT=1 tools/run_tests.sh` (builds, imports, runs the suite). Expect 734 passing.
5. Open the project with the .NET editor. The editor may stay open while you build and run the tests.

## Not in git

Session memory, review reports and execution ledgers (`.superpowers/`, git-ignored and deleted after each plan), the Godot binaries, and the owner's uncommitted local edits.
