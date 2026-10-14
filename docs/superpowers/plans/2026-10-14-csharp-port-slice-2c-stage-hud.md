# C# + Forge Port — Slice 2c (Stage, HUD, dev scene, first data) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close slice 2 of the port: the non-Phase-3 stage (`StageRoot`, `Arena`, `RunState`, `StageClock`, `StageFlow`, `EnemyFactory`, `SpawnDirector`, `WaveDirector`, `LootDirector`), `StageDef` + `RunResult`, a basic HUD (health bars, wallet, timeline, result), the world health bar, the first `port/data/*.tres`, a playable dev stage scene with R-restart, and a measured 100-enemy frame budget.

**Architecture:** `StageRoot` (the composition root; see the naming note below) owns the arena, the world layers and the systems as scene children referenced through `[Export]` properties, builds everything in `Start(StageDef, PlayerProfile, seed)`, wires signals, and reports a `RunResult` through a C# event. Directors subscribe to `StageClock.Ticked`; nothing looks anything up. The HUD only reads models it is bound to. `App/DevStage` plays the role the GDScript `Main` had in Phase 1 (stage + HUD + building bar + R restart) until `Main` is ported in slice 5. Every enemy death raises `event.kill` (payload: the enemy) on the player's entity through a new `ActorEntity.Raise`.

**Tech Stack:** Godot 4.7.2 .NET, C# net8.0, Forge 0.4.0 behind `Gas/` and `Components/ActorEntity`.

**Spec:** `docs/superpowers/specs/2026-10-14-csharp-forge-port-design.md` (sections 3, 4, 5, 6, 7.3 slice 2, 8.12). Previous plans: `...-slices-0-1.md`, `...-slice-2a-defs-components.md`, `...-slice-2b-actors.md`. GDScript reference: `src/stage/`, `src/ui/hud/hud.gd`, `src/ui/hud/timeline_bar.gd`, `src/ui/widgets/world_health_bar.gd`, `src/app/main.gd`, and their tests under `tests/`.

## Global Constraints

