# C# + Forge port — status and setup (handoff)

Read this first on a new machine or in a new session. The decisions live in the spec; this note says where we are and how to get running.

## Where we are

Spec: `docs/superpowers/specs/2026-10-14-csharp-forge-port-design.md` (binding). The C# tree grows under `port/` next to the untouched GDScript tree, which stays the regression reference until the cutover (spec 7.2).

| Slice | Contents | Plan | State |
|---|---|---|---|
| 0 | scaffold: csproj, Forge 0.4.0 vendored, C# test runner, layer test, `Core/` | `plans/2026-10-14-csharp-port-slices-0-1.md` | done, on `main` |
| 1 | Forge fit gate (attributes, damage, passives, abilities, cleanup) — **GO** | same plan; report `notes/2026-10-14-slice1-forge-gate.md` | done |
| 2a | Defs + Components | `plans/2026-10-14-csharp-port-slice-2a-defs-components.md` | done |
| 2b | Actors (Building, Projectile, Pickup, Enemy, Player) + Volley/Dash + AutoCaster | `plans/2026-10-14-csharp-port-slice-2b-actors.md` | done |
| 2c | Stage + HUD + dev stage scene + first `port/data` `.tres` + 100-enemy FPS check | **no plan yet** | **next** |
| 3-5 | shop/abilities, events + boss, meta + cutover | not planned | later |

Test totals at the end of 2b: GDScript 648, C# 215 (`tools/run_tests.sh`).

## How we work (so a new session continues the same way)

- Per slice: brainstorm is done (spec); write a plan (`superpowers:writing-plans`), execute it task by task in the main session (`superpowers:executing-plans`, TDD: watch every test fail first), then a fresh-context final review on the most capable model, fix Critical/Important with a test that fails first, merge to `main` and push. Minor review findings are listed as "deferred" in the final message, not fixed.
- Work on a branch `port/<slice>`; never stage the owner's own uncommitted edits (`src/app/logo.gd`, `src/app/main.gd`); `git add` explicit paths only.
- Decisions still open for later slices (from reviews): `AutoCaster` activate/end churn when nothing is in range (slice 3), `AbilityContext.Wallet` nullability for the boss (slice 4), per-slot cooldowns for two slots holding the same attack (slice 3). See the spec's slice notes.
- Port classes must not be `[GlobalClass]` while a GDScript `class_name` of the same name exists (`GlobalClassCollisionTest`).
- Commit the generated `.cs.uid` files before merging (run the tests once more first, Godot creates them on import).

## Setting up another machine

1. Clone the repo. Install the **.NET SDK 8+** (the project targets `net8.0`; `RollForward=Major` lets it run on newer runtimes).
2. Download the **.NET build of Godot 4.7.2** (`Godot_v4.7.2-stable_mono_win64`) from https://github.com/godotengine/godot-builds/releases and unzip it anywhere. The standard build cannot load the C# autoload.
3. Tell the scripts where it is: `export GODOT_BIN=/path/to/Godot_v4.7.2-stable_mono_win64_console.exe` (the default in `tools/run_tests.sh` is `/d/CM/Godot/Godot_v4.7.2-stable_mono_win64/...`).
4. First run: `IMPORT=1 tools/run_tests.sh` (builds, imports, runs both suites). Expect GDScript 648 and C# 215 passing.
5. Open the project with the .NET editor. The editor may stay open while you build and run the tests.

## Not in git

Session memory, review reports and execution ledgers (`.superpowers/`, git-ignored and deleted after each plan), the Godot binaries, and the owner's uncommitted local edits.