- Work on a new branch `port/slice-2c` cut from an up-to-date `main`. Never stage `src/app/logo.gd` or `src/app/main.gd` (owner work, if present); always `git add` explicit paths.
- Commit messages end with a second `-m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"`.
- Tests run through `tools/run_tests.sh` (`RUNNER=cs` for the C# suite, `FILTER=<substring>` for one file). `GODOT_BIN` is set by `.claude/settings.local.json`; in a plain shell export it first: `export GODOT_BIN="/c/Users/wo248/OneDrive/바탕 화면/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"`. After adding a scene, a `.tres`, or a script a scene references, run once with `IMPORT=1`. The default run (both suites) must keep GDScript at **648**.
- Layer order (a file may only `using` layers below it): `Core -> Defs -> Gas -> Components -> Actors -> Stage | Abilities | Boss -> Events -> Ui -> App | Meta`. Only `Stage/StageRoot.cs` may later use `Abilities`/`Boss`. Project namespaces are never written fully qualified. Namespace = **top-level** folder (`port/src/Ui/Hud/Hud.cs` is `namespace VampireSurvivor.Ui;`, `port/src/Ui/Widgets/WorldHealthBar.cs` too).
- **Naming:** the composition root class is `StageRoot` (file `StageRoot.cs`, scene still `stage.tscn`), because a class `Stage` inside namespace `VampireSurvivor.Stage` cannot be referenced from other `VampireSurvivor.*` namespaces (the name resolves to the namespace, CS0118). Add this to the spec's slice notes (Task 14).
- **One global class namespace:** no new class carries `[GlobalClass]` (`StageDef`, `RunResult`, `Arena`, `RunState`, `StageClock`, `StageFlow`, `EnemyFactory`, `SpawnDirector`, `WaveDirector`, `LootDirector`, `Hud`, `TimelineBar`, `WorldHealthBar` all share names with GDScript `class_name`s). Scenes and `.tres` reference scripts by path.
- Godot C# rules: a class extending a Godot type is `partial`; file name = class name; `[Signal]` delegates end in `EventHandler`; payloads that are not Variant-compatible (`RunResult`, `DropRoll`, enums) use C# `event`s. Subscribe Godot signals with **method groups or lambdas that capture only `this`**, so Godot disconnects them when the receiver is freed; unsubscribe plain C# events (`Wallet.Changed`) in `_ExitTree`.
- Scenes are hand-written text `.tscn`; a node-typed `[Export]` needs `node_paths=PackedStringArray(...)` in the node header and `Name = NodePath("...")` in its body. `.tres` files reference C# scripts by path, write exported properties by their **C# names** (`MaxHealth = 40.0`), enums as ints, and curves as `Curve` sub-resources with `max_value` set before `_data`.
- Damage only through `DamageEffects.Hit`. Rounding that GDScript did with `roundi` uses `Math.Round(x, MidpointRounding.AwayFromZero)`.
- Validation returns `List<string>`. No balance numbers in code except the constants the GDScript classes already had (`PLAYER_START`, `EDGE_MARGIN`, `WALL_THICKNESS`, `BUILDING_BAR_OFFSET`).
- Expected C# totals assume the suite starts at **215** tests.

## Review Focus

1. Restarting a run must leave nothing of the old run: its enemies leave the `enemies` group at once (so the new player's auto-aim never picks them), and the freed HUD stops listening to the old wallet (Tasks 11, 13).
2. When the building (the spawn anchor) is gone, the trickle and the waves must skip spawning without throwing (Tasks 6, 7).
3. A drop multiplier below 1 must still pay at least 1, and halves round away from zero as in GDScript (Task 8).
4. Once the run has ended the world must stop: enemies and the player stop moving, the clock stops, and later deaths or time-up do not report a second result (Task 9).
5. `trickle_cap = 0` (allowed by validation) must spawn nothing rather than one enemy per tick (Task 6).

---

## File Structure

```
port/src/Components/ActorEntity.cs                 (T1, + Raise)
port/src/Actors/Enemy/Enemy.cs                     (T1, Setup target nullable)
port/src/Defs/{StageDef,RunResult}.cs              (T2)
port/src/Stage/{StageFlow,RunState,StageClock}.cs  (T3)
port/src/Stage/{Arena.cs,arena.tscn}               (T4)
port/src/Stage/EnemyFactory.cs                     (T5)
port/src/Stage/SpawnDirector.cs                    (T6)
port/src/Stage/WaveDirector.cs                     (T7)
port/src/Stage/LootDirector.cs                     (T8)
port/src/Stage/{StageRoot.cs,stage.tscn}           (T9)
port/src/Ui/Hud/TimelineBar.cs, port/src/Ui/Widgets/WorldHealthBar.cs   (T10)
port/src/Ui/Hud/{Hud.cs,hud.tscn}                  (T11)
port/data/**.tres                                  (T12)
port/src/App/{DevStage.cs,dev_stage.tscn}          (T13)
port/tools/{SwarmBench.cs,swarm_bench.tscn}        (T14)
port/tests/Support/{ActorFixture,DefFactory}.cs    (extended)
port/tests/{Components,Actors,Defs,Stage,Ui,Data,App}/...
docs/superpowers/notes/2026-10-14-port-status-and-setup.md, spec slice notes (T14)
```

---

### Task 1: `ActorEntity.Raise`, nullable enemy target, kill helper

**Files:**
- Modify: `port/src/Components/ActorEntity.cs`, `port/src/Actors/Enemy/Enemy.cs`, `port/tests/Support/ActorFixture.cs`
- Test: `port/tests/Components/ActorEntityTest.cs` (add 1), `port/tests/Actors/EnemyTest.cs` (add 1)

**Interfaces:**
- Produces: `ActorEntity.Raise<T>(string eventTag, T payload)` — raises a Forge `EventData<T>` with `EventTags = GameplayTags.ContainerOf(eventTag)`, `Source = Target = this`, `Payload = payload` on `Events`. `Enemy.Setup(EnemyDef def, Node2D? target, RunRng rng)`. Tests: `ActorFixture.Kill(TestCase test, ActorEntity victim)`.

- [ ] **Step 1: Write the failing tests**

Add to `port/tests/Components/ActorEntityTest.cs` (add `using System.Collections.Generic;`, `using Gamesmiths.Forge.Events;`, `using Godot;` if missing):

```csharp
	public void TestRaiseDeliversTheEventAndItsPayloadToSubscribers()
	{
		ActorEntity entity = ActorFixture.BareActor(this, new StatBlock());
		var payload = Adopt(new Node2D());
		var seen = new List<Node>();
		entity.Events.Subscribe<Node>(
			GameplayTags.Request(GameplayTags.EventKill), data => seen.Add(data.Payload), 0);
		entity.Raise<Node>(GameplayTags.EventKill, payload);
		Assert.Eq(seen.Count, 1);
		Assert.True(seen[0] == payload, "the payload arrives unchanged");
	}
```

Add to `port/tests/Actors/EnemyTest.cs`:

```csharp
	public async Task TestEnemyWithoutATargetStandsStill()
	{
		Enemy enemy = Adopt(GD.Load<PackedScene>(EnemyScene).Instantiate<Enemy>());
		enemy.GlobalPosition = new Vector2(40f, 0f);
		enemy.Setup(MakeDef(), null, new RunRng(5));
		await WaitSeconds(0.2);
		Assert.AlmostEq(enemy.GlobalPosition.X, 40.0, 0.01, "no target: no movement and no exception");
	}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `RUNNER=cs FILTER=ActorEntityTest tools/run_tests.sh` and `RUNNER=cs FILTER=EnemyTest tools/run_tests.sh`
Expected: build fails — `ActorEntity` has no `Raise`; `Setup` does not accept `null` for a non-nullable parameter (nullable warnings are errors).

- [ ] **Step 3: Implement**

In `port/src/Components/ActorEntity.cs` add `using Gamesmiths.Forge.Events;` and, after `TryActivate`:

```csharp
	/// <summary>
	/// Raises a gameplay event on this entity (for example <c>event.kill</c> with the dead enemy as payload). Abilities
	/// triggered by the event tag react through Forge; nothing happens when none listens.
	/// </summary>
	public void Raise<T>(string eventTag, T payload)
	{
		var data = new EventData<T>
		{
			EventTags = GameplayTags.ContainerOf(eventTag),
			Source = this,
			Target = this,
			Payload = payload,
		};
		Events.Raise(in data);
	}
```

In `port/src/Actors/Enemy/Enemy.cs` change the signature to `public void Setup(EnemyDef def, Node2D? target, RunRng rng)` (the body is unchanged; `_PhysicsProcess` already handles an invalid target).

In `port/tests/Support/ActorFixture.cs` add `using VampireSurvivor.Gas;` and:

```csharp
	/// <summary>Deals far more damage than any test actor has health, from a fresh bare attacker.</summary>
	public static void Kill(TestCase test, ActorEntity victim)
	{
		DamageEffects.Hit(BareActor(test, new StatBlock()), victim, 1_000_000f);
	}
```

- [ ] **Step 4: Run them to verify they pass**

Run: `RUNNER=cs tools/run_tests.sh`
Expected: `217 passed, 0 failed`.

- [ ] **Step 5: Commit**

```bash
git add port/src/Components/ActorEntity.cs port/src/Actors/Enemy/Enemy.cs port/tests/Support/ActorFixture.cs port/tests/Components/ActorEntityTest.cs port/tests/Actors/EnemyTest.cs
git commit -m "feat(components): ActorEntity.Raise for gameplay events; enemies accept no target" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `StageDef` and `RunResult`

**Files:**
- Create: `port/src/Defs/StageDef.cs`, `port/src/Defs/RunResult.cs`
- Modify: `port/tests/Support/DefFactory.cs`
- Test: `port/tests/Defs/StageDefTest.cs`

**Interfaces:**
- Produces: `enum RunOutcome { Cleared, Failed }`; `sealed class RunResult { RunOutcome Outcome; float Elapsed; int Kills, Gold, Stone }` (init-only). `partial class StageDef : Resource` with exports `Id` (StringName), `DisplayName`, `Duration` (180), `ArenaSize` (2400x1600), `GroundTexture`, `BuildingTexture`, `BuildingStats`, `TrickleEnemies` (`Godot.Collections.Array<EnemyDef>`), `TrickleIntervalStart` (2), `TrickleIntervalEnd` (0.8), `TrickleCap` (60), `SpawnRadius` (520), `Waves` (`WaveSchedule?`), `StartGold`, `StartStone`, `DropMultiplier` (1); `List<string> Validate()`. Grade table, events, boss and gem reward are added by slices 3-5. Tests: `DefFactory.Stage(float duration = 10f)`.

- [ ] **Step 1: Write the failing tests**

Add to `port/tests/Support/DefFactory.cs`:

```csharp
	public static StageDef Stage(float duration = 10f) => new()
	{
		Id = "test_stage",
		Duration = duration,
		GroundTexture = Texture(32, 32),
		BuildingTexture = Texture(64, 64),
		BuildingStats = new StatBlock(),
		Waves = new WaveSchedule(),
	};
```

Create `port/tests/Defs/StageDefTest.cs`:

```csharp
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class StageDefTest : TestCase
{
	public void TestFactoryStageIsValidEvenWithoutTrickleEnemies()
	{
		Assert.NoErrors(DefFactory.Stage().Validate(), "an empty trickle list is allowed");
	}

	public void TestStageCollectsErrorsFromNestedDefs()
	{
		StageDef def = DefFactory.Stage();
		def.BuildingStats!.MaxHealth = 0f;
		EnemyDef broken = DefFactory.Enemy();
		broken.Stats = null;
		def.TrickleEnemies.Add(broken);
		Assert.True(def.Validate().Count >= 2, string.Join("; ", def.Validate()));
	}

	public void TestStageRequiresPositiveDurationAndTextures()
	{
		StageDef def = DefFactory.Stage(0f);
		def.GroundTexture = null;
		Assert.Eq(def.Validate().Count, 2, string.Join("; ", def.Validate()));
	}

	public void TestStageValidatesWavesAgainstItsOwnDuration()
	{
		StageDef def = DefFactory.Stage(60f);
		def.Waves!.Entries.Add(new WaveEntry { Time = 90f, Count = 3, Enemy = DefFactory.Enemy() });
		Assert.Eq(def.Validate().Count, 1, string.Join("; ", def.Validate()));
	}

	public void TestStageRequiresASaneEconomy()
	{
		StageDef def = DefFactory.Stage();
		def.StartGold = -1;
		def.DropMultiplier = 0f;
		def.TrickleCap = -1;
		Assert.Eq(def.Validate().Count, 3, string.Join("; ", def.Validate()));
	}
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `RUNNER=cs FILTER=StageDefTest tools/run_tests.sh`
Expected: build fails — `StageDef` does not exist.

- [ ] **Step 3: Implement**

Create `port/src/Defs/RunResult.cs`:

```csharp
namespace VampireSurvivor.Defs;

public enum RunOutcome
{
	Cleared,
	Failed,
}

/// <summary>What a finished stage run reports to its caller.</summary>
public sealed class RunResult
{
	public RunOutcome Outcome { get; init; } = RunOutcome.Failed;

	public float Elapsed { get; init; }

	public int Kills { get; init; }

	public int Gold { get; init; }

	public int Stone { get; init; }
}
```

Create `port/src/Defs/StageDef.cs`:

```csharp
using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.Defs;

/// <summary>
/// Static description of one stage. The grade table (slice 3), events and boss (slice 4) and the gem reward (slice 5) are
/// added by later slices.
/// </summary>
public partial class StageDef : Resource
{
	[Export]
	public StringName Id { get; set; } = string.Empty;

	[Export]
	public string DisplayName { get; set; } = string.Empty;

	[Export]
	public float Duration { get; set; } = 180f;

	[Export]
	public Vector2 ArenaSize { get; set; } = new(2400f, 1600f);

	[Export]
	public Texture2D? GroundTexture { get; set; }

	[Export]
	public Texture2D? BuildingTexture { get; set; }

	[Export]
	public StatBlock? BuildingStats { get; set; }

	[Export]
	public Godot.Collections.Array<EnemyDef> TrickleEnemies { get; set; } = [];

	[Export]
	public float TrickleIntervalStart { get; set; } = 2f;

	[Export]
	public float TrickleIntervalEnd { get; set; } = 0.8f;

	[Export]
	public int TrickleCap { get; set; } = 60;

	[Export]
	public float SpawnRadius { get; set; } = 520f;

	[Export]
	public WaveSchedule? Waves { get; set; }

	[Export]
	public int StartGold { get; set; }

	[Export]
	public int StartStone { get; set; }

	/// <summary>Scales every enemy drop; 1.0 for balanced play, very large for prototype tuning.</summary>
	[Export]
	public float DropMultiplier { get; set; } = 1f;

	public List<string> Validate()
	{
		var errors = new List<string>();
		string id = Id.ToString();
		if (Duration <= 0f)
		{
			errors.Add($"StageDef '{id}': duration must be > 0");
		}

		if (ArenaSize.X <= 0f || ArenaSize.Y <= 0f || SpawnRadius <= 0f)
		{
			errors.Add($"StageDef '{id}': arena_size and spawn_radius must be > 0");
		}

		if (TrickleIntervalStart <= 0f || TrickleIntervalEnd <= 0f || TrickleCap < 0)
		{
			errors.Add($"StageDef '{id}': trickle intervals must be > 0 and trickle_cap >= 0");
		}

		if (GroundTexture is null || BuildingTexture is null)
		{
			errors.Add($"StageDef '{id}': ground_texture and building_texture are required");
		}

		if (BuildingStats is null)
		{
			errors.Add($"StageDef '{id}': building_stats is null");
		}
		else
		{
			errors.AddRange(BuildingStats.Validate());
		}

		foreach (EnemyDef? enemy in TrickleEnemies)
		{
			if (enemy is null)
			{
				errors.Add($"StageDef '{id}': null trickle enemy");
			}
			else
			{
				errors.AddRange(enemy.Validate());
			}
		}

		if (Waves is not null)
		{
			errors.AddRange(Waves.ValidateAgainst(Duration));
		}

		if (StartGold < 0 || StartStone < 0)
		{
			errors.Add($"StageDef '{id}': start_gold and start_stone must be >= 0");
		}

		if (DropMultiplier <= 0f)
		{
			errors.Add($"StageDef '{id}': drop_multiplier must be > 0");
		}

		return errors;
	}
}
```

- [ ] **Step 4: Run them to verify they pass**

Run: `RUNNER=cs tools/run_tests.sh`
Expected: `222 passed, 0 failed` (`GlobalClassCollisionTest` stays green: no `[GlobalClass]`).

- [ ] **Step 5: Commit**

```bash
git add port/src/Defs/StageDef.cs port/src/Defs/RunResult.cs port/tests/Support/DefFactory.cs port/tests/Defs/StageDefTest.cs
git commit -m "feat(defs): StageDef and RunResult" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `StageFlow`, `RunState`, `StageClock`

**Files:**
- Create: `port/src/Stage/StageFlow.cs`, `port/src/Stage/RunState.cs`, `port/src/Stage/StageClock.cs`
- Test: `port/tests/Stage/StageFlowTest.cs`, `port/tests/Stage/RunStateTest.cs`, `port/tests/Stage/StageClockTest.cs`

**Interfaces:**
- Produces:
  - `enum StageState { Prep, Running, Boss, Cleared, Failed }`; `sealed class StageFlow { event Action<StageState>? StateChanged; StageState State; bool TransitionTo(StageState); bool IsTerminal }`.
  - `sealed class RunState(StageDef stageDef, PlayerProfile profile, long runSeed = 0) { StageDef StageDef; PlayerProfile Profile; RunRng Rng; Wallet Wallet; int Kills (get; set); RunResult ToResult(RunOutcome, float elapsed) }` — the wallet starts with `StartGold`/`StartStone`.
  - `partial class StageClock : Node { [Signal] Ticked(float elapsed); [Signal] Ended(); float Elapsed; float Duration; bool IsRunning; bool HasEnded; void Start(float); void Stop(); void Step(float delta) }` — ticks in `_PhysicsProcess`; `Ended` fires once when `Elapsed >= Duration`, then it keeps ticking (overtime) until stopped.

- [ ] **Step 1: Write the failing tests**

Create `port/tests/Stage/StageFlowTest.cs`:

```csharp
using System.Collections.Generic;
using VampireSurvivor.Stage;

namespace VampireSurvivor.Tests.Stage;

public class StageFlowTest : TestCase
{
	public void TestNormalProgressionAndEvent()
	{
		var flow = new StageFlow();
		var seen = new List<StageState>();
		flow.StateChanged += seen.Add;
		Assert.True(flow.TransitionTo(StageState.Running));
		Assert.True(flow.TransitionTo(StageState.Cleared));
		Assert.SequenceEq(seen, new[] { StageState.Running, StageState.Cleared });
		Assert.True(flow.IsTerminal);
	}

	public void TestIllegalTransitionsAreRefused()
	{
		var flow = new StageFlow();
		Assert.False(flow.TransitionTo(StageState.Cleared), "cannot clear before running");
		flow.TransitionTo(StageState.Running);
		flow.TransitionTo(StageState.Failed);
		Assert.False(flow.TransitionTo(StageState.Running), "terminal states are final");
		Assert.False(flow.TransitionTo(StageState.Cleared));
		Assert.Eq(flow.State, StageState.Failed);
	}

	public void TestBossStateSitsBetweenRunningAndTheEnd()
	{
		var flow = new StageFlow();
		flow.TransitionTo(StageState.Running);
		Assert.True(flow.TransitionTo(StageState.Boss));
		Assert.True(flow.TransitionTo(StageState.Cleared));
	}
}
```

Create `port/tests/Stage/RunStateTest.cs`:

```csharp
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Stage;

public class RunStateTest : TestCase
{
	public void TestNewRunStartsClean()
	{
		var state = new RunState(DefFactory.Stage(), new PlayerProfile(), 7);
		Assert.Eq(state.Wallet.Gold, 0);
		Assert.Eq(state.Wallet.Stone, 0);
		Assert.Eq(state.Kills, 0);
		Assert.Eq(state.Rng.SeedValue, 7L);
	}

	public void TestToResultReportsTheRun()
	{
		var state = new RunState(DefFactory.Stage(), new PlayerProfile());
		state.Wallet.Add(CurrencyKind.Gold, 12);
		state.Wallet.Add(CurrencyKind.Stone, 3);
		state.Kills = 9;
		RunResult result = state.ToResult(RunOutcome.Cleared, 42f);
		Assert.Eq(result.Outcome, RunOutcome.Cleared);
		Assert.Eq(result.Gold, 12);
		Assert.Eq(result.Stone, 3);
		Assert.Eq(result.Kills, 9);
		Assert.AlmostEq(result.Elapsed, 42.0);
	}

	public void TestWalletIsSeededFromTheStageDef()
	{
		StageDef def = DefFactory.Stage();
		def.StartGold = 200;
		def.StartStone = 3;
		var state = new RunState(def, new PlayerProfile(), 7);
		Assert.Eq(state.Wallet.Gold, 200);
		Assert.Eq(state.Wallet.Stone, 3);
	}
}
```

Create `port/tests/Stage/StageClockTest.cs`:

```csharp
using System.Collections.Generic;
using VampireSurvivor.Stage;

namespace VampireSurvivor.Tests.Stage;

public class StageClockTest : TestCase
{
	private StageClock MakeClock()
	{
		StageClock clock = Adopt(new StageClock());
		clock.SetPhysicsProcess(false);
		return clock;
	}

	public void TestTicksReportElapsedTime()
	{
		StageClock clock = MakeClock();
		var seen = new List<float>();
		clock.Ticked += seen.Add;
		clock.Start(10f);
		clock.Step(1.5f);
		clock.Step(0.5f);
		Assert.SequenceEq(seen, new[] { 1.5f, 2f });
	}

	public void TestEndedFiresOnceAndTheClockKeepsTickingIntoOvertime()
	{
		StageClock clock = MakeClock();
		int ended = 0;
		var seen = new List<float>();
		clock.Ended += () => ended++;
		clock.Ticked += seen.Add;
		clock.Start(2f);
		clock.Step(1f);
		Assert.False(clock.HasEnded);
		clock.Step(4f);
		Assert.Eq(ended, 1);
		Assert.True(clock.HasEnded);
		Assert.True(clock.IsRunning, "overtime: the boss fight runs on the same clock");
		clock.Step(1.5f);
		Assert.Eq(ended, 1, "ended fires once");
		Assert.AlmostEq(clock.Elapsed, 6.5);
		Assert.SequenceEq(seen, new[] { 1f, 5f, 6.5f });
	}

	public void TestStoppedClockDoesNotTick()
	{
		StageClock clock = MakeClock();
		var seen = new List<float>();
		clock.Ticked += seen.Add;
		clock.Start(10f);
		clock.Stop();
		clock.Step(1f);
		Assert.Eq(seen.Count, 0);
	}

	public void TestStartResetsElapsed()
	{
		StageClock clock = MakeClock();
		clock.Start(10f);
		clock.Step(4f);
		clock.Start(10f);
		Assert.AlmostEq(clock.Elapsed, 0.0);
		Assert.True(clock.IsRunning);
	}

	public void TestStartClearsTheEndedState()
	{
		StageClock clock = MakeClock();
		int ended = 0;
		clock.Ended += () => ended++;
		clock.Start(1f);
		clock.Step(2f);
		clock.Start(1f);
		Assert.False(clock.HasEnded);
		clock.Step(1f);
		Assert.Eq(ended, 2, "a restarted clock ends again");
	}
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `RUNNER=cs FILTER=Stage/ tools/run_tests.sh`
Expected: build fails — namespace `VampireSurvivor.Stage` / the three types do not exist.

- [ ] **Step 3: Implement**

Create `port/src/Stage/StageFlow.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace VampireSurvivor.Stage;

public enum StageState
{
	Prep,
	Running,
	Boss,
	Cleared,
	Failed,
}

/// <summary>The stage's lifecycle as an explicit state machine; owns transitions only.</summary>
public sealed class StageFlow
{
	private static readonly Dictionary<StageState, StageState[]> Allowed = new()
	{
		[StageState.Prep] = [StageState.Running],
		[StageState.Running] = [StageState.Boss, StageState.Cleared, StageState.Failed],
		[StageState.Boss] = [StageState.Cleared, StageState.Failed],
	};

	public event Action<StageState>? StateChanged;

	public StageState State { get; private set; } = StageState.Prep;

	public bool IsTerminal => State is StageState.Cleared or StageState.Failed;

	public bool TransitionTo(StageState to)
	{
		if (!Allowed.TryGetValue(State, out StageState[]? next) || Array.IndexOf(next, to) < 0)
		{
			return false;
		}

		State = to;
		StateChanged?.Invoke(State);
		return true;
	}
}
```

Create `port/src/Stage/RunState.cs`:

```csharp
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Stage;

/// <summary>Mutable state of one stage run. Dies with the stage; nothing here is global.</summary>
public sealed class RunState
{
	public RunState(StageDef stageDef, PlayerProfile profile, long runSeed = 0)
	{
		StageDef = stageDef;
		Profile = profile;
		Rng = new RunRng(runSeed);
		Wallet.Add(CurrencyKind.Gold, stageDef.StartGold);
		Wallet.Add(CurrencyKind.Stone, stageDef.StartStone);
	}

	public StageDef StageDef { get; }

	public PlayerProfile Profile { get; }

	public RunRng Rng { get; }

	public Wallet Wallet { get; } = new();

	public int Kills { get; set; }

	public RunResult ToResult(RunOutcome outcome, float elapsed) => new()
	{
		Outcome = outcome,
		Elapsed = elapsed,
		Kills = Kills,
		Gold = Wallet.Gold,
		Stone = Wallet.Stone,
	};
}
```

Create `port/src/Stage/StageClock.cs`:

```csharp
using Godot;

namespace VampireSurvivor.Stage;

/// <summary>
/// Elapsed stage time. Everything time-driven (spawning, waves, HUD) subscribes to <see cref="Ticked"/>. <see cref="Ended"/>
/// fires once when <see cref="Elapsed"/> reaches <see cref="Duration"/>; the clock then keeps ticking (overtime: the boss
/// fight) until stopped, so the trickle continues while waves, scheduled inside the duration, never fire again.
/// </summary>
public partial class StageClock : Node
{
	[Signal]
	public delegate void TickedEventHandler(float elapsed);

	[Signal]
	public delegate void EndedEventHandler();

	public float Elapsed { get; private set; }

	public float Duration { get; private set; }

	public bool IsRunning { get; private set; }

	public bool HasEnded { get; private set; }

	public void Start(float duration)
	{
		Duration = duration;
		Elapsed = 0f;
		IsRunning = true;
		HasEnded = false;
	}

	public void Stop() => IsRunning = false;

	public void Step(float delta)
	{
		if (!IsRunning)
		{
			return;
		}

		Elapsed += delta;
		EmitSignal(SignalName.Ticked, Elapsed);
		if (!HasEnded && Elapsed >= Duration)
		{
			HasEnded = true;
			EmitSignal(SignalName.Ended);
		}
	}

	public override void _PhysicsProcess(double delta) => Step((float)delta);
}
```

- [ ] **Step 4: Run them to verify they pass**

Run: `RUNNER=cs tools/run_tests.sh`
Expected: `233 passed, 0 failed` (`LayerRulesTest` green: `Stage` uses only `Core`/`Defs`).

- [ ] **Step 5: Commit**

```bash
git add port/src/Stage/StageFlow.cs port/src/Stage/RunState.cs port/src/Stage/StageClock.cs port/tests/Stage/StageFlowTest.cs port/tests/Stage/RunStateTest.cs port/tests/Stage/StageClockTest.cs
git commit -m "feat(stage): StageFlow, RunState and StageClock" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `Arena`

**Files:**
- Create: `port/src/Stage/Arena.cs`, `port/src/Stage/arena.tscn`
- Test: `port/tests/Stage/ArenaTest.cs`

**Interfaces:**
- Produces: `partial class Arena : Node2D { const float WallThickness = 200f; [Export] Sprite2D Ground; [Export] StaticBody2D Walls; Rect2 Bounds; void Build(Vector2 size, Texture2D groundTexture) }` — bounds centred on the origin, tiled ground, four wall shapes (rebuilding replaces them).

- [ ] **Step 1: Write the failing tests**

Create `port/tests/Stage/ArenaTest.cs`:

```csharp
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Stage;

public class ArenaTest : TestCase
{
	private Arena MakeArena() => Adopt(GD.Load<PackedScene>("res://port/src/Stage/arena.tscn").Instantiate<Arena>());

	public void TestBuildCentersTheBoundsAndSurroundsThemWithWalls()
	{
		Arena arena = MakeArena();
		arena.Build(new Vector2(400f, 300f), DefFactory.Texture(32, 32));
		Assert.Eq(arena.Bounds, new Rect2(-200f, -150f, 400f, 300f));
		Assert.Eq(arena.Walls.GetChildCount(), 4);
		var centers = new List<Vector2>();
		foreach (Node child in arena.Walls.GetChildren())
		{
			centers.Add(((CollisionShape2D)child).Position);
		}

		Assert.True(centers.Contains(new Vector2(0f, -250f)), "top wall");
		Assert.True(centers.Contains(new Vector2(0f, 250f)), "bottom wall");
		Assert.True(centers.Contains(new Vector2(-300f, 0f)), "left wall");
		Assert.True(centers.Contains(new Vector2(300f, 0f)), "right wall");
	}

	public void TestRebuildingReplacesTheWalls()
	{
		Arena arena = MakeArena();
		arena.Build(new Vector2(400f, 300f), DefFactory.Texture(32, 32));
		arena.Build(new Vector2(800f, 600f), DefFactory.Texture(32, 32));
		Assert.Eq(arena.Walls.GetChildCount(), 4);
		Assert.Eq(arena.Bounds.Size, new Vector2(800f, 600f));
	}
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `RUNNER=cs FILTER=ArenaTest tools/run_tests.sh`
Expected: build fails — `Arena` does not exist.

- [ ] **Step 3: Implement**

Create `port/src/Stage/Arena.cs`:

```csharp
using Godot;

namespace VampireSurvivor.Stage;

/// <summary>The playfield: a tiled ground and four walls around it.</summary>
public partial class Arena : Node2D
{
	public const float WallThickness = 200f;

	[Export]
	public Sprite2D Ground { get; set; } = null!;

	[Export]
	public StaticBody2D Walls { get; set; } = null!;

	public Rect2 Bounds { get; private set; }

	/// <summary>Call after the arena has entered the tree.</summary>
	public void Build(Vector2 size, Texture2D groundTexture)
	{
		Bounds = new Rect2(-size / 2f, size);
		Ground.Texture = groundTexture;
		Ground.TextureRepeat = TextureRepeatEnum.Enabled;
		Ground.RegionEnabled = true;
		Ground.RegionRect = new Rect2(Vector2.Zero, size);
		foreach (Node child in Walls.GetChildren())
		{
			Walls.RemoveChild(child);
			child.Free();
		}

		foreach (Rect2 rect in WallRects())
		{
			var shape = new CollisionShape2D
			{
				Shape = new RectangleShape2D { Size = rect.Size },
				Position = rect.GetCenter(),
			};
			Walls.AddChild(shape);
		}
	}

	private Rect2[] WallRects()
	{
		float t = WallThickness;
		float width = Bounds.Size.X + (2f * t);
		return
		[
			new Rect2(Bounds.Position.X - t, Bounds.Position.Y - t, width, t),
			new Rect2(Bounds.Position.X - t, Bounds.End.Y, width, t),
			new Rect2(Bounds.Position.X - t, Bounds.Position.Y, t, Bounds.Size.Y),
			new Rect2(Bounds.End.X, Bounds.Position.Y, t, Bounds.Size.Y),
		];
	}
}
```

Create `port/src/Stage/arena.tscn`:

```
[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://port/src/Stage/Arena.cs" id="1_arena"]

[node name="Arena" type="Node2D" node_paths=PackedStringArray("Ground", "Walls")]
script = ExtResource("1_arena")
Ground = NodePath("Ground")
Walls = NodePath("Walls")

[node name="Ground" type="Sprite2D" parent="."]
z_index = -10

[node name="Walls" type="StaticBody2D" parent="."]
collision_layer = 1
collision_mask = 0
```

- [ ] **Step 4: Run them to verify they pass**

Run: `IMPORT=1 RUNNER=cs tools/run_tests.sh`
Expected: `235 passed, 0 failed`.

- [ ] **Step 5: Commit**

```bash
git add port/src/Stage/Arena.cs port/src/Stage/arena.tscn port/tests/Stage/ArenaTest.cs
git commit -m "feat(stage): Arena with ground and walls" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

(If Godot created `port/src/Stage/*.cs.uid` files, add them in the same commit.)

---

### Task 5: `EnemyFactory`

**Files:**
- Create: `port/src/Stage/EnemyFactory.cs`
- Test: `port/tests/Stage/EnemyFactoryTest.cs`

**Interfaces:**
- Consumes: `Enemy.Setup(EnemyDef, Node2D?, RunRng)` (Task 1), `TargetProvider.TargetFor(TargetPolicy)`.
- Produces: `partial class EnemyFactory : Node { [Signal] Spawned(Enemy enemy); void Bind(Node2D layer, TargetProvider targets, RunRng rng); Enemy Spawn(EnemyDef def, Vector2 position); int AliveCount }` — the single place a pooling seam would go. Dead or freed enemies no longer count.

- [ ] **Step 1: Write the failing tests**

Create `port/tests/Stage/EnemyFactoryTest.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Stage;

public class EnemyFactoryTest : TestCase
{
	private const string EnemyScene = "res://port/src/Actors/Enemy/enemy.tscn";

	private (EnemyFactory Factory, Node2D Layer) MakeRig()
	{
		Node2D layer = Adopt(new Node2D());
		TargetProvider targets = Adopt(new TargetProvider());
		targets.Building = Adopt(new Node2D { Position = new Vector2(0f, 500f) });
		targets.Player = Adopt(new Node2D { Position = new Vector2(500f, 0f) });
		EnemyFactory factory = Adopt(new EnemyFactory());
		factory.Bind(layer, targets, new RunRng(1));
		return (factory, layer);
	}

	private static EnemyDef MakeDef() => DefFactory.Enemy(GD.Load<PackedScene>(EnemyScene));

	public void TestSpawnPlacesAConfiguredEnemyInTheLayer()
	{
		(EnemyFactory factory, Node2D layer) = MakeRig();
		var spawned = new List<Enemy>();
		factory.Spawned += spawned.Add;
		Enemy enemy = factory.Spawn(MakeDef(), new Vector2(30f, 40f));
		Assert.True(enemy.GetParent() == layer, "added to the enemy layer");
		Assert.Eq(enemy.GlobalPosition, new Vector2(30f, 40f));
		Assert.Eq(spawned.Count, 1);
		Assert.True(spawned[0] == enemy);
		Assert.Eq(factory.AliveCount, 1);
	}

	public void TestDeadEnemiesNoLongerCountAsAlive()
	{
		(EnemyFactory factory, _) = MakeRig();
		Enemy enemy = factory.Spawn(MakeDef(), Vector2.Zero);
		ActorFixture.Kill(this, enemy.Entity);
		Assert.Eq(factory.AliveCount, 0);
	}

	public async Task TestEnemiesChaseTheTargetOfTheirPolicy()
	{
		(EnemyFactory factory, _) = MakeRig();
		EnemyDef def = MakeDef();
		def.TargetPolicy = TargetPolicy.Player;
		Enemy enemy = factory.Spawn(def, Vector2.Zero);
		await WaitSeconds(0.3);
		Assert.True(enemy.GlobalPosition.X > 3f, "moved toward the player on +x");
		Assert.AlmostEq(enemy.GlobalPosition.Y, 0.0, 0.5);
	}
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `RUNNER=cs FILTER=EnemyFactoryTest tools/run_tests.sh`
Expected: build fails — `EnemyFactory` does not exist.

- [ ] **Step 3: Implement**

Create `port/src/Stage/EnemyFactory.cs`:

```csharp
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Stage;

/// <summary>Creates enemies from defs. The single place to add pooling later.</summary>
public partial class EnemyFactory : Node
{
	private readonly List<Enemy> _alive = [];
	private Node2D _layer = null!;
	private TargetProvider _targets = null!;
	private RunRng _rng = null!;

	[Signal]
	public delegate void SpawnedEventHandler(Enemy enemy);

	/// <summary>Enemies this factory created that are still alive (dying enemies no longer count toward spawn caps).</summary>
	public int AliveCount => _alive.Count;

	public void Bind(Node2D layer, TargetProvider targets, RunRng rng)
	{
		_layer = layer;
		_targets = targets;
		_rng = rng;
	}

	public Enemy Spawn(EnemyDef def, Vector2 position)
	{
		var enemy = def.Scene!.Instantiate<Enemy>();
		_layer.AddChild(enemy);
		enemy.GlobalPosition = position;
		enemy.Setup(def, _targets.TargetFor(def.TargetPolicy), _rng);
		_alive.Add(enemy);
		enemy.Died += Forget;
		enemy.TreeExited += () => Forget(enemy);
		EmitSignal(SignalName.Spawned, enemy);
		return enemy;
	}

	private void Forget(Enemy enemy) => _alive.Remove(enemy);
}
```

- [ ] **Step 4: Run them to verify they pass**

Run: `RUNNER=cs tools/run_tests.sh`
Expected: `238 passed, 0 failed`.

- [ ] **Step 5: Commit**

```bash
git add port/src/Stage/EnemyFactory.cs port/tests/Stage/EnemyFactoryTest.cs
git commit -m "feat(stage): EnemyFactory" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: `SpawnDirector` (the trickle)

**Files:**
- Create: `port/src/Stage/SpawnDirector.cs`
- Test: `port/tests/Stage/SpawnDirectorTest.cs`

**Interfaces:**
- Consumes: `StageClock.Ticked`, `EnemyFactory.Spawn/AliveCount`, `SpawnGeometry.RingPoint/ClampToRect`, `RunRng.Pick/Float`.
- Produces: `partial class SpawnDirector : Node { const float EdgeMargin = 24f; void Bind(StageDef, StageClock, EnemyFactory, Node2D anchor, RunRng, Rect2 arenaBounds); float CurrentInterval(float elapsed) }`.

- [ ] **Step 1: Write the failing tests**

Create `port/tests/Stage/SpawnDirectorTest.cs`:

```csharp
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Stage;

public class SpawnDirectorTest : TestCase
{
	private const string EnemyScene = "res://port/src/Actors/Enemy/enemy.tscn";
	private static readonly Rect2 BigArena = new(-5000f, -5000f, 10000f, 10000f);

	private (StageClock Clock, EnemyFactory Factory, SpawnDirector Director, Node2D Anchor) MakeRig(StageDef def)
	{
		Node2D layer = Adopt(new Node2D());
		TargetProvider targets = Adopt(new TargetProvider());
		Node2D anchor = Adopt(new Node2D());
		targets.Building = anchor;
		targets.Player = anchor;
		var rng = new RunRng(2);
		EnemyFactory factory = Adopt(new EnemyFactory());
		factory.Bind(layer, targets, rng);
		StageClock clock = Adopt(new StageClock());
		clock.SetPhysicsProcess(false);
		SpawnDirector director = Adopt(new SpawnDirector());
		director.Bind(def, clock, factory, anchor, rng, BigArena);
		clock.Start(def.Duration);
		return (clock, factory, director, anchor);
	}

	private static StageDef MakeStage(int cap)
	{
		StageDef def = DefFactory.Stage(100f);
		def.TrickleEnemies.Add(DefFactory.Enemy(GD.Load<PackedScene>(EnemyScene)));
		def.TrickleIntervalStart = 1f;
		def.TrickleIntervalEnd = 1f;
		def.TrickleCap = cap;
		def.SpawnRadius = 300f;
		return def;
	}

	public void TestIntervalInterpolatesAcrossTheStage()
	{
		StageDef def = MakeStage(10);
		def.TrickleIntervalStart = 2f;
		def.TrickleIntervalEnd = 0.5f;
		SpawnDirector director = MakeRig(def).Director;
		Assert.AlmostEq(director.CurrentInterval(0f), 2.0);
		Assert.AlmostEq(director.CurrentInterval(50f), 1.25);
		Assert.AlmostEq(director.CurrentInterval(100f), 0.5);
	}

	public void TestSpawnsOnTheRingAtTheConfiguredIntervalAndRespectsTheCap()
	{
		var rig = MakeRig(MakeStage(2));
		rig.Clock.Step(0.1f);
		Assert.Eq(rig.Factory.AliveCount, 1);
		rig.Clock.Step(0.5f);
		Assert.Eq(rig.Factory.AliveCount, 1, "interval not elapsed");
		rig.Clock.Step(1f);
		Assert.Eq(rig.Factory.AliveCount, 2);
		rig.Clock.Step(5f);
		Assert.Eq(rig.Factory.AliveCount, 2, "cap reached");
		foreach (Node node in Host.GetTree().GetNodesInGroup(Groups.Enemies))
		{
			Assert.AlmostEq(((Node2D)node).GlobalPosition.Length(), 300.0, 0.5);
		}
	}

	public void TestEmptyTrickleListSpawnsNothingAndDoesNotFail()
	{
		StageDef def = MakeStage(5);
		def.TrickleEnemies.Clear();
		var rig = MakeRig(def);
		rig.Clock.Step(3f);
		Assert.Eq(rig.Factory.AliveCount, 0);
	}

	public void TestTrickleContinuesInOvertimeAtTheEndInterval()
	{
		StageDef def = MakeStage(10);
		def.Duration = 2f;
		def.TrickleIntervalStart = 3f;
		def.TrickleIntervalEnd = 1f;
		var rig = MakeRig(def);
		Assert.AlmostEq(rig.Director.CurrentInterval(25f), 1.0, 0.001, "clamped to the end interval");
		rig.Clock.Step(2.5f);
		Assert.Eq(rig.Factory.AliveCount, 1);
		rig.Clock.Step(1f);
		Assert.Eq(rig.Factory.AliveCount, 2, "spawns keep coming after the stage time");
		rig.Clock.Step(1f);
		Assert.Eq(rig.Factory.AliveCount, 3);
		Assert.True(rig.Clock.Elapsed > def.Duration);
	}

	public void TestAZeroCapSpawnsNothing()
	{
		var rig = MakeRig(MakeStage(0));
		for (int i = 0; i < 5; i++)
		{
			rig.Clock.Step(1f);
		}

		Assert.Eq(rig.Factory.AliveCount, 0);
	}

	public void TestAFreedAnchorStopsTheTrickleWithoutErrors()
	{
		var rig = MakeRig(MakeStage(10));
		rig.Anchor.Free();
		rig.Clock.Step(0.1f);
		rig.Clock.Step(1f);
		Assert.Eq(rig.Factory.AliveCount, 0, "no building, no ring to spawn on");
	}
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `RUNNER=cs FILTER=SpawnDirectorTest tools/run_tests.sh`
Expected: build fails — `SpawnDirector` does not exist.

- [ ] **Step 3: Implement**

Create `port/src/Stage/SpawnDirector.cs`:

```csharp
using System;
using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Stage;

/// <summary>The gentle background trickle of enemies; its pace ramps across the stage.</summary>
public partial class SpawnDirector : Node
{
	public const float EdgeMargin = 24f;

	private StageDef _stageDef = null!;
	private EnemyFactory _factory = null!;
	private Node2D? _anchor;
	private RunRng _rng = null!;
	private Rect2 _bounds;
	private float _nextSpawnAt;

	public void Bind(StageDef stageDef, StageClock clock, EnemyFactory factory, Node2D anchor, RunRng rng, Rect2 arenaBounds)
	{
		_stageDef = stageDef;
		_factory = factory;
		_anchor = anchor;
		_rng = rng;
		_bounds = arenaBounds;
		clock.Ticked += OnClockTicked;
	}

	/// <summary>Seconds between trickle spawns at <paramref name="elapsed"/>, interpolated across the stage.</summary>
	public float CurrentInterval(float elapsed)
	{
		float progress = Math.Clamp(elapsed / _stageDef.Duration, 0f, 1f);
		return Mathf.Lerp(_stageDef.TrickleIntervalStart, _stageDef.TrickleIntervalEnd, progress);
	}

	private void OnClockTicked(float elapsed)
	{
		if (elapsed < _nextSpawnAt)
		{
			return;
		}

		_nextSpawnAt = elapsed + CurrentInterval(elapsed);
		if (_stageDef.TrickleEnemies.Count == 0 || _factory.AliveCount >= _stageDef.TrickleCap)
		{
			return;
		}

		if (!IsInstanceValid(_anchor))
		{
			return;
		}

		EnemyDef def = _rng.Pick(_stageDef.TrickleEnemies)!;
		Vector2 point = SpawnGeometry.RingPoint(_anchor!.GlobalPosition, _stageDef.SpawnRadius, _rng.Float() * Mathf.Tau);
		_factory.Spawn(def, SpawnGeometry.ClampToRect(point, _bounds, EdgeMargin));
	}
}
```

(If `RunRng.Pick` does not accept `Godot.Collections.Array<EnemyDef>` as `IReadOnlyList<EnemyDef>`, pass `[.. _stageDef.TrickleEnemies]`.)

- [ ] **Step 4: Run them to verify they pass**

Run: `RUNNER=cs tools/run_tests.sh`
Expected: `244 passed, 0 failed`.

- [ ] **Step 5: Commit**

```bash
git add port/src/Stage/SpawnDirector.cs port/tests/Stage/SpawnDirectorTest.cs
git commit -m "feat(stage): SpawnDirector trickle" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: `WaveDirector`

**Files:**
- Create: `port/src/Stage/WaveDirector.cs`
- Test: `port/tests/Stage/WaveDirectorTest.cs`

**Interfaces:**
- Consumes: `WaveSchedule.SortedEntries()/Times()`, `Timeline(duration, marks).Crossed(from, to)`.
- Produces: `partial class WaveDirector : Node { [Signal] WaveStarted(WaveEntry entry); const float EdgeMargin = 24f; Timeline Timeline; void Bind(StageDef, StageClock, EnemyFactory, Node2D anchor, RunRng, Rect2 arenaBounds); float[] MarkTimes() }` — every wave fires exactly once, even across a huge step; never in overtime.

- [ ] **Step 1: Write the failing tests**

Create `port/tests/Stage/WaveDirectorTest.cs`:

```csharp
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Stage;

public class WaveDirectorTest : TestCase
{
	private const string EnemyScene = "res://port/src/Actors/Enemy/enemy.tscn";
	private static readonly Rect2 BigArena = new(-5000f, -5000f, 10000f, 10000f);

	private static WaveEntry Entry(float time, int count, string label) => new()
	{
		Time = time,
		Count = count,
		Label = label,
		Enemy = DefFactory.Enemy(GD.Load<PackedScene>(EnemyScene)),
	};

	private (StageClock Clock, EnemyFactory Factory, WaveDirector Director, Node2D Anchor, List<string> Labels) MakeRig(
		params WaveEntry[] entries)
	{
		StageDef def = DefFactory.Stage(10f);
		def.SpawnRadius = 300f;
		foreach (WaveEntry entry in entries)
		{
			def.Waves!.Entries.Add(entry);
		}

		Node2D layer = Adopt(new Node2D());
		TargetProvider targets = Adopt(new TargetProvider());
		Node2D anchor = Adopt(new Node2D());
		targets.Building = anchor;
		targets.Player = anchor;
		var rng = new RunRng(4);
		EnemyFactory factory = Adopt(new EnemyFactory());
		factory.Bind(layer, targets, rng);
		StageClock clock = Adopt(new StageClock());
		clock.SetPhysicsProcess(false);
		WaveDirector director = Adopt(new WaveDirector());
		director.Bind(def, clock, factory, anchor, rng, BigArena);
		var labels = new List<string>();
		director.WaveStarted += entry => labels.Add(entry.Label);
		clock.Start(def.Duration);
		return (clock, factory, director, anchor, labels);
	}

	public void TestWavesFireInTimeOrderWithTheirEnemyCounts()
	{
		var rig = MakeRig(Entry(3f, 4, "late"), Entry(1f, 3, "early"));
		rig.Clock.Step(1.5f);
		Assert.Eq(rig.Factory.AliveCount, 3);
		rig.Clock.Step(1f);
		Assert.SequenceEq(rig.Labels, new[] { "early" });
		rig.Clock.Step(1f);
		Assert.SequenceEq(rig.Labels, new[] { "early", "late" });
		Assert.Eq(rig.Factory.AliveCount, 7);
	}

	public void TestAHugeTimeStepFiresEveryWaveExactlyOnce()
	{
		var rig = MakeRig(Entry(1f, 2, "a"), Entry(3f, 2, "b"));
		rig.Clock.Step(100f);
		Assert.SequenceEq(rig.Labels, new[] { "a", "b" });
	}

	public void TestAWaveAtTimeZeroStillFires()
	{
		var rig = MakeRig(Entry(0f, 2, "opener"));
		rig.Clock.Step(0.1f);
		Assert.SequenceEq(rig.Labels, new[] { "opener" });
	}

	public void TestMarkTimesAreSortedForTheHud()
	{
		var rig = MakeRig(Entry(3f, 1, "b"), Entry(1f, 1, "a"));
		Assert.SequenceEq(rig.Director.MarkTimes(), new[] { 1f, 3f });
	}

	public void TestWavesNeverFireAgainInOvertime()
	{
		var rig = MakeRig(Entry(1f, 2, "a"), Entry(9.5f, 1, "b"));
		rig.Clock.Step(10.5f);
		Assert.SequenceEq(rig.Labels, new[] { "a", "b" });
		for (int i = 0; i < 5; i++)
		{
			rig.Clock.Step(3f);
		}

		Assert.AlmostEq(rig.Clock.Elapsed, 25.5, 0.001, "the clock ran on past the 10 s stage");
		Assert.SequenceEq(rig.Labels, new[] { "a", "b" }, "no wave re-fires in overtime");
		Assert.Eq(rig.Factory.AliveCount, 3);
	}

	public void TestAWaveWithoutAnAnchorIsAnnouncedButSpawnsNothing()
	{
		var rig = MakeRig(Entry(1f, 3, "orphan"));
		rig.Anchor.Free();
		rig.Clock.Step(2f);
		Assert.SequenceEq(rig.Labels, new[] { "orphan" });
		Assert.Eq(rig.Factory.AliveCount, 0);
	}
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `RUNNER=cs FILTER=WaveDirectorTest tools/run_tests.sh`
Expected: build fails — `WaveDirector` does not exist.

- [ ] **Step 3: Implement**

Create `port/src/Stage/WaveDirector.cs`:

```csharp
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Stage;

/// <summary>Fires the stage's scheduled surges and exposes their times for the timeline bar.</summary>
public partial class WaveDirector : Node
{
	public const float EdgeMargin = 24f;

	private List<WaveEntry> _entries = [];
	private float _previousTime = -1f;
	private EnemyFactory _factory = null!;
	private Node2D? _anchor;
	private RunRng _rng = null!;
	private Rect2 _bounds;
	private float _spawnRadius;

	[Signal]
	public delegate void WaveStartedEventHandler(WaveEntry entry);

	public Timeline Timeline { get; private set; } = new(0f);

	public void Bind(StageDef stageDef, StageClock clock, EnemyFactory factory, Node2D anchor, RunRng rng, Rect2 arenaBounds)
	{
		_factory = factory;
		_anchor = anchor;
		_rng = rng;
		_bounds = arenaBounds;
		_spawnRadius = stageDef.SpawnRadius;
		float[] times = [];
		if (stageDef.Waves is not null)
		{
			_entries = stageDef.Waves.SortedEntries();
			times = stageDef.Waves.Times();
		}

		Timeline = new Timeline(stageDef.Duration, times);
		clock.Ticked += OnClockTicked;
	}

	public float[] MarkTimes() => Timeline.Marks;

	private void OnClockTicked(float elapsed)
	{
		foreach (int index in Timeline.Crossed(_previousTime, elapsed))
		{
			Launch(_entries[index]);
		}

		_previousTime = elapsed;
	}

	private void Launch(WaveEntry entry)
	{
		EmitSignal(SignalName.WaveStarted, entry);
		if (!IsInstanceValid(_anchor))
		{
			return;
		}

		float baseAngle = _rng.Float() * Mathf.Tau;
		for (int i = 0; i < entry.Count; i++)
		{
			float angle = baseAngle + (Mathf.Tau * i / entry.Count);
			Vector2 point = SpawnGeometry.RingPoint(_anchor!.GlobalPosition, _spawnRadius, angle);
			_factory.Spawn(entry.Enemy!, SpawnGeometry.ClampToRect(point, _bounds, EdgeMargin));
		}
	}
}
```

Note: the `-1` start of `_previousTime` makes a wave at time 0 fire on the first tick (marks with `from < mark <= to`).

- [ ] **Step 4: Run them to verify they pass**

Run: `RUNNER=cs tools/run_tests.sh`
Expected: `250 passed, 0 failed`.

- [ ] **Step 5: Commit**

```bash
git add port/src/Stage/WaveDirector.cs port/tests/Stage/WaveDirectorTest.cs
git commit -m "feat(stage): WaveDirector" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: `LootDirector`

**Files:**
- Create: `port/src/Stage/LootDirector.cs`
- Test: `port/tests/Stage/LootDirectorTest.cs`

**Interfaces:**
- Consumes: `EnemyFactory.Spawned`, `Enemy.LootDropped` (C# event, `DropRoll`, `Vector2`), `Player.LootCollected` (C# event), `Player.GainMultiplier(CurrencyKind)`, `Pickup.Setup(DropRoll)`.
- Produces: `partial class LootDirector : Node { const string PickupScene = "res://port/src/Actors/Pickup/pickup.tscn"; void Bind(EnemyFactory, Node2D pickupLayer, Player, Wallet, float dropMultiplier = 1f) }`. Pickups are created deferred (never inside a physics callback); the drop multiplier is applied to the drop (`max(1, round(amount * multiplier))`, halves away from zero); collected loot is credited as `round(amount * gain multiplier)`.

- [ ] **Step 1: Write the failing tests**

Create `port/tests/Stage/LootDirectorTest.cs`:

```csharp
using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Stage;

public class LootDirectorTest : TestCase
{
	private const string EnemyScene = "res://port/src/Actors/Enemy/enemy.tscn";
	private const string PickupScene = "res://port/src/Actors/Pickup/pickup.tscn";

	private sealed record Rig(Player Player, EnemyFactory Factory, Node2D Pickups, Wallet Wallet);

	private Rig MakeRig(float dropMultiplier = 1f)
	{
		var wallet = new Wallet();
		Player player = ActorFixture.Player(this, wallet: wallet);
		TargetProvider targets = Adopt(new TargetProvider { Player = player, Building = player });
		EnemyFactory factory = Adopt(new EnemyFactory());
		factory.Bind(Adopt(new Node2D()), targets, new RunRng(6));
		Node2D pickups = Adopt(new Node2D());
		LootDirector loot = Adopt(new LootDirector());
		loot.Bind(factory, pickups, player, wallet, dropMultiplier);
		return new Rig(player, factory, pickups, wallet);
	}

	private static EnemyDef DroppingDef(int amount)
	{
		EnemyDef def = DefFactory.Enemy(GD.Load<PackedScene>(EnemyScene));
		def.Drops = new DropTable();
		def.Drops.Entries.Add(new DropEntry
		{
			Chance = 1f,
			MinAmount = amount,
			MaxAmount = amount,
			Icon = DefFactory.Texture(8, 8),
		});
		return def;
	}

	private Pickup DropAt(Node2D layer, Vector2 position, CurrencyKind kind, int amount)
	{
		var pickup = GD.Load<PackedScene>(PickupScene).Instantiate<Pickup>();
		layer.AddChild(pickup);
		pickup.GlobalPosition = position;
		pickup.Setup(new DropRoll { Kind = kind, Amount = amount, Icon = DefFactory.Texture(8, 8) });
		return pickup;
	}

	public async Task TestCollectedLootIsCreditedWithTheGainMultiplier()
	{
		Rig rig = MakeRig();
		ActorEntity entity = rig.Player.Entity;
		entity.Apply(StatEffects.SetBase(Stats.GoldGain, entity.Attributes[Stats.GoldGain].ToRawValue(2f)));
		DropAt(rig.Pickups, rig.Player.GlobalPosition, CurrencyKind.Gold, 5);
		DropAt(rig.Pickups, rig.Player.GlobalPosition, CurrencyKind.Stone, 3);
		await WaitSeconds(0.3);
		Assert.Eq(rig.Wallet.Gold, 10);
		Assert.Eq(rig.Wallet.Stone, 3);
	}

	public async Task TestEnemyDropsBecomePickupsOnThePickupLayer()
	{
		Rig rig = MakeRig();
		Enemy enemy = rig.Factory.Spawn(DroppingDef(1), new Vector2(300f, 300f));
		ActorFixture.Kill(this, enemy.Entity);
		Assert.Eq(rig.Pickups.GetChildCount(), 0, "spawned deferred, not inside the death callback");
		await NextFrame();
		await NextFrame();
		Assert.Eq(rig.Pickups.GetChildCount(), 1);
		var pickup = rig.Pickups.GetChild(0) as Pickup;
		Assert.NotNull(pickup);
		Assert.True(pickup!.GlobalPosition.DistanceTo(new Vector2(300f, 300f)) <= 12f, "dropped where the enemy died");
	}

	public async Task TestDropMultiplierScalesPickupAmounts()
	{
		Rig rig = MakeRig(3f);
		Enemy enemy = rig.Factory.Spawn(DroppingDef(2), new Vector2(300f, 300f));
		ActorFixture.Kill(this, enemy.Entity);
		await NextFrame();
		await NextFrame();
		Assert.Eq(((Pickup)rig.Pickups.GetChild(0)).Amount, 6, "2 x 3.0");
	}

	public async Task TestASmallMultiplierStillPaysAtLeastOneAndHalvesRoundUp()
	{
		Rig small = MakeRig(0.1f);
		ActorFixture.Kill(this, small.Factory.Spawn(DroppingDef(2), new Vector2(300f, 300f)).Entity);
		Rig half = MakeRig(0.5f);
		ActorFixture.Kill(this, half.Factory.Spawn(DroppingDef(5), new Vector2(-300f, 300f)).Entity);
		await NextFrame();
		await NextFrame();
		Assert.Eq(((Pickup)small.Pickups.GetChild(0)).Amount, 1, "0.2 is still worth one coin");
		Assert.Eq(((Pickup)half.Pickups.GetChild(0)).Amount, 3, "2.5 rounds away from zero, as GDScript roundi did");
	}
}
```

(The enemies spawn at distance ~420 from the player at the origin, outside its 70 px magnet, so their pickups stay on the layer.)

- [ ] **Step 2: Run them to verify they fail**

Run: `RUNNER=cs FILTER=LootDirectorTest tools/run_tests.sh`
Expected: build fails — `LootDirector` does not exist.

- [ ] **Step 3: Implement**

Create `port/src/Stage/LootDirector.cs`:

```csharp
using System;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Stage;

/// <summary>Turns enemy drops into pickups and collected pickups into wallet credit.</summary>
public partial class LootDirector : Node
{
	public const string PickupScene = "res://port/src/Actors/Pickup/pickup.tscn";

	private Node2D _pickupLayer = null!;
	private Player _player = null!;
	private Wallet _wallet = null!;
	private float _dropMultiplier = 1f;

	public void Bind(EnemyFactory factory, Node2D pickupLayer, Player player, Wallet wallet, float dropMultiplier = 1f)
	{
		_pickupLayer = pickupLayer;
		_player = player;
		_wallet = wallet;
		_dropMultiplier = dropMultiplier;
		factory.Spawned += OnEnemySpawned;
		player.LootCollected += OnLootCollected;
	}

	public override void _ExitTree()
	{
		if (_player is not null && IsInstanceValid(_player))
		{
			_player.LootCollected -= OnLootCollected;
		}
	}

	private static int RoundHalfAway(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

	private void OnEnemySpawned(Enemy enemy)
	{
		enemy.LootDropped += OnLootDropped;
	}

	/// <summary>Deferred: pickups are collision objects and must not be added while physics signals are flushing.</summary>
	private void OnLootDropped(DropRoll roll, Vector2 position)
	{
		Callable.From(() => SpawnPickup(roll, position)).CallDeferred();
	}

	private void SpawnPickup(DropRoll roll, Vector2 position)
	{
		if (!IsInstanceValid(_pickupLayer))
		{
			return;
		}

		// The stage's drop multiplier is applied to the drop itself, so the pickup shows what it pays.
		var scaled = new DropRoll
		{
			Kind = roll.Kind,
			Amount = Math.Max(1, RoundHalfAway(roll.Amount * _dropMultiplier)),
			Icon = roll.Icon,
		};
		var pickup = GD.Load<PackedScene>(PickupScene).Instantiate<Pickup>();
		_pickupLayer.AddChild(pickup);
		pickup.GlobalPosition = position;
		pickup.Setup(scaled);
	}

	private void OnLootCollected(CurrencyKind kind, int amount)
	{
		_wallet.Add(kind, RoundHalfAway(amount * _player.GainMultiplier(kind)));
	}
}
```

- [ ] **Step 4: Run them to verify they pass**

Run: `RUNNER=cs tools/run_tests.sh`
Expected: `254 passed, 0 failed`.

- [ ] **Step 5: Commit**

```bash
git add port/src/Stage/LootDirector.cs port/tests/Stage/LootDirectorTest.cs
git commit -m "feat(stage): LootDirector" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: `StageRoot` (composition root) and `stage.tscn`

**Files:**
- Create: `port/src/Stage/StageRoot.cs`, `port/src/Stage/stage.tscn`
- Test: `port/tests/Stage/StageRootTest.cs`

**Interfaces:**
- Consumes: everything from Tasks 1-8, `Player.Setup/LimitCamera/Died`, `Building.Setup/Destroyed`.
- Produces: `partial class StageRoot : Node2D` with `event Action<RunResult>? Finished`; `[Export]` properties `Arena`, `World`, `Player`, `Building`, `EnemyLayer`, `ProjectileLayer`, `PickupLayer`, `Clock`, `Targets`, `Factory`, `Spawner`, `Waves`, `Loot`; `RunState? RunState`; `StageState FlowState`; `bool Start(StageDef def, PlayerProfile profile, long runSeed = 0)` (false, after `GD.PushError` per problem, when a def is invalid or the stage already started). Every enemy death adds a kill and raises `event.kill` with the enemy (`Node`) on the player's entity. Finishing is idempotent; it stops the clock and disables `World` processing.

- [ ] **Step 1: Write the failing tests**

Create `port/tests/Stage/StageRootTest.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Gamesmiths.Forge.Events;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Stage;

public class StageRootTest : TestCase
{
	private const string StageScene = "res://port/src/Stage/stage.tscn";
	private const string EnemyScene = "res://port/src/Actors/Enemy/enemy.tscn";
	private const string PickupScene = "res://port/src/Actors/Pickup/pickup.tscn";

	private static EnemyDef Grunt()
	{
		EnemyDef def = DefFactory.Enemy(GD.Load<PackedScene>(EnemyScene));
		def.Stats = new StatBlock { MaxHealth = 20f, MoveSpeed = 40f };
		def.Drops = new DropTable();
		def.Drops.Entries.Add(new DropEntry { Chance = 1f, Icon = DefFactory.Texture(8, 8) });
		return def;
	}

	private static StageDef MakeDef(float duration)
	{
		StageDef def = DefFactory.Stage(duration);
		EnemyDef grunt = Grunt();
		def.TrickleEnemies.Add(grunt);
		def.TrickleIntervalStart = 0.3f;
		def.TrickleIntervalEnd = 0.3f;
		def.Waves!.Entries.Add(new WaveEntry { Time = 0.5f, Enemy = grunt, Count = 3 });
		return def;
	}

	private StageRoot NewStage() => Adopt(GD.Load<PackedScene>(StageScene).Instantiate<StageRoot>());

	private StageRoot RunningStage(float duration, List<RunResult> results)
	{
		StageRoot stage = NewStage();
		stage.Finished += results.Add;
		Assert.True(stage.Start(MakeDef(duration), DefFactory.Profile(), 11));
		return stage;
	}

	private List<Node> Enemies() => [.. Host.GetTree().GetNodesInGroup(Groups.Enemies)];

	public async Task TestRunReachesClearedWhenTimeIsUp()
	{
		var results = new List<RunResult>();
		RunningStage(1.5f, results);
		await WaitSeconds(1.0);
		Assert.True(Enemies().Count > 0, "enemies spawned");
		await WaitSeconds(1.2);
		Assert.Eq(results.Count, 1);
		Assert.Eq(results[0].Outcome, RunOutcome.Cleared);
		Assert.AlmostEq(results[0].Elapsed, 1.5, 0.15);
	}

	public void TestDestroyingTheBuildingFailsTheRun()
	{
		var results = new List<RunResult>();
		StageRoot stage = RunningStage(30f, results);
		ActorFixture.Kill(this, stage.Building.Entity);
		Assert.Eq(results.Count, 1);
		Assert.Eq(results[0].Outcome, RunOutcome.Failed);
	}

	public void TestPlayerDeathFailsTheRun()
	{
		var results = new List<RunResult>();
		StageRoot stage = RunningStage(30f, results);
		ActorFixture.Kill(this, stage.Player.Entity);
		Assert.Eq(results.Count, 1);
		Assert.Eq(results[0].Outcome, RunOutcome.Failed);
	}

	public void TestFinishIsIdempotentFailureFirst()
	{
		var results = new List<RunResult>();
		StageRoot stage = RunningStage(30f, results);
		ActorFixture.Kill(this, stage.Building.Entity);
		stage.Player.EmitSignal(Player.SignalName.Died);
		stage.Clock.EmitSignal(StageClock.SignalName.Ended);
		Assert.Eq(results.Count, 1, "later finish paths must hit the terminal guard");
		Assert.Eq(results[0].Outcome, RunOutcome.Failed);
	}

	public void TestFinishIsIdempotentClearedFirst()
	{
		var results = new List<RunResult>();
		StageRoot stage = RunningStage(30f, results);
		stage.Clock.EmitSignal(StageClock.SignalName.Ended);
		ActorFixture.Kill(this, stage.Building.Entity);
		stage.Player.EmitSignal(Player.SignalName.Died);
		Assert.Eq(results.Count, 1);
		Assert.Eq(results[0].Outcome, RunOutcome.Cleared);
		Assert.Eq(stage.FlowState, StageState.Cleared);
	}

	public void TestTheEndedRunStopsTheWorldAndTheClock()
	{
		var results = new List<RunResult>();
		StageRoot stage = RunningStage(30f, results);
		ActorFixture.Kill(this, stage.Building.Entity);
		Assert.Eq(stage.World.ProcessMode, Node.ProcessModeEnum.Disabled);
		Assert.False(stage.Clock.IsRunning);
	}

	public async Task TestInvalidDefsAreRejectedBeforeAnythingStarts()
	{
		var results = new List<RunResult>();
		StageRoot stage = NewStage();
		stage.Finished += results.Add;
		Assert.False(stage.Start(MakeDef(0f), DefFactory.Profile()));
		await WaitSeconds(0.2);
		Assert.Eq(results.Count, 0);
		Assert.Null(stage.RunState);
	}

	public async Task TestCollectedLootIsCreditedWithTheGainMultiplier()
	{
		var results = new List<RunResult>();
		StageRoot stage = RunningStage(30f, results);
		ActorEntity entity = stage.Player.Entity;
		entity.Apply(StatEffects.SetBase(Stats.GoldGain, entity.Attributes[Stats.GoldGain].ToRawValue(2f)));
		var pickup = GD.Load<PackedScene>(PickupScene).Instantiate<Pickup>();
		stage.PickupLayer.AddChild(pickup);
		pickup.GlobalPosition = stage.Player.GlobalPosition;
		pickup.Setup(new DropRoll { Kind = CurrencyKind.Gold, Amount = 5, Icon = DefFactory.Texture(8, 8) });
		await WaitSeconds(0.3);
		Assert.Eq(stage.RunState!.Wallet.Gold, 10);
	}

	public async Task TestKilledEnemiesAreCountedAndDropPickups()
	{
		var results = new List<RunResult>();
		StageRoot stage = RunningStage(30f, results);
		await WaitSeconds(0.9);
		List<Node> victims = Enemies();
		Assert.True(victims.Count > 0);
		foreach (Node victim in victims)
		{
			ActorFixture.Kill(this, ((Enemy)victim).Entity);
		}

		await NextFrame();
		await NextFrame();
		Assert.Eq(stage.RunState!.Kills, victims.Count);
		Assert.True(stage.PickupLayer.GetChildCount() > 0, "gold dropped");
	}

	public async Task TestEveryEnemyDeathRaisesEventKillOnThePlayer()
	{
		var results = new List<RunResult>();
		StageRoot stage = RunningStage(30f, results);
		var payloads = new List<Node>();
		stage.Player.Entity.Events.Subscribe<Node>(
			GameplayTags.Request(GameplayTags.EventKill), data => payloads.Add(data.Payload), 0);
		await WaitSeconds(0.9);
		List<Node> victims = Enemies();
		Assert.True(victims.Count > 0);
		foreach (Node victim in victims)
		{
			ActorFixture.Kill(this, ((Enemy)victim).Entity);
		}

		Assert.Eq(payloads.Count, victims.Count);
		Assert.True(payloads[0] is Enemy, "the payload is the dead enemy");
		Assert.Eq(stage.RunState!.Kills, victims.Count);
	}

	public async Task TestRestartStartsClean()
	{
		var firstResults = new List<RunResult>();
		StageRoot first = RunningStage(30f, firstResults);
		first.RunState!.Wallet.Add(CurrencyKind.Gold, 50);
		ActorFixture.Kill(this, first.Building.Entity);
		first.Free();
		await NextFrame();
		var secondResults = new List<RunResult>();
		StageRoot second = RunningStage(30f, secondResults);
		Assert.Eq(second.RunState!.Wallet.Gold, 0);
		Assert.AlmostEq(second.Clock.Elapsed, 0.0);
		Assert.Eq(Enemies().Count, 0);
	}

	public void TestSecondStartIsRejectedAndDoesNotDoubleWire()
	{
		var results = new List<RunResult>();
		StageRoot stage = RunningStage(30f, results);
		RunState first = stage.RunState!;
		Assert.False(stage.Start(MakeDef(30f), DefFactory.Profile(), 12));
		Assert.True(stage.RunState == first, "run state untouched by the rejected start");
		stage.Clock.EmitSignal(StageClock.SignalName.Ended);
		Assert.Eq(results.Count, 1);
	}
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `RUNNER=cs FILTER=StageRootTest tools/run_tests.sh`
Expected: build fails — `StageRoot` does not exist.

- [ ] **Step 3: Implement**

Create `port/src/Stage/StageRoot.cs`:

```csharp
using System;
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Stage;

/// <summary>
/// Composition root of one stage run. Builds everything from (StageDef, PlayerProfile), wires the signals between modules,
/// and reports a <see cref="RunResult"/>. Modules never look each other up, and the stage knows nothing about UI: whoever
/// presents it binds to the public models below. (Named StageRoot because a class Stage inside namespace
/// VampireSurvivor.Stage could not be referenced from other namespaces.)
/// </summary>
public partial class StageRoot : Node2D
{
	private static readonly Vector2 PlayerStart = new(0f, 150f);

	private readonly StageFlow _flow = new();

	public event Action<RunResult>? Finished;

	[Export]
	public Arena Arena { get; set; } = null!;

	[Export]
	public Node2D World { get; set; } = null!;

	[Export]
	public Player Player { get; set; } = null!;

	[Export]
	public Building Building { get; set; } = null!;

	[Export]
	public Node2D EnemyLayer { get; set; } = null!;

	[Export]
	public Node2D ProjectileLayer { get; set; } = null!;

	[Export]
	public Node2D PickupLayer { get; set; } = null!;

	[Export]
	public StageClock Clock { get; set; } = null!;

	[Export]
	public TargetProvider Targets { get; set; } = null!;

	[Export]
	public EnemyFactory Factory { get; set; } = null!;

	[Export]
	public SpawnDirector Spawner { get; set; } = null!;

	[Export]
	public WaveDirector Waves { get; set; } = null!;

	[Export]
	public LootDirector Loot { get; set; } = null!;

	public RunState? RunState { get; private set; }

	public StageState FlowState => _flow.State;

	/// <summary>Returns false, after logging every problem, when a def is invalid; nothing is started then.</summary>
	public bool Start(StageDef def, PlayerProfile profile, long runSeed = 0)
	{
		if (RunState is not null)
		{
			GD.PushError("StageRoot.Start: already started; a stage runs once");
			return false;
		}

		List<string> errors = def.Validate();
		errors.AddRange(profile.Validate());
		if (errors.Count > 0)
		{
			foreach (string error in errors)
			{
				GD.PushError($"StageRoot.Start: {error}");
			}

			return false;
		}

		RunState = new RunState(def, profile, runSeed);
		Arena.Build(def.ArenaSize, def.GroundTexture!);
		Building.Setup(def.BuildingTexture!, def.BuildingStats!);
		Player.GlobalPosition = PlayerStart;
		Player.Setup(profile, Targets, ProjectileLayer, RunState.Wallet);
		Player.LimitCamera(Arena.Bounds);
		Targets.Building = Building;
		Targets.Player = Player;
		Factory.Bind(EnemyLayer, Targets, RunState.Rng);

		// Enemies appear on a ring around the defended building, not around the roaming player.
		Spawner.Bind(def, Clock, Factory, Building, RunState.Rng, Arena.Bounds);
		Waves.Bind(def, Clock, Factory, Building, RunState.Rng, Arena.Bounds);
		Loot.Bind(Factory, PickupLayer, Player, RunState.Wallet, def.DropMultiplier);
		Wire();
		_flow.TransitionTo(StageState.Running);
		Clock.Start(def.Duration);
		return true;
	}

	private void Wire()
	{
		Building.Destroyed += () => Finish(RunOutcome.Failed);
		Player.Died += () => Finish(RunOutcome.Failed);
		Clock.Ended += OnClockEnded;
		Factory.Spawned += OnEnemySpawned;
	}

	/// <summary>Time up clears the stage (slice 4 starts the boss fight here instead when the stage has one).</summary>
	private void OnClockEnded()
	{
		Finish(RunOutcome.Cleared);
	}

	private void OnEnemySpawned(Enemy enemy)
	{
		enemy.Died += OnEnemyDied;
	}

	/// <summary>Every kill is also a gameplay event on the player's entity, so on-kill abilities react through Forge.</summary>
	private void OnEnemyDied(Enemy enemy)
	{
		RunState!.Kills++;
		Player.Entity.Raise<Node>(GameplayTags.EventKill, enemy);
	}

	private void Finish(RunOutcome outcome)
	{
		if (_flow.IsTerminal)
		{
			return;
		}

		_flow.TransitionTo(outcome == RunOutcome.Cleared ? StageState.Cleared : StageState.Failed);
		Clock.Stop();
		World.ProcessMode = ProcessModeEnum.Disabled;
		Finished?.Invoke(RunState!.ToResult(outcome, Clock.Elapsed));
	}
}
```

Create `port/src/Stage/stage.tscn`:

```
[gd_scene load_steps=11 format=3]

[ext_resource type="Script" path="res://port/src/Stage/StageRoot.cs" id="1_stage"]
[ext_resource type="PackedScene" path="res://port/src/Stage/arena.tscn" id="2_arena"]
[ext_resource type="PackedScene" path="res://port/src/Actors/Building/building.tscn" id="3_building"]
[ext_resource type="PackedScene" path="res://port/src/Actors/Player/player.tscn" id="4_player"]
[ext_resource type="Script" path="res://port/src/Stage/StageClock.cs" id="5_clock"]
[ext_resource type="Script" path="res://port/src/Components/TargetProvider.cs" id="6_targets"]
[ext_resource type="Script" path="res://port/src/Stage/EnemyFactory.cs" id="7_factory"]
[ext_resource type="Script" path="res://port/src/Stage/SpawnDirector.cs" id="8_spawner"]
[ext_resource type="Script" path="res://port/src/Stage/WaveDirector.cs" id="9_waves"]
[ext_resource type="Script" path="res://port/src/Stage/LootDirector.cs" id="10_loot"]

[node name="Stage" type="Node2D" node_paths=PackedStringArray("Arena", "World", "Player", "Building", "EnemyLayer", "ProjectileLayer", "PickupLayer", "Clock", "Targets", "Factory", "Spawner", "Waves", "Loot")]
script = ExtResource("1_stage")
Arena = NodePath("Arena")
World = NodePath("World")
Player = NodePath("World/Player")
Building = NodePath("World/Building")
EnemyLayer = NodePath("World/Enemies")
ProjectileLayer = NodePath("World/Projectiles")
PickupLayer = NodePath("World/Pickups")
Clock = NodePath("Systems/StageClock")
Targets = NodePath("Systems/TargetProvider")
Factory = NodePath("Systems/EnemyFactory")
Spawner = NodePath("Systems/SpawnDirector")
Waves = NodePath("Systems/WaveDirector")
Loot = NodePath("Systems/LootDirector")

[node name="Arena" parent="." instance=ExtResource("2_arena")]

[node name="World" type="Node2D" parent="."]
y_sort_enabled = true

[node name="Pickups" type="Node2D" parent="World"]
z_index = -1

[node name="Building" parent="World" instance=ExtResource("3_building")]

[node name="Enemies" type="Node2D" parent="World"]
y_sort_enabled = true

[node name="Player" parent="World" instance=ExtResource("4_player")]

[node name="Projectiles" type="Node2D" parent="World"]
y_sort_enabled = true

[node name="Systems" type="Node" parent="."]

[node name="StageClock" type="Node" parent="Systems"]
script = ExtResource("5_clock")

[node name="TargetProvider" type="Node" parent="Systems"]
script = ExtResource("6_targets")

[node name="EnemyFactory" type="Node" parent="Systems"]
script = ExtResource("7_factory")

[node name="SpawnDirector" type="Node" parent="Systems"]
script = ExtResource("8_spawner")

[node name="WaveDirector" type="Node" parent="Systems"]
script = ExtResource("9_waves")

[node name="LootDirector" type="Node" parent="Systems"]
script = ExtResource("10_loot")
```

- [ ] **Step 4: Run them to verify they pass**

Run: `IMPORT=1 RUNNER=cs tools/run_tests.sh`
Expected: `266 passed, 0 failed`. `LayerRulesTest` green (`StageRoot` uses Actors/Components/Defs/Gas, all lower).

- [ ] **Step 5: Commit**

```bash
git add port/src/Stage/StageRoot.cs port/src/Stage/stage.tscn port/tests/Stage/StageRootTest.cs
git commit -m "feat(stage): StageRoot composition root and stage scene" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: `TimelineBar` and `WorldHealthBar`

**Files:**
- Create: `port/src/Ui/Hud/TimelineBar.cs`, `port/src/Ui/Widgets/WorldHealthBar.cs`
- Test: `port/tests/Ui/TimelineBarTest.cs`, `port/tests/Ui/WorldHealthBarTest.cs`

**Interfaces:**
- Produces (namespace `VampireSurvivor.Ui`):
  - `partial class TimelineBar : Control { const float MarkHalfWidth = 5f; exports TrackColor, FillColor, MarkColor, PassedMarkColor; void Configure(float duration, float[] marks); void SetElapsed(float elapsed); float MarkX(float mark) }` — wave diamonds; event circles and the BOSS label come with slice 4.
  - `partial class WorldHealthBar : Node2D { exports BarSize (80x8), BackgroundColour, BorderColour, FillColourHigh/Mid/Low, AlwaysVisible (true); void Bind(Health health, Node2D? anchor, Vector2 offset); void Step(); float FillRatio(); Color ColourFor(float ratio) }`.

- [ ] **Step 1: Write the failing tests**

Create `port/tests/Ui/TimelineBarTest.cs`:

```csharp
using Godot;
using VampireSurvivor.Ui;

namespace VampireSurvivor.Tests.Ui;

public class TimelineBarTest : TestCase
{
	private TimelineBar MakeBar(float duration, params float[] marks)
	{
		TimelineBar bar = Adopt(new TimelineBar());
		bar.Size = new Vector2(400f, 20f);
		bar.Configure(duration, marks);
		return bar;
	}

	public void TestMarkXScalesWithWidthAndDuration()
	{
		TimelineBar bar = MakeBar(100f, 25f, 50f);
		Assert.AlmostEq(bar.MarkX(25f), 100.0);
		Assert.AlmostEq(bar.MarkX(50f), 200.0);
		Assert.AlmostEq(bar.MarkX(500f), 400.0, 0.001, "clamped to the bar");
	}

	public void TestZeroDurationDoesNotDivideByZero()
	{
		TimelineBar bar = MakeBar(0f, 10f);
		bar.SetElapsed(5f);
		Assert.AlmostEq(bar.MarkX(10f), 0.0);
	}
}
```

Create `port/tests/Ui/WorldHealthBarTest.cs`:

```csharp
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;
using VampireSurvivor.Ui;

namespace VampireSurvivor.Tests.Ui;

public class WorldHealthBarTest : TestCase
{
	private sealed record Rig(WorldHealthBar Bar, Node2D Anchor, ActorEntity Entity, Health Health);

	private Rig MakeRig(float maxHealth = 50f)
	{
		ActorEntity entity = ActorFixture.BareActor(this, new StatBlock { MaxHealth = maxHealth });
		var health = new Health();
		entity.AddChild(health);
		health.Bind(entity);
		Node2D anchor = Adopt(new Node2D());
		WorldHealthBar bar = Adopt(new WorldHealthBar());
		bar.Bind(health, anchor, new Vector2(0f, -60f));
		return new Rig(bar, anchor, entity, health);
	}

	public void TestFillRatioFollowsHealthChanged()
	{
		Rig rig = MakeRig();
		Assert.AlmostEq(rig.Bar.FillRatio(), 1.0);
		DamageEffects.Hit(ActorFixture.BareActor(this, new StatBlock()), rig.Entity, 25f);
		Assert.AlmostEq(rig.Bar.FillRatio(), 0.5);
	}

	public void TestFillRatioIsZeroWhenMaximumIsZero()
	{
		WorldHealthBar bar = Adopt(new WorldHealthBar());
		Assert.AlmostEq(bar.FillRatio(), 0.0);
		Health health = Adopt(new Health());
		bar.Bind(health, null, Vector2.Zero);
		health.EmitSignal(Health.SignalName.Changed, 10f, 0f);
		Assert.AlmostEq(bar.FillRatio(), 0.0);
	}

	public void TestFillRatioIsClamped()
	{
		Rig rig = MakeRig();
		rig.Health.EmitSignal(Health.SignalName.Changed, 80f, 50f);
		Assert.AlmostEq(rig.Bar.FillRatio(), 1.0);
		rig.Health.EmitSignal(Health.SignalName.Changed, -5f, 50f);
		Assert.AlmostEq(rig.Bar.FillRatio(), 0.0);
	}

	public void TestBindPositionsAtAnchorPlusOffsetAndFollowsTheAnchor()
	{
		Rig rig = MakeRig();
		rig.Anchor.GlobalPosition = new Vector2(100f, 40f);
		rig.Bar.Step();
		Assert.Eq(rig.Bar.GlobalPosition, new Vector2(100f, -20f));
		rig.Anchor.GlobalPosition = new Vector2(-30f, 10f);
		rig.Bar.Step();
		Assert.Eq(rig.Bar.GlobalPosition, new Vector2(-30f, -50f));
	}

	public void TestFreedAnchorHidesTheBarWithoutError()
	{
		Rig rig = MakeRig();
		Assert.True(rig.Bar.Visible);
		rig.Anchor.Free();
		rig.Bar.Step();
		Assert.False(rig.Bar.Visible);
	}

	public void TestFillColourShiftsGreenOrangeRed()
	{
		WorldHealthBar bar = Adopt(new WorldHealthBar());
		Assert.Eq(bar.ColourFor(1f), bar.FillColourHigh);
		Assert.Eq(bar.ColourFor(0.5f), bar.FillColourMid);
		Assert.Eq(bar.ColourFor(0.1f), bar.FillColourLow);
	}

	public void TestHidesAtFullHealthWhenNotAlwaysVisible()
	{
		Rig rig = MakeRig();
		rig.Bar.AlwaysVisible = false;
		rig.Bar.Step();
		Assert.False(rig.Bar.Visible);
		rig.Health.EmitSignal(Health.SignalName.Changed, 10f, 50f);
		rig.Bar.Step();
		Assert.True(rig.Bar.Visible);
	}
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `RUNNER=cs FILTER=Ui/ tools/run_tests.sh`
Expected: build fails — `TimelineBar` / `WorldHealthBar` do not exist.

- [ ] **Step 3: Implement**

Create `port/src/Ui/Hud/TimelineBar.cs`:

```csharp
using System;
using Godot;

namespace VampireSurvivor.Ui;

/// <summary>Stage progress: a fill for elapsed time and a diamond for every wave; passed marks turn grey.</summary>
public partial class TimelineBar : Control
{
	public const float MarkHalfWidth = 5f;

	private float _duration = 1f;
	private float[] _marks = [];
	private float _elapsed;

	[Export]
	public Color TrackColor { get; set; } = new(0.12f, 0.12f, 0.16f, 0.85f);

	[Export]
	public Color FillColor { get; set; } = new(0.35f, 0.7f, 1f);

	[Export]
	public Color MarkColor { get; set; } = new(1f, 0.75f, 0.2f);

	[Export]
	public Color PassedMarkColor { get; set; } = new(0.55f, 0.55f, 0.55f);

	public void Configure(float duration, float[] marks)
	{
		_duration = duration;
		_marks = marks;
		_elapsed = 0f;
		QueueRedraw();
	}

	public void SetElapsed(float elapsed)
	{
		_elapsed = elapsed;
		QueueRedraw();
	}

	public float MarkX(float mark)
	{
		if (_duration <= 0f)
		{
			return 0f;
		}

		return Size.X * Math.Clamp(mark / _duration, 0f, 1f);
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(Vector2.Zero, Size), TrackColor);
		float ratio = _duration <= 0f ? 1f : Math.Clamp(_elapsed / _duration, 0f, 1f);
		DrawRect(new Rect2(Vector2.Zero, new Vector2(Size.X * ratio, Size.Y)), FillColor);
		foreach (float mark in _marks)
		{
			float x = MarkX(mark);
			Color color = mark <= _elapsed ? PassedMarkColor : MarkColor;
			DrawColoredPolygon(
				[
					new Vector2(x, 0f),
					new Vector2(x + MarkHalfWidth, Size.Y * 0.5f),
					new Vector2(x, Size.Y),
					new Vector2(x - MarkHalfWidth, Size.Y * 0.5f),
				],
				color);
		}
	}
}
```

Create `port/src/Ui/Widgets/WorldHealthBar.cs`:

```csharp
using System;
using Godot;
using VampireSurvivor.Components;

namespace VampireSurvivor.Ui;

/// <summary>Health bar drawn in world space above an actor. Read-only: it observes Health and follows its anchor.</summary>
public partial class WorldHealthBar : Node2D
{
	private float _current;
	private float _maximum;
	private Node2D? _anchor;
	private Vector2 _offset;

	[Export]
	public Vector2 BarSize { get; set; } = new(80f, 8f);

	[Export]
	public Color BackgroundColour { get; set; } = new(0.1f, 0.1f, 0.1f, 0.8f);

	[Export]
	public Color BorderColour { get; set; } = new(0f, 0f, 0f);

	[Export]
	public Color FillColourHigh { get; set; } = new(0.3f, 0.8f, 0.3f);

	[Export]
	public Color FillColourMid { get; set; } = new(0.95f, 0.6f, 0.15f);

	[Export]
	public Color FillColourLow { get; set; } = new(0.85f, 0.15f, 0.15f);

	[Export]
	public bool AlwaysVisible { get; set; } = true;

	public void Bind(Health health, Node2D? anchor, Vector2 offset)
	{
		_anchor = anchor;
		_offset = offset;
		health.Changed += OnHealthChanged;
		OnHealthChanged(health.Current(), health.Maximum());
		Step();
	}

	public override void _Process(double delta) => Step();

	/// <summary>Follows the anchor and refreshes visibility; _Process calls it every frame.</summary>
	public void Step()
	{
		if (!IsInstanceValid(_anchor))
		{
			Visible = false;
			return;
		}

		GlobalPosition = _anchor!.GlobalPosition + _offset;
		Visible = AlwaysVisible || FillRatio() < 1f;
	}

	public float FillRatio()
	{
		if (_maximum <= 0f)
		{
			return 0f;
		}

		return Math.Clamp(_current / _maximum, 0f, 1f);
	}

	public Color ColourFor(float ratio)
	{
		if (ratio > 0.6f)
		{
			return FillColourHigh;
		}

		return ratio > 0.3f ? FillColourMid : FillColourLow;
	}

	public override void _Draw()
	{
		var rect = new Rect2(-BarSize * 0.5f, BarSize);
		DrawRect(rect, BackgroundColour);
		float ratio = FillRatio();
		DrawRect(new Rect2(rect.Position, new Vector2(rect.Size.X * ratio, rect.Size.Y)), ColourFor(ratio));
		DrawRect(rect, BorderColour, false, 1f);
	}

	private void OnHealthChanged(float current, float maximum)
	{
		_current = current;
		_maximum = maximum;
		QueueRedraw();
	}
}
```

(The GDScript `size` export is renamed `BarSize` because `Node2D` has no `Size`, but `Control` does and the name would mislead.)

- [ ] **Step 4: Run them to verify they pass**

Run: `RUNNER=cs tools/run_tests.sh`
Expected: `275 passed, 0 failed`.

- [ ] **Step 5: Commit**

```bash
git add port/src/Ui/Hud/TimelineBar.cs port/src/Ui/Widgets/WorldHealthBar.cs port/tests/Ui/TimelineBarTest.cs port/tests/Ui/WorldHealthBarTest.cs
git commit -m "feat(ui): TimelineBar and WorldHealthBar" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: `Hud` and `hud.tscn`

**Files:**
- Create: `port/src/Ui/Hud/Hud.cs`, `port/src/Ui/Hud/hud.tscn`
- Test: `port/tests/Ui/HudTest.cs`

**Interfaces:**
- Consumes: `Health.Changed`, `Wallet.Changed` (C# event), `StageClock.Ticked`, `Timeline`, `TimelineBar` (Task 10), `RunResult`.
- Produces: `partial class Hud : CanvasLayer { [Export] ProgressBar PlayerBar, BuildingBar; [Export] Label WalletLabel, ResultLabel; [Export] TimelineBar Timeline; void Bind(Health playerHealth, Health buildingHealth, Wallet wallet, StageClock clock, Timeline timeline); void ShowResult(RunResult result) }`. Wallet text `Gold {g}   Stone {s}   Ticket {t}`; result text `STAGE CLEARED|GAME OVER\nKills {k}   Gold {g}   Stone {s}\nR: restart` (Gem and "Enter: continue" return with slice 5). Unsubscribes from the wallet in `_ExitTree`.

- [ ] **Step 1: Write the failing tests**

Create `port/tests/Ui/HudTest.cs`:

```csharp
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Support;
using VampireSurvivor.Ui;

namespace VampireSurvivor.Tests.Ui;

public class HudTest : TestCase
{
	private const string HudScene = "res://port/src/Ui/Hud/hud.tscn";

	private sealed record Rig(Hud Hud, ActorEntity PlayerEntity, Wallet Wallet);

	private Health HealthFor(ActorEntity entity)
	{
		var health = new Health();
		entity.AddChild(health);
		health.Bind(entity);
		return health;
	}

	private Rig MakeRig()
	{
		var stats = new StatBlock { MaxHealth = 50f };
		ActorEntity player = ActorFixture.BareActor(this, stats);
		ActorEntity building = ActorFixture.BareActor(this, stats);
		var wallet = new Wallet();
		StageClock clock = Adopt(new StageClock());
		clock.SetPhysicsProcess(false);
		Hud hud = Adopt(GD.Load<PackedScene>(HudScene).Instantiate<Hud>());
		hud.Bind(HealthFor(player), HealthFor(building), wallet, clock, new Timeline(100f, [30f]));
		return new Rig(hud, player, wallet);
	}

	public void TestBarsShowInitialHealthAndFollowDamage()
	{
		Rig rig = MakeRig();
		Assert.AlmostEq(rig.Hud.PlayerBar.Value, 50.0);
		Assert.AlmostEq(rig.Hud.PlayerBar.MaxValue, 50.0);
		DamageEffects.Hit(ActorFixture.BareActor(this, new StatBlock()), rig.PlayerEntity, 20f);
		Assert.AlmostEq(rig.Hud.PlayerBar.Value, 30.0);
	}

	public void TestWalletLabelFollowsTheWallet()
	{
		Rig rig = MakeRig();
		Assert.Eq(rig.Hud.WalletLabel.Text, "Gold 0   Stone 0   Ticket 0");
		rig.Wallet.Add(CurrencyKind.Gold, 7);
		rig.Wallet.Add(CurrencyKind.Stone, 2);
		rig.Wallet.Add(CurrencyKind.Ticket, 1);
		Assert.Eq(rig.Hud.WalletLabel.Text, "Gold 7   Stone 2   Ticket 1");
	}

	public void TestShowResultDisplaysOutcomeAndTotals()
	{
		Rig rig = MakeRig();
		rig.Hud.ShowResult(new RunResult { Outcome = RunOutcome.Cleared, Kills = 3, Gold = 40 });
		Label label = rig.Hud.ResultLabel;
		Assert.True(label.Visible);
		Assert.True(label.Text.Contains("STAGE CLEARED"), label.Text);
		Assert.True(label.Text.Contains("Kills 3"), label.Text);
		Assert.True(label.Text.Contains("Gold 40"), label.Text);
		Assert.True(label.Text.Contains("R: restart"), label.Text);
		rig.Hud.ShowResult(new RunResult { Outcome = RunOutcome.Failed });
		Assert.True(label.Text.Contains("GAME OVER"), label.Text);
	}

	public void TestAFreedHudStopsFollowingTheWallet()
	{
		Rig rig = MakeRig();
		rig.Hud.Free();
		rig.Wallet.Add(CurrencyKind.Gold, 5);
		Assert.Eq(rig.Wallet.Gold, 5, "the wallet keeps working; the freed HUD's label is never touched");
	}
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `RUNNER=cs FILTER=HudTest tools/run_tests.sh`
Expected: build fails — `Hud` does not exist.

- [ ] **Step 3: Implement**

Create `port/src/Ui/Hud/Hud.cs`:

```csharp
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Stage;

namespace VampireSurvivor.Ui;

/// <summary>
/// Read-only view of the stage. It binds to models and never mutates them; whoever owns the stage (DevStage now, Main in
/// slice 5) binds it, because the stage layer must not know the HUD.
/// </summary>
public partial class Hud : CanvasLayer
{
	private Wallet? _wallet;

	[Export]
	public ProgressBar PlayerBar { get; set; } = null!;

	[Export]
	public ProgressBar BuildingBar { get; set; } = null!;

	[Export]
	public Label WalletLabel { get; set; } = null!;

	[Export]
	public TimelineBar Timeline { get; set; } = null!;

	[Export]
	public Label ResultLabel { get; set; } = null!;

	public void Bind(Health playerHealth, Health buildingHealth, Wallet wallet, StageClock clock, Timeline timeline)
	{
		playerHealth.Changed += OnPlayerHealthChanged;
		buildingHealth.Changed += OnBuildingHealthChanged;
		SetBar(PlayerBar, playerHealth.Current(), playerHealth.Maximum());
		SetBar(BuildingBar, buildingHealth.Current(), buildingHealth.Maximum());
		_wallet = wallet;
		wallet.Changed += OnWalletChanged;
		OnWalletChanged(wallet.Gold, wallet.Stone, wallet.Tickets);
		Timeline.Configure(timeline.Duration, timeline.Marks);
		clock.Ticked += Timeline.SetElapsed;
	}

	public void ShowResult(RunResult result)
	{
		string title = result.Outcome == RunOutcome.Cleared ? "STAGE CLEARED" : "GAME OVER";
		ResultLabel.Text = $"{title}\nKills {result.Kills}   Gold {result.Gold}   Stone {result.Stone}\nR: restart";
		ResultLabel.Visible = true;
	}

	/// <summary>The wallet is a plain C# object that can outlive this node; Godot signals disconnect themselves.</summary>
	public override void _ExitTree()
	{
		if (_wallet is not null)
		{
			_wallet.Changed -= OnWalletChanged;
			_wallet = null;
		}
	}

	private static void SetBar(ProgressBar bar, float current, float maximum)
	{
		bar.MaxValue = Mathf.Max(maximum, 1f);
		bar.Value = current;
	}

	private void OnPlayerHealthChanged(float current, float maximum) => SetBar(PlayerBar, current, maximum);

	private void OnBuildingHealthChanged(float current, float maximum) => SetBar(BuildingBar, current, maximum);

	private void OnWalletChanged(int gold, int stone, int tickets)
	{
		WalletLabel.Text = $"Gold {gold}   Stone {stone}   Ticket {tickets}";
	}
}
```

Create `port/src/Ui/Hud/hud.tscn`:

```
[gd_scene load_steps=3 format=3]

[ext_resource type="Script" path="res://port/src/Ui/Hud/Hud.cs" id="1_hud"]
[ext_resource type="Script" path="res://port/src/Ui/Hud/TimelineBar.cs" id="2_timeline"]

[node name="Hud" type="CanvasLayer" node_paths=PackedStringArray("PlayerBar", "BuildingBar", "WalletLabel", "Timeline", "ResultLabel")]
script = ExtResource("1_hud")
PlayerBar = NodePath("Root/Bars/PlayerBar")
BuildingBar = NodePath("Root/Bars/BuildingBar")
WalletLabel = NodePath("Root/Bars/WalletLabel")
Timeline = NodePath("Root/Timeline")
ResultLabel = NodePath("Root/ResultLabel")

[node name="Root" type="Control" parent="."]
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
grow_horizontal = 2
grow_vertical = 2
mouse_filter = 2

[node name="Bars" type="VBoxContainer" parent="Root"]
layout_mode = 0
offset_left = 16.0
offset_top = 16.0
offset_right = 296.0
offset_bottom = 90.0

[node name="PlayerBar" type="ProgressBar" parent="Root/Bars"]
self_modulate = Color(0.4, 1, 0.4, 1)
custom_minimum_size = Vector2(280, 16)
layout_mode = 2
show_percentage = false

[node name="BuildingBar" type="ProgressBar" parent="Root/Bars"]
self_modulate = Color(1, 0.65, 0.25, 1)
custom_minimum_size = Vector2(280, 16)
layout_mode = 2
show_percentage = false

[node name="WalletLabel" type="Label" parent="Root/Bars"]
layout_mode = 2

[node name="Timeline" type="Control" parent="Root"]
layout_mode = 1
anchors_preset = 5
anchor_left = 0.5
anchor_right = 0.5
offset_left = -260.0
offset_top = 16.0
offset_right = 260.0
offset_bottom = 40.0
grow_horizontal = 2
script = ExtResource("2_timeline")

[node name="ResultLabel" type="Label" parent="Root"]
visible = false
layout_mode = 1
anchors_preset = 8
anchor_left = 0.5
anchor_top = 0.5
anchor_right = 0.5
anchor_bottom = 0.5
offset_left = -240.0
offset_top = -60.0
offset_right = 240.0
offset_bottom = 60.0
grow_horizontal = 2
grow_vertical = 2
horizontal_alignment = 1
vertical_alignment = 1
```

- [ ] **Step 4: Run them to verify they pass**

Run: `IMPORT=1 RUNNER=cs tools/run_tests.sh`
Expected: `279 passed, 0 failed`. Confirm `TestAFreedHudStopsFollowingTheWallet` fails if you temporarily delete `_ExitTree` (an `ObjectDisposedException` escapes `Wallet.Add`), then restore it.

- [ ] **Step 5: Commit**

```bash
git add port/src/Ui/Hud/Hud.cs port/src/Ui/Hud/hud.tscn port/tests/Ui/HudTest.cs
git commit -m "feat(ui): basic HUD (health bars, wallet, timeline, result)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: First `port/data` content and `DataFilesTest`

**Files:**
- Create: `port/data/characters/soldier_visual.tres`, `port/data/characters/default_profile.tres`, `port/data/abilities/arrow.tres`, `port/data/abilities/dash.tres`, `port/data/projectiles/arrow.tres`, `port/data/enemies/orc_visual.tres`, `port/data/enemies/orc.tres`, `port/data/enemies/orc_swarmer.tres`, `port/data/drops/orc_drops.tres`, `port/data/waves/stage_01_waves.tres`, `port/data/stages/stage_01.tres`
- Test: `port/tests/Data/DataFilesTest.cs`

**Interfaces:**
- Consumes: every Def from slices 2a-2c, `SourceTree.Files(directory, extension)` (test support), `StageRoot` (Task 9).
- Produces: the shipped data, mirroring `data/` numbers exactly (orc 40 HP / 55 speed / 8 contact, swarmer 14 / 90 / 4 targeting the player, arrow 12 dmg / 0.7 s / 260 range, dash 480 / 0.18 s / 1.2 s, stage_01: 180 s, 200 gold, drop multiplier 10000, five waves at 30/60/90/120/150 s, building 500 HP). `res://port/data/...` paths are what the dev scene loads.

Format rules for the files below: no `uid=` in headers (Godot adds them on import), scripts by path, exported properties by C# name, enums as ints (`CurrencyKind.Stone = 1`, `TargetPolicy.Player = 1`), typed arrays as `Array[ExtResource("<script id>")]([...])`.

- [ ] **Step 1: Write the failing test**

Create `port/tests/Data/DataFilesTest.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Godot;
using VampireSurvivor.Abilities;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Data;

/// <summary>Scans port/data instead of keeping a hand-written list (spec section 5).</summary>
public class DataFilesTest : TestCase
{
	private const string DataRoot = "res://port/data";
	private const string Stage01 = "res://port/data/stages/stage_01.tres";
	private const string Profile = "res://port/data/characters/default_profile.tres";

	public void TestEveryDataFileLoads()
	{
		List<string> files = [.. SourceTree.Files(DataRoot, ".tres")];
		Assert.True(files.Count >= 11, $"found {files.Count} files");
		foreach (string path in files)
		{
			Assert.NotNull(ResourceLoader.Load(path), path);
		}
	}

	public void TestStage01AndTheDefaultProfileValidate()
	{
		Assert.NoErrors(GD.Load<StageDef>(Stage01).Validate(), "stage_01");
		Assert.NoErrors(GD.Load<PlayerProfile>(Profile).Validate(), "default_profile");
	}

	public void TestStage01SchedulesFiveWavesInsideTheStage()
	{
		StageDef stage = GD.Load<StageDef>(Stage01);
		Assert.SequenceEq(stage.Waves!.Times(), new[] { 30f, 60f, 90f, 120f, 150f });
		Assert.AlmostEq(stage.Duration, 180.0);
	}

	public void TestStage01StartsWithTheOwnerEconomy()
	{
		StageDef stage = GD.Load<StageDef>(Stage01);
		Assert.Eq(stage.StartGold, 200);
		Assert.Eq(stage.StartStone, 0);
		Assert.AlmostEq(stage.DropMultiplier, 10000.0, 0.001, "prototype tuning, as in data/stages/stage_01.tres");
		Assert.AlmostEq(stage.BuildingStats!.MaxHealth, 500.0);
	}

	public void TestEnemyDefsUseTheGenericEnemySceneAndHaveADeathStrip()
	{
		foreach (string path in new[] { "res://port/data/enemies/orc.tres", "res://port/data/enemies/orc_swarmer.tres" })
		{
			EnemyDef def = GD.Load<EnemyDef>(path);
			Assert.Eq(def.Scene!.ResourcePath, "res://port/src/Actors/Enemy/enemy.tscn", path);
			Assert.NotNull(def.Visual!.FindStrip(AnimNames.Death), $"{path} has a death strip");
		}

		Assert.Eq(GD.Load<EnemyDef>("res://port/data/enemies/orc_swarmer.tres").TargetPolicy, TargetPolicy.Player);
	}

	public void TestTheDefaultProfileStartsWithTheArrowAndTheDash()
	{
		PlayerProfile profile = GD.Load<PlayerProfile>(Profile);
		var tags = profile.InnateAbilities.Select(a => a.Tag).ToList();
		Assert.SequenceEq(tags, new[] { "ability.arrow", "ability.dash" });
		var arrow = (VolleyDef)profile.InnateAbilities[0];
		Assert.True(arrow.AutoFire);
		Assert.AlmostEq(arrow.Damage!.Sample(1f), 12.0);
		Assert.AlmostEq(arrow.Cooldown!.Sample(6f), 0.7);
		Assert.AlmostEq(arrow.ArrowCount!.Sample(3f), 1.0);
		Assert.False(profile.InnateAbilities[1].AutoFire, "the dash is on demand");
	}

	public void TestTheShippedStageStartsWithTheDefaultProfile()
	{
		StageRoot stage = Adopt(GD.Load<PackedScene>("res://port/src/Stage/stage.tscn").Instantiate<StageRoot>());
		Assert.True(stage.Start(GD.Load<StageDef>(Stage01), GD.Load<PlayerProfile>(Profile)), "the exact call DevStage makes");
	}
}
```

(`SourceTree.Files` recurses into sub-folders.)

- [ ] **Step 2: Run it to verify it fails**

Run: `RUNNER=cs FILTER=DataFilesTest tools/run_tests.sh`
Expected: FAIL — `found 0 files` (and null loads).

- [ ] **Step 3: Write the data files**

`port/data/characters/soldier_visual.tres`:

```
[gd_resource type="Resource" format=3]

[ext_resource type="Script" path="res://port/src/Defs/VisualDef.cs" id="1_visual"]
[ext_resource type="Script" path="res://port/src/Defs/AnimationStrip.cs" id="2_strip"]
[ext_resource type="Texture2D" path="res://assets/art/tinyrpg/soldier/Soldier_Idle.png" id="3_idle"]
[ext_resource type="Texture2D" path="res://assets/art/tinyrpg/soldier/Soldier_Walk.png" id="4_walk"]
[ext_resource type="Texture2D" path="res://assets/art/tinyrpg/soldier/Soldier_Attack01.png" id="5_attack"]
[ext_resource type="Texture2D" path="res://assets/art/tinyrpg/soldier/Soldier_Hurt.png" id="6_hurt"]
[ext_resource type="Texture2D" path="res://assets/art/tinyrpg/soldier/Soldier_Death.png" id="7_death"]

[sub_resource type="Resource" id="Resource_idle"]
script = ExtResource("2_strip")
Animation = &"idle"
Texture = ExtResource("3_idle")
FrameCount = 6
Fps = 8.0
Loop = true

[sub_resource type="Resource" id="Resource_walk"]
script = ExtResource("2_strip")
Animation = &"walk"
Texture = ExtResource("4_walk")
FrameCount = 8
Fps = 10.0
Loop = true

[sub_resource type="Resource" id="Resource_attack"]
script = ExtResource("2_strip")
Animation = &"attack"
Texture = ExtResource("5_attack")
FrameCount = 6
Fps = 12.0
Loop = false

[sub_resource type="Resource" id="Resource_hurt"]
script = ExtResource("2_strip")
Animation = &"hurt"
Texture = ExtResource("6_hurt")
FrameCount = 4
Fps = 12.0
Loop = false

[sub_resource type="Resource" id="Resource_death"]
script = ExtResource("2_strip")
Animation = &"death"
Texture = ExtResource("7_death")
FrameCount = 4
Fps = 8.0
Loop = false

[resource]
script = ExtResource("1_visual")
Strips = Array[ExtResource("2_strip")]([SubResource("Resource_idle"), SubResource("Resource_walk"), SubResource("Resource_attack"), SubResource("Resource_hurt"), SubResource("Resource_death")])
Offset = Vector2(0, -12)
```

(The GDScript walk strip used the default `fps` of 10.0.)

`port/data/enemies/orc_visual.tres`: identical to `soldier_visual.tres` except the five texture paths, which are `res://assets/art/tinyrpg/orc/Orc_Idle.png`, `Orc_Walk.png`, `Orc_Attack01.png`, `Orc_Hurt.png`, `Orc_Death.png` (frame counts, fps, loop and offset are the same as the soldier's, as in `data/enemies/orc_visual.tres`).

`port/data/drops/orc_drops.tres`:

```
[gd_resource type="Resource" format=3]

[ext_resource type="Script" path="res://port/src/Defs/DropTable.cs" id="1_table"]
[ext_resource type="Script" path="res://port/src/Defs/DropEntry.cs" id="2_entry"]
[ext_resource type="Texture2D" path="res://assets/art/items/coin.png" id="3_coin"]
[ext_resource type="Texture2D" path="res://assets/art/items/stone.png" id="4_stone"]

[sub_resource type="Resource" id="Resource_gold"]
script = ExtResource("2_entry")
Kind = 0
Icon = ExtResource("3_coin")
Chance = 1.0
MinAmount = 1
MaxAmount = 2

[sub_resource type="Resource" id="Resource_stone"]
script = ExtResource("2_entry")
Kind = 1
Icon = ExtResource("4_stone")
Chance = 0.12
MinAmount = 1
MaxAmount = 1

[resource]
script = ExtResource("1_table")
Entries = Array[ExtResource("2_entry")]([SubResource("Resource_gold"), SubResource("Resource_stone")])
```

`port/data/enemies/orc.tres`:

```
[gd_resource type="Resource" format=3]

[ext_resource type="Script" path="res://port/src/Defs/EnemyDef.cs" id="1_def"]
[ext_resource type="Script" path="res://port/src/Defs/StatBlock.cs" id="2_stats"]
[ext_resource type="PackedScene" path="res://port/src/Actors/Enemy/enemy.tscn" id="3_scene"]
[ext_resource type="Resource" path="res://port/data/enemies/orc_visual.tres" id="4_visual"]
[ext_resource type="Resource" path="res://port/data/drops/orc_drops.tres" id="5_drops"]

[sub_resource type="Resource" id="Resource_stats"]
script = ExtResource("2_stats")
MaxHealth = 40.0
MoveSpeed = 55.0
Attack = 1.0

[resource]
script = ExtResource("1_def")
Id = &"orc"
Scene = ExtResource("3_scene")
Visual = ExtResource("4_visual")
VisualScale = 1.0
Tint = Color(1, 1, 1, 1)
Stats = SubResource("Resource_stats")
ContactDamage = 8.0
ContactInterval = 0.9
TargetPolicy = 0
BodyRadius = 10.0
Drops = ExtResource("5_drops")
```

`port/data/enemies/orc_swarmer.tres`: the same as `orc.tres` with `Id = &"orc_swarmer"`, `MaxHealth = 14.0`, `MoveSpeed = 90.0`, `VisualScale = 0.7`, `Tint = Color(1, 0.6, 0.6, 1)`, `ContactDamage = 4.0`, `ContactInterval = 0.7`, `TargetPolicy = 1`, `BodyRadius = 7.0`.

`port/data/projectiles/arrow.tres`:

```
[gd_resource type="Resource" format=3]

[ext_resource type="Script" path="res://port/src/Defs/ProjectileDef.cs" id="1_def"]
[ext_resource type="Texture2D" path="res://assets/art/tinyrpg/arrow.png" id="2_tex"]

[resource]
script = ExtResource("1_def")
Texture = ExtResource("2_tex")
Speed = 420.0
Lifetime = 0.7
Pierce = 0
HitRadius = 6.0
Hits = 0
```

`port/data/abilities/arrow.tres` (the innate arrow is a Volley of one arrow at every level):

```
[gd_resource type="Resource" format=3]

[ext_resource type="Script" path="res://port/src/Abilities/Attacks/VolleyDef.cs" id="1_volley"]
[ext_resource type="PackedScene" path="res://port/src/Actors/Projectile/projectile.tscn" id="2_scene"]
[ext_resource type="Resource" path="res://port/data/projectiles/arrow.tres" id="3_arrow"]

[sub_resource type="Curve" id="Curve_cooldown"]
min_domain = 1.0
max_domain = 6.0
max_value = 10.0
_data = [Vector2(1, 0.7), 0.0, 0.0, 0, 0, Vector2(6, 0.7), 0.0, 0.0, 0, 0]
point_count = 2

[sub_resource type="Curve" id="Curve_count"]
min_domain = 1.0
max_domain = 6.0
max_value = 10.0
_data = [Vector2(1, 1), 0.0, 0.0, 0, 0, Vector2(6, 1), 0.0, 0.0, 0, 0]
point_count = 2

[sub_resource type="Curve" id="Curve_damage"]
min_domain = 1.0
max_domain = 6.0
max_value = 100.0
_data = [Vector2(1, 12), 0.0, 0.0, 0, 0, Vector2(6, 12), 0.0, 0.0, 0, 0]
point_count = 2

[resource]
script = ExtResource("1_volley")
Tag = "ability.arrow"
Cooldown = SubResource("Curve_cooldown")
AutoFire = true
ProjectileScene = ExtResource("2_scene")
Projectile = ExtResource("3_arrow")
ArrowCount = SubResource("Curve_count")
Damage = SubResource("Curve_damage")
AttackRange = 260.0
```

`port/data/abilities/dash.tres` (`OwnedTags` comes from the `DashDef` constructor):

```
[gd_resource type="Resource" format=3]

[ext_resource type="Script" path="res://port/src/Abilities/Attacks/DashDef.cs" id="1_dash"]

[sub_resource type="Curve" id="Curve_cooldown"]
min_domain = 1.0
max_domain = 6.0
max_value = 10.0
_data = [Vector2(1, 1.2), 0.0, 0.0, 0, 0, Vector2(6, 1.2), 0.0, 0.0, 0, 0]
point_count = 2

[resource]
script = ExtResource("1_dash")
Tag = "ability.dash"
Cooldown = SubResource("Curve_cooldown")
AutoFire = false
Speed = 480.0
Duration = 0.18
```

`port/data/characters/default_profile.tres` (the shop catalog comes with slice 3):

```
[gd_resource type="Resource" format=3]

[ext_resource type="Script" path="res://port/src/Defs/PlayerProfile.cs" id="1_profile"]
[ext_resource type="Script" path="res://port/src/Defs/StatBlock.cs" id="2_stats"]
[ext_resource type="Resource" path="res://port/data/characters/soldier_visual.tres" id="3_visual"]
[ext_resource type="Script" path="res://port/src/Defs/ActiveAbilityDef.cs" id="4_ability"]
[ext_resource type="Resource" path="res://port/data/abilities/arrow.tres" id="5_arrow"]
[ext_resource type="Resource" path="res://port/data/abilities/dash.tres" id="6_dash"]

[sub_resource type="Resource" id="Resource_stats"]
script = ExtResource("2_stats")
MaxHealth = 100.0
MoveSpeed = 140.0
Attack = 1.0

[resource]
script = ExtResource("1_profile")
Stats = SubResource("Resource_stats")
Visual = ExtResource("3_visual")
SlotCount = 6
InnateAbilities = Array[ExtResource("4_ability")]([ExtResource("5_arrow"), ExtResource("6_dash")])
```

`port/data/waves/stage_01_waves.tres`:

```
[gd_resource type="Resource" format=3]

[ext_resource type="Script" path="res://port/src/Defs/WaveSchedule.cs" id="1_schedule"]
[ext_resource type="Script" path="res://port/src/Defs/WaveEntry.cs" id="2_entry"]
[ext_resource type="Resource" path="res://port/data/enemies/orc.tres" id="3_orc"]
[ext_resource type="Resource" path="res://port/data/enemies/orc_swarmer.tres" id="4_swarmer"]

[sub_resource type="Resource" id="Resource_w1"]
script = ExtResource("2_entry")
Time = 30.0
Enemy = ExtResource("3_orc")
Count = 10
Label = "Orc Raid"

[sub_resource type="Resource" id="Resource_w2"]
script = ExtResource("2_entry")
Time = 60.0
Enemy = ExtResource("4_swarmer")
Count = 20
Label = "Swarm"

[sub_resource type="Resource" id="Resource_w3"]
script = ExtResource("2_entry")
Time = 90.0
Enemy = ExtResource("3_orc")
Count = 16
Label = "Orc Raid II"

[sub_resource type="Resource" id="Resource_w4"]
script = ExtResource("2_entry")
Time = 120.0
Enemy = ExtResource("4_swarmer")
Count = 28
Label = "Swarm II"

[sub_resource type="Resource" id="Resource_w5"]
script = ExtResource("2_entry")
Time = 150.0
Enemy = ExtResource("3_orc")
Count = 22
Label = "Final Raid"

[resource]
script = ExtResource("1_schedule")
Entries = Array[ExtResource("2_entry")]([SubResource("Resource_w1"), SubResource("Resource_w2"), SubResource("Resource_w3"), SubResource("Resource_w4"), SubResource("Resource_w5")])
```

`port/data/stages/stage_01.tres`:

```
[gd_resource type="Resource" format=3]

[ext_resource type="Script" path="res://port/src/Defs/StageDef.cs" id="1_stage"]
[ext_resource type="Script" path="res://port/src/Defs/StatBlock.cs" id="2_stats"]
[ext_resource type="Script" path="res://port/src/Defs/EnemyDef.cs" id="3_enemy_script"]
[ext_resource type="Texture2D" path="res://assets/art/world/grass.png" id="4_grass"]
[ext_resource type="Texture2D" path="res://assets/art/farmrpg/house.png" id="5_house"]
[ext_resource type="Resource" path="res://port/data/enemies/orc.tres" id="6_orc"]
[ext_resource type="Resource" path="res://port/data/enemies/orc_swarmer.tres" id="7_swarmer"]
[ext_resource type="Resource" path="res://port/data/waves/stage_01_waves.tres" id="8_waves"]

[sub_resource type="Resource" id="Resource_building"]
script = ExtResource("2_stats")
MaxHealth = 500.0
MoveSpeed = 0.0
Attack = 1.0

[sub_resource type="AtlasTexture" id="AtlasTexture_house"]
atlas = ExtResource("5_house")
region = Rect2(2, 2, 76, 90)

[resource]
script = ExtResource("1_stage")
Id = &"stage_01"
DisplayName = "Training Grounds"
Duration = 180.0
ArenaSize = Vector2(2400, 1600)
GroundTexture = ExtResource("4_grass")
BuildingTexture = SubResource("AtlasTexture_house")
BuildingStats = SubResource("Resource_building")
TrickleEnemies = Array[ExtResource("3_enemy_script")]([ExtResource("6_orc"), ExtResource("7_swarmer")])
TrickleIntervalStart = 2.0
TrickleIntervalEnd = 0.8
TrickleCap = 60
SpawnRadius = 520.0
Waves = ExtResource("8_waves")
StartGold = 200
StartStone = 0
DropMultiplier = 10000.0
```

- [ ] **Step 4: Run it to verify it passes**

Run: `IMPORT=1 RUNNER=cs tools/run_tests.sh`
Expected: `286 passed, 0 failed`. If a typed array (`Strips`, `Entries`, `InnateAbilities`, `TrickleEnemies`) loads empty or fails, re-save that one file from Godot to learn the exact syntax Godot writes for a C#-typed array (a throwaway `GD.Load` + `ResourceSaver.Save(res, "user://probe.tres")` in a scratch test), copy that syntax into the file, and delete the scratch code.

- [ ] **Step 5: Commit**

```bash
git add port/data port/tests/Data/DataFilesTest.cs
git commit -m "feat(data): first port data (stage_01, orcs, soldier, arrow, dash) and DataFilesTest" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 13: `DevStage` — playable dev scene with R-restart

**Files:**
- Create: `port/src/App/DevStage.cs`, `port/src/App/dev_stage.tscn`
- Test: `port/tests/App/DevStageTest.cs`

**Interfaces:**
- Consumes: `StageRoot` (Task 9), `Hud` (Task 11), `WorldHealthBar` (Task 10), the data from Task 12.
- Produces: `partial class DevStage : Node { const string StageScene, HudScene; static readonly Vector2 BuildingBarOffset = (0, -60); [Export] StageDef StageDef; [Export] PlayerProfile Profile; StageRoot? Stage; Hud? Hud; RunResult? Result; void StartRun() }`. `_Ready` starts a run; `_UnhandledInput` restarts on `InputActions.Restart` only after a result; a failed `Start` quits with exit code 1. It prints `[DevStage] {outcome} after {elapsed:0.0}s - kills {k}, gold {g}, stone {s}` on finish. Main replaces it in slice 5.

- [ ] **Step 1: Write the failing tests**

Create `port/tests/App/DevStageTest.cs`:

```csharp
using System.Threading.Tasks;
using Godot;
using VampireSurvivor.App;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.App;

public class DevStageTest : TestCase
{
	private DevStage MakeDev()
	{
		StageDef def = GD.Load<StageDef>("res://port/data/stages/stage_01.tres");
		var dev = GD.Load<PackedScene>("res://port/src/App/dev_stage.tscn").Instantiate<DevStage>();
		dev.StageDef = (StageDef)def.Duplicate();
		dev.StageDef.Duration = 30f;
		return Adopt(dev);
	}

	private static InputEventAction RestartPress() => new() { Action = InputActions.Restart, Pressed = true };

	public void TestReadyStartsARunAndBindsTheHud()
	{
		DevStage dev = MakeDev();
		Assert.NotNull(dev.Stage);
		Assert.NotNull(dev.Stage!.RunState, "the run started");
		Assert.Eq(dev.Hud!.WalletLabel.Text, "Gold 200   Stone 0   Ticket 0");
		Assert.Null(dev.Result);
	}

	public void TestRestartDuringARunDoesNothing()
	{
		DevStage dev = MakeDev();
		StageRoot first = dev.Stage!;
		dev._UnhandledInput(RestartPress());
		Assert.True(dev.Stage == first, "R is only honoured on the result screen");
	}

	public async Task TestRestartAfterAResultStartsACleanRun()
	{
		DevStage dev = MakeDev();
		StageRoot first = dev.Stage!;
		first.RunState!.Wallet.Add(CurrencyKind.Gold, 50);
		ActorFixture.Kill(this, first.Building.Entity);
		Assert.Eq(dev.Result!.Outcome, RunOutcome.Failed);
		Assert.True(dev.Hud!.ResultLabel.Visible);
		dev._UnhandledInput(RestartPress());
		Assert.True(dev.Stage != first, "a new stage");
		Assert.False(first.IsInsideTree(), "the old stage left the tree at once");
		Assert.Null(dev.Result);
		Assert.Eq(dev.Stage!.RunState!.Wallet.Gold, 200);
		Assert.False(dev.Hud!.ResultLabel.Visible);
		await NextFrame();
		Assert.False(GodotObject.IsInstanceValid(first), "the old stage was freed");
	}
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `RUNNER=cs FILTER=DevStageTest tools/run_tests.sh`
Expected: build fails — `DevStage` does not exist.

- [ ] **Step 3: Implement**

Create `port/src/App/DevStage.cs`:

```csharp
using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Stage;
using VampireSurvivor.Ui;

namespace VampireSurvivor.App;

/// <summary>
/// Temporary stage screen for the port (slice 2c): runs one stage with its HUD and the building's health bar, shows the
/// result and restarts on R. The real Main (hub, save, gem) replaces it in slice 5.
/// </summary>
public partial class DevStage : Node
{
	public const string StageScene = "res://port/src/Stage/stage.tscn";
	public const string HudScene = "res://port/src/Ui/Hud/hud.tscn";

	private static readonly Vector2 BuildingBarOffset = new(0f, -60f);

	private WorldHealthBar? _buildingBar;

	[Export]
	public StageDef StageDef { get; set; } = null!;

	[Export]
	public PlayerProfile Profile { get; set; } = null!;

	public StageRoot? Stage { get; private set; }

	public Hud? Hud { get; private set; }

	public RunResult? Result { get; private set; }

	public override void _Ready() => StartRun();

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Result is not null && @event.IsActionPressed(InputActions.Restart))
		{
			StartRun();
		}
	}

	public void StartRun()
	{
		DiscardRun();
		Result = null;
		Stage = GD.Load<PackedScene>(StageScene).Instantiate<StageRoot>();
		Hud = GD.Load<PackedScene>(HudScene).Instantiate<Hud>();
		_buildingBar = new WorldHealthBar();
		AddChild(Stage);
		AddChild(_buildingBar);
		AddChild(Hud);
		if (!Stage.Start(StageDef, Profile))
		{
			GetTree().Quit(1);
			return;
		}

		Hud.Bind(Stage.Player.Health, Stage.Building.Health, Stage.RunState!.Wallet, Stage.Clock, Stage.Waves.Timeline);
		_buildingBar.Bind(Stage.Building.Health, Stage.Building, BuildingBarOffset);
		Stage.Finished += OnFinished;
	}

	/// <summary>Removed from the tree at once (so the old enemies leave their group), freed at the end of the frame.</summary>
	private void DiscardRun()
	{
		foreach (Node? node in new Node?[] { Stage, _buildingBar, Hud })
		{
			if (node is not null && IsInstanceValid(node))
			{
				RemoveChild(node);
				node.QueueFree();
			}
		}
	}

	private void OnFinished(RunResult result)
	{
		if (Result is not null)
		{
			return;
		}

		Result = result;
		Hud!.ShowResult(result);
		GD.Print($"[DevStage] {result.Outcome} after {result.Elapsed:0.0}s - kills {result.Kills}, gold {result.Gold}, stone {result.Stone}");
	}
}
```

Create `port/src/App/dev_stage.tscn`:

```
[gd_scene load_steps=4 format=3]

[ext_resource type="Script" path="res://port/src/App/DevStage.cs" id="1_dev"]
[ext_resource type="Resource" path="res://port/data/stages/stage_01.tres" id="2_stage"]
[ext_resource type="Resource" path="res://port/data/characters/default_profile.tres" id="3_profile"]

[node name="DevStage" type="Node"]
script = ExtResource("1_dev")
StageDef = ExtResource("2_stage")
Profile = ExtResource("3_profile")
```

- [ ] **Step 4: Run them to verify they pass, then smoke-run the scene**

Run: `IMPORT=1 RUNNER=cs tools/run_tests.sh`
Expected: `289 passed, 0 failed`.

Then run the dev scene headless for ~20 s of game time and check the log:

```bash
timeout 120 "$GODOT_BIN" --headless --path . res://port/src/App/dev_stage.tscn --quit-after 1200 2>&1 | tee /tmp/dev_stage.log | tail -20
grep -E "SCRIPT ERROR|ERROR:|Unhandled exception" /tmp/dev_stage.log || echo "clean log"
```

Expected: `clean log` (the Forge editor-side import error noted in the spec, if it appears, is not from this scene; anything else is a failure to fix before committing).

- [ ] **Step 5: Commit**

```bash
git add port/src/App/DevStage.cs port/src/App/dev_stage.tscn port/tests/App/DevStageTest.cs
git commit -m "feat(app): DevStage scene - a playable C# stage with HUD and R restart" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 14: Swarm benchmark (100 / 300 enemies) and notes

**Files:**
- Create: `port/tools/SwarmBench.cs`, `port/tools/swarm_bench.tscn`
- Modify: `docs/superpowers/notes/2026-10-14-port-status-and-setup.md`, `docs/superpowers/specs/2026-10-14-csharp-forge-port-design.md` (slice 2c notes after the slice 2b notes)

**Interfaces:**
- Consumes: `StageRoot`, `EnemyFactory.Spawn`, `Player.AutoCaster`, the port data.
- Produces: a tool scene that prints `[Bench] enemies=N physics_ms=… process_ms=… fps=…` for N = 100 and 300 and quits. It is a measurement, not a test: it keeps the stage running (huge building/player health, auto-cast off, no trickle/waves) so the enemy count stays fixed.

- [ ] **Step 1: Write the tool**

Create `port/tools/SwarmBench.cs`:

```csharp
using Godot;
using VampireSurvivor.Defs;
using VampireSurvivor.Stage;

namespace VampireSurvivor.Tools;

/// <summary>
/// Measures the frame cost of N chasing enemies (spec 8.12): spawns them around the building on a stage that cannot end,
/// waits <see cref="WarmupSeconds"/>, then averages Godot's monitors over <see cref="SampleSeconds"/>. Run windowed for real
/// FPS, or headless for the physics/process cost alone.
/// </summary>
public partial class SwarmBench : Node
{
	private const float WarmupSeconds = 2f;
	private const float SampleSeconds = 5f;
	private static readonly int[] Counts = [100, 300];

	private int _countIndex;
	private StageRoot? _stage;
	private float _clock;
	private int _frames;
	private double _physicsMs;
	private double _processMs;
	private double _fps;

	public override void _Ready()
	{
		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
		StartRound();
	}

	public override void _Process(double delta)
	{
		_clock += (float)delta;
		if (_clock < WarmupSeconds)
		{
			return;
		}

		_frames++;
		_physicsMs += Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0;
		_processMs += Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0;
		_fps += Performance.GetMonitor(Performance.Monitor.TimeFps);
		if (_clock < WarmupSeconds + SampleSeconds)
		{
			return;
		}

		GD.Print($"[Bench] enemies={Counts[_countIndex]} alive={_stage!.Factory.AliveCount} physics_ms={_physicsMs / _frames:0.00} process_ms={_processMs / _frames:0.00} fps={_fps / _frames:0}");
		_countIndex++;
		if (_countIndex >= Counts.Length)
		{
			GetTree().Quit();
			return;
		}

		StartRound();
	}

	private void StartRound()
	{
		if (_stage is not null)
		{
			RemoveChild(_stage);
			_stage.QueueFree();
		}

		var def = (StageDef)GD.Load<StageDef>("res://port/data/stages/stage_01.tres").Duplicate();
		def.TrickleEnemies = [];
		def.Waves = null;
		def.Duration = 3600f;
		def.BuildingStats = new StatBlock { MaxHealth = 1_000_000f, MoveSpeed = 0f };
		var profile = (PlayerProfile)GD.Load<PlayerProfile>("res://port/data/characters/default_profile.tres").Duplicate();
		profile.Stats = new StatBlock { MaxHealth = 1_000_000f, MoveSpeed = 140f };
		_stage = GD.Load<PackedScene>("res://port/src/Stage/stage.tscn").Instantiate<StageRoot>();
		AddChild(_stage);
		_stage.Start(def, profile, 1);
		_stage.Player.AutoCaster.Enabled = false;
		EnemyDef orc = GD.Load<EnemyDef>("res://port/data/enemies/orc.tres");
		int count = Counts[_countIndex];
		for (int i = 0; i < count; i++)
		{
			float angle = Mathf.Tau * i / count;
			_stage.Factory.Spawn(orc, _stage.Building.GlobalPosition + (Vector2.Right.Rotated(angle) * (200f + (i % 5 * 60f))));
		}

		_clock = 0f;
		_frames = 0;
		_physicsMs = 0.0;
		_processMs = 0.0;
		_fps = 0.0;
	}
}
```

Create `port/tools/swarm_bench.tscn`:

```
[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://port/tools/SwarmBench.cs" id="1_bench"]

[node name="SwarmBench" type="Node"]
script = ExtResource("1_bench")
```

(`MaxHealth` 1,000,000 is 100,000,000 raw units at 2 decimal places, inside `int`.)

- [ ] **Step 2: Build, import and run it headless**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q && "$GODOT_BIN" --headless --path . --import >/dev/null 2>&1
timeout 120 "$GODOT_BIN" --headless --path . res://port/tools/swarm_bench.tscn 2>&1 | grep -E "\[Bench\]|ERROR|exception"
```

Expected: two `[Bench]` lines with `alive=100` and `alive=300` and no errors. Record the numbers.

- [ ] **Step 3: Run it windowed for real FPS**

```bash
timeout 120 "$GODOT_BIN" --path . res://port/tools/swarm_bench.tscn 2>&1 | grep -E "\[Bench\]|ERROR|exception"
```

(This opens a game window; it needs no editor.) Expected: two `[Bench]` lines. Acceptance for slice 2: at 100 enemies `physics_ms` < 8 (half a 60 Hz frame) and windowed `fps` >= 60; the GDScript Phase 1 measured 145 FPS with 100 enemies. If the 300-enemy line exceeds 16 ms of physics, note it — the spec's mitigation (pooling through `EnemyFactory`, fewer Forge ticks) is later work, not this slice's.

- [ ] **Step 4: Write the notes**

In `docs/superpowers/notes/2026-10-14-port-status-and-setup.md`: set slice 2c to **done** (plan `plans/2026-10-14-csharp-port-slice-2c-stage-hud.md`), slice 3 to **next**, update "Test totals at the end of 2c" (GDScript 648, C# 289), add the bench numbers and how to run the dev scene (`"$GODOT_BIN" --path . res://port/src/App/dev_stage.tscn`) and the bench.

In the spec, after the slice 2b notes paragraph, add a `Slice 2c notes:` paragraph covering: the composition root class is `StageRoot` (scene `stage.tscn`) because `Stage` inside namespace `VampireSurvivor.Stage` is unreachable from other namespaces (CS0118) — the same will apply to any class named like its top-level folder; `RunOutcome`/`StageState` are top-level enums; `RunResult` and `Stage.Finished` are plain C# (no Variant); `ActorEntity.Raise<T>` wraps Forge `EventData<T>` and `EventManager.Raise(in ...)`; `StageDef` lacks grade table/events/boss/gem until slices 3-5; `App/DevStage` stands in for `Main` until slice 5; HUD event/boss/ability widgets come with slices 3-4; the measured bench numbers.

- [ ] **Step 5: Commit**

```bash
git add port/tools/SwarmBench.cs port/tools/swarm_bench.tscn docs/superpowers/notes/2026-10-14-port-status-and-setup.md docs/superpowers/specs/2026-10-14-csharp-forge-port-design.md
git commit -m "chore(port): swarm benchmark and slice 2c notes" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 15: Full verification, generated files, review, merge

- [ ] **Step 1: Run everything**

Run: `IMPORT=1 tools/run_tests.sh`
Expected: GDScript `648 passed, 0 failed`; C# `289 passed, 0 failed`.

- [ ] **Step 2: Commit generated `.cs.uid` / import files**

```bash
git status --short
git add $(git status --short | awk '/\.cs\.uid$/ {print $2}')
git commit -m "chore: commit generated .cs.uid files for slice 2c" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

(Only `.cs.uid` files under `port/`; leave anything else unstaged and report it.)

- [ ] **Step 3: Final review**

Dispatch a fresh-context reviewer on the most capable model over `git diff main...port/slice-2c` with the spec and this plan. Fix Critical/Important findings test-first; list Minor ones as deferred.

- [ ] **Step 4: Merge and push**

```bash
git checkout main && git pull --ff-only && git merge --no-ff port/slice-2c -m "Merge slice 2c: stage, HUD, dev scene, first port data" && git push
```

(Ask the owner before pushing if they have not approved it in this session.)
