# C# + Forge Port — Slice 2a (Defs and Components) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port the data classes (`Defs/`) and the reusable node components (`Components/`) that the core loop needs: visuals, drops, enemies, waves, projectiles, movement, hit detection, contact damage, loot, lifetime, targeting and the animated sprite.

**Architecture:** Slice 2 of the spec is split into three plans: **2a (this plan)** Defs + Components, **2b** Actors (Player, Enemy, Building, Projectile, Pickup) with scenes and data, **2c** Stage + HUD + dev stage scene + the 100-enemy FPS measurement. 2a adds no scenes and no game behaviour you can see; everything is proven by headless tests. Components never find their neighbours: they receive them through `Bind(...)`.

**Tech Stack:** Godot 4.7.2 .NET, C# net8.0, Forge 0.4.0 behind `Gas/` and `Components/ActorEntity` (slice 1).

**Spec:** `docs/superpowers/specs/2026-10-14-csharp-forge-port-design.md` (sections 3, 4, 6, 7.3 slice 2). Slice 0/1 plan: `docs/superpowers/plans/2026-10-14-csharp-port-slices-0-1.md`; gate report `docs/superpowers/notes/2026-10-14-slice1-forge-gate.md`.

## Global Constraints

- Work on a new branch `port/slice-2a` cut from `main`. Never stage `src/app/logo.gd` or `src/app/main.gd` (uncommitted user work); always `git add` explicit paths, never `-A`/`.`.
- Commit messages end with `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>` (second `-m`).
- Tests run through `tools/run_tests.sh` (`RUNNER=cs` for the C# suite, default runs both). Use the **.NET** Godot build; the project is opened in the .NET editor. If the editor is open, `dotnet build` can fail on a locked DLL: close the editor first.
- Layer order (a file may only `using` layers below it): `Core -> Defs -> Gas -> Components -> Actors -> Stage | Abilities | Boss -> Events -> Ui -> App | Meta`. Project namespaces are never written fully qualified. `tests/Architecture/LayerRulesTest` enforces it.
- **One global class namespace:** port classes must NOT be `[GlobalClass]` when a GDScript `class_name` of the same name exists (`AnimationStrip`, `VisualDef`, `DropEntry`, `DropTable`, `ProjectileDef`, `EnemyDef`, `WaveEntry`, `WaveSchedule`, `Mover`, `Hurtbox`, `Hitbox`, `ContactDamage`, `LootDropper`, `Lifetime`, `TargetProvider`, `ActorVisual` all collide). None of this plan's classes carry `[GlobalClass]`; `GlobalClassCollisionTest` enforces it. Resources load from `.tres` by script path, so they do not need the attribute.
- Godot C# rules: a class that extends a Godot type is `partial`, and its file name equals the class name.
- `Node` classes use `[Signal]`; payloads that are not Variant-compatible (for example `DropRoll`) use a C# `event`.
- Validation returns `List<string>` (empty = OK). No balance numbers in code beyond the constants the GDScript version already had.
- Damage always goes through `DamageEffects.Hit` (slice 1). There is no `EffectApplier` in C#: `ContactDamage` calls `DamageEffects.Hit` directly. `EnemyDef.contact_effect` and `ProjectileDef.hit_effect` no longer exist.
- Test namespaces are `VampireSurvivor.Tests.<Layer>`; test files mirror `port/src`. Expected totals below assume the C# suite starts at **99** tests.

## Review Focus

1. A `VisualDef`/`AnimationStrip` with zero frames, a null texture or a width that does not divide by the frame count must be reported by `Validate()`, never thrown (Task 1).
2. A `DropTable` with a null entry, a chance outside [0, 1] or an inverted amount range must be reported by `Validate()`, and `Roll` must skip a null entry instead of crashing (Task 2).
3. `ContactDamage` must not throw when the hurtbox's entity (or its own source) was freed while the hitbox still overlaps it (Task 5).
4. `Mover.Step` called before `Bind`, or after the bound entity was freed, must be a no-op (Task 4).
5. `TargetProvider` must fall back from a freed preferred target to the other one, and return an empty list for a zero or negative count or range (Task 7).

---

## File Structure

```
port/src/Defs/{AnimationStrip,VisualDef,DropEntry,DropRoll,DropTable,ProjectileDef,EnemyDef,WaveEntry,WaveSchedule}.cs
port/src/Components/{Mover,Hurtbox,Hitbox,ContactDamage,LootDropper,Lifetime,TargetProvider,ActorVisual}.cs
port/tests/Support/DefFactory.cs              (extended: Texture, Visual, Enemy)
port/tests/Defs/{VisualDefTest,DropTableTest,ProjectileDefTest,EnemyDefTest,WaveScheduleTest}.cs
port/tests/Components/{MoverTest,HitDetectionTest,ContactDamageTest,LootDropperTest,LifetimeTest,TargetProviderTest,ActorVisualTest}.cs
```

---

### Task 1: `AnimationStrip` and `VisualDef`

**Files:**
- Create: `port/src/Defs/AnimationStrip.cs`, `port/src/Defs/VisualDef.cs`
- Modify: `port/tests/Support/DefFactory.cs` (add `Texture`, `Visual`)
- Test: `port/tests/Defs/VisualDefTest.cs`

**Interfaces:**
- Produces (namespace `VampireSurvivor.Defs`): `AnimationStrip : Resource` — `[Export] StringName Animation = AnimNames.Idle`, `Texture2D? Texture`, `int FrameCount = 1`, `float Fps = 10`, `bool Loop = true`, `List<string> Validate()`. `VisualDef : Resource` — `[Export] Godot.Collections.Array<AnimationStrip> Strips`, `Vector2 Offset`, `AnimationStrip? FindStrip(StringName)`, `SpriteFrames SpriteFrames()` (built once, shared), `List<string> Validate()`.
- Produces (tests): `DefFactory.Texture(int width = 60, int height = 10) : Texture2D`, `DefFactory.Visual() : VisualDef` (one idle strip, 60x10 texture, 6 frames).

- [ ] **Step 1: Create the branch, then write the failing test**

```bash
git switch main && git pull --ff-only && git switch -c port/slice-2a
```

`port/tests/Defs/VisualDefTest.cs`:

```csharp
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class VisualDefTest : TestCase
{
	public void TestValidVisualHasNoErrors()
	{
		Assert.NoErrors(DefFactory.Visual().Validate(), "factory visual");
	}

	public void TestMissingIdleStripIsAnError()
	{
		VisualDef def = DefFactory.Visual();
		def.Strips[0].Animation = AnimNames.Walk;
		Assert.Eq(def.Validate().Count, 1);
	}

	public void TestStripWidthMustDivideByFrameCount()
	{
		VisualDef def = DefFactory.Visual();
		def.Strips[0].FrameCount = 7;
		Assert.Eq(def.Validate().Count, 1);
	}

	public void TestZeroFrameCountIsAnErrorWithoutThrowing()
	{
		VisualDef def = DefFactory.Visual();
		def.Strips[0].FrameCount = 0;
		Assert.Eq(def.Validate().Count, 1);
	}

	public void TestNonPositiveFpsIsAnError()
	{
		VisualDef def = DefFactory.Visual();
		def.Strips[0].Fps = 0f;
		Assert.Eq(def.Validate().Count, 1);
	}

	public void TestDuplicateAnimationNamesAreReportedOncePerName()
	{
		VisualDef def = DefFactory.Visual();
		def.Strips.Add((AnimationStrip)def.Strips[0].Duplicate());
		def.Strips.Add((AnimationStrip)def.Strips[0].Duplicate());
		Assert.Eq(def.Validate().Count, 1);
	}

	public void TestNullTextureIsAnError()
	{
		VisualDef def = DefFactory.Visual();
		def.Strips[0].Texture = null;
		Assert.Eq(def.Validate().Count, 1);
	}

	public void TestFindStripReturnsNullWhenAbsent()
	{
		Assert.Null(DefFactory.Visual().FindStrip(AnimNames.Death));
	}

	public void TestSpriteFramesAreBuiltFromStripsAndShared()
	{
		VisualDef def = DefFactory.Visual();
		Godot.SpriteFrames frames = def.SpriteFrames();
		Assert.Eq(frames.GetFrameCount(AnimNames.Idle), 6);
		Assert.False(frames.HasAnimation("default"));
		Assert.True(ReferenceEquals(def.SpriteFrames(), frames), "memoised: one SpriteFrames per def");
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: a count greater than 0 (`VisualDef`, `DefFactory.Visual` missing).

- [ ] **Step 3: Implement**

`port/src/Defs/AnimationStrip.cs`:

```csharp
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;

namespace VampireSurvivor.Defs;

/// <summary>One animation stored as a horizontal strip of equally sized frames.</summary>
public partial class AnimationStrip : Resource
{
	[Export]
	public StringName Animation { get; set; } = AnimNames.Idle;

	[Export]
	public Texture2D? Texture { get; set; }

	[Export(PropertyHint.Range, "1,64")]
	public int FrameCount { get; set; } = 1;

	[Export]
	public float Fps { get; set; } = 10f;

	[Export]
	public bool Loop { get; set; } = true;

	public List<string> Validate()
	{
		var errors = new List<string>();
		if (Texture is null)
		{
			errors.Add($"AnimationStrip '{Animation}': texture is null");
		}
		else if (FrameCount < 1)
		{
			errors.Add($"AnimationStrip '{Animation}': frame_count must be >= 1");
		}
		else if (Texture.GetWidth() % FrameCount != 0)
		{
			errors.Add($"AnimationStrip '{Animation}': width {Texture.GetWidth()} is not divisible by frame_count {FrameCount}");
		}

		if (Fps <= 0f)
		{
			errors.Add($"AnimationStrip '{Animation}': fps must be > 0");
		}

		return errors;
	}
}
```

`port/src/Defs/VisualDef.cs`:

```csharp
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;

namespace VampireSurvivor.Defs;

/// <summary>The set of animations an actor can play. <see cref="SpriteFrames"/> builds and memoises the frames.</summary>
public partial class VisualDef : Resource
{
	private SpriteFrames? _frames;

	[Export]
	public Godot.Collections.Array<AnimationStrip> Strips { get; set; } = [];

	[Export]
	public Vector2 Offset { get; set; } = Vector2.Zero;

	public AnimationStrip? FindStrip(StringName animation)
	{
		foreach (AnimationStrip? strip in Strips)
		{
			if (strip is not null && strip.Animation == animation)
			{
				return strip;
			}
		}

		return null;
	}

	/// <summary>Built on first use and shared by every actor that plays this def. Call <see cref="Validate"/> first.</summary>
	public SpriteFrames SpriteFrames() => _frames ??= BuildFrames();

	public List<string> Validate()
	{
		var errors = new List<string>();
		if (FindStrip(AnimNames.Idle) is null)
		{
			errors.Add($"VisualDef: missing required '{AnimNames.Idle}' strip");
		}

		var seen = new HashSet<string>();
		var reported = new HashSet<string>();
		foreach (AnimationStrip? strip in Strips)
		{
			if (strip is null)
			{
				errors.Add("VisualDef: null strip");
				continue;
			}

			errors.AddRange(strip.Validate());
			string name = strip.Animation.ToString();
			if (seen.Contains(name) && reported.Add(name))
			{
				errors.Add($"VisualDef: duplicate strip for animation '{name}'");
			}

			seen.Add(name);
		}

		return errors;
	}

	private SpriteFrames BuildFrames()
	{
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");
		foreach (AnimationStrip strip in Strips)
		{
			frames.AddAnimation(strip.Animation);
			frames.SetAnimationLoop(strip.Animation, strip.Loop);
			frames.SetAnimationSpeed(strip.Animation, strip.Fps);
			float frameWidth = strip.Texture!.GetWidth() / (float)strip.FrameCount;
			float frameHeight = strip.Texture.GetHeight();
			for (int i = 0; i < strip.FrameCount; i++)
			{
				var frame = new AtlasTexture
				{
					Atlas = strip.Texture,
					Region = new Rect2(i * frameWidth, 0f, frameWidth, frameHeight),
				};
				frames.AddFrame(strip.Animation, frame);
			}
		}

		return frames;
	}
}
```

Add to `port/tests/Support/DefFactory.cs` (inside the class; add `using VampireSurvivor.Core;` at the top):

```csharp
	public static Texture2D Texture(int width = 60, int height = 10)
	{
		return ImageTexture.CreateFromImage(Image.CreateEmpty(width, height, false, Image.Format.Rgba8));
	}

	public static VisualDef Visual()
	{
		var strip = new AnimationStrip
		{
			Animation = AnimNames.Idle,
			Texture = Texture(60, 10),
			FrameCount = 6,
		};
		var def = new VisualDef();
		def.Strips.Add(strip);
		return def;
	}
```

- [ ] **Step 4: Run**

```bash
RUNNER=cs IMPORT=1 tools/run_tests.sh | grep -E "VisualDefTest|FAIL|  -|^----|!!"
```
Expected: nine `PASS  Defs/VisualDefTest::...` and `---- 108 passed, 0 failed ----`.

- [ ] **Step 5: Commit**

```bash
git add port/src/Defs port/tests/Support/DefFactory.cs port/tests/Defs/VisualDefTest.cs port/tests/Defs/*.uid
git commit -m "feat(defs): AnimationStrip and VisualDef" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
---

### Task 2: Drop defs

**Files:**
- Create: `port/src/Defs/DropEntry.cs`, `port/src/Defs/DropRoll.cs`, `port/src/Defs/DropTable.cs`
- Test: `port/tests/Defs/DropTableTest.cs`

**Interfaces:**
- Consumes: `CurrencyKind`, `RunRng` (Core), `DefFactory.Texture` (Task 1).
- Produces: `DropEntry : Resource` — `[Export] CurrencyKind Kind`, `Texture2D? Icon`, `float Chance = 1` (0..1), `int MinAmount = 1`, `int MaxAmount = 1`, `List<string> Validate()`. `DropRoll` (plain sealed class) — `CurrencyKind Kind`, `int Amount`, `Texture2D? Icon`. `DropTable : Resource` — `[Export] Godot.Collections.Array<DropEntry> Entries`, `List<DropRoll> Roll(RunRng rng)` (skips null entries), `List<string> Validate()`.

- [ ] **Step 1: Write the failing test**

`port/tests/Defs/DropTableTest.cs`:

```csharp
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class DropTableTest : TestCase
{
	private static DropEntry MakeEntry(CurrencyKind kind, float chance, int low, int high) => new()
	{
		Kind = kind,
		Chance = chance,
		MinAmount = low,
		MaxAmount = high,
		Icon = DefFactory.Texture(8, 8),
	};

	public void TestCertainEntryAlwaysDropsWithinRange()
	{
		var table = new DropTable();
		table.Entries.Add(MakeEntry(CurrencyKind.Gold, 1f, 2, 4));
		var rng = new RunRng(11);
		for (int i = 0; i < 20; i++)
		{
			var rolls = table.Roll(rng);
			Assert.Eq(rolls.Count, 1);
			Assert.Eq(rolls[0].Kind, CurrencyKind.Gold);
			Assert.True(rolls[0].Amount >= 2 && rolls[0].Amount <= 4);
		}
	}

	public void TestImpossibleEntryNeverDrops()
	{
		var table = new DropTable();
		table.Entries.Add(MakeEntry(CurrencyKind.Stone, 0f, 1, 1));
		Assert.Eq(table.Roll(new RunRng(1)).Count, 0);
	}

	public void TestValidateFlagsInvertedRangeAndBadChance()
	{
		var table = new DropTable();
		table.Entries.Add(MakeEntry(CurrencyKind.Gold, 1.5f, 5, 2));
		Assert.Eq(table.Validate().Count, 2);
	}

	public void TestNullEntryIsReportedAndSkippedWhenRolling()
	{
		var table = new DropTable();
		table.Entries.Add(null!);
		table.Entries.Add(MakeEntry(CurrencyKind.Gold, 1f, 1, 1));
		Assert.Eq(table.Validate().Count, 1);
		Assert.Eq(table.Roll(new RunRng(5)).Count, 1);
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: count greater than 0.

- [ ] **Step 3: Implement**

`port/src/Defs/DropEntry.cs`:

```csharp
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;

namespace VampireSurvivor.Defs;

/// <summary>One possible drop of a <see cref="DropTable"/>.</summary>
public partial class DropEntry : Resource
{
	[Export]
	public CurrencyKind Kind { get; set; } = CurrencyKind.Gold;

	[Export]
	public Texture2D? Icon { get; set; }

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float Chance { get; set; } = 1f;

	[Export]
	public int MinAmount { get; set; } = 1;

	[Export]
	public int MaxAmount { get; set; } = 1;

	public List<string> Validate()
	{
		var errors = new List<string>();
		if (Chance < 0f || Chance > 1f)
		{
			errors.Add("DropEntry: chance must be within [0, 1]");
		}

		if (MinAmount < 1 || MaxAmount < MinAmount)
		{
			errors.Add("DropEntry: need 1 <= min_amount <= max_amount");
		}

		return errors;
	}
}
```

`port/src/Defs/DropRoll.cs`:

```csharp
using Godot;
using VampireSurvivor.Core;

namespace VampireSurvivor.Defs;

/// <summary>The concrete result of rolling one <see cref="DropEntry"/>.</summary>
public sealed class DropRoll
{
	public CurrencyKind Kind { get; init; } = CurrencyKind.Gold;

	public int Amount { get; init; }

	public Texture2D? Icon { get; init; }
}
```

`port/src/Defs/DropTable.cs`:

```csharp
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;

namespace VampireSurvivor.Defs;

/// <summary>What an enemy may drop on death.</summary>
public partial class DropTable : Resource
{
	[Export]
	public Godot.Collections.Array<DropEntry> Entries { get; set; } = [];

	public List<DropRoll> Roll(RunRng rng)
	{
		var rolls = new List<DropRoll>();
		foreach (DropEntry? entry in Entries)
		{
			if (entry is null || !rng.Chance(entry.Chance))
			{
				continue;
			}

			rolls.Add(new DropRoll
			{
				Kind = entry.Kind,
				Icon = entry.Icon,
				Amount = rng.Int(entry.MinAmount, entry.MaxAmount),
			});
		}

		return rolls;
	}

	public List<string> Validate()
	{
		var errors = new List<string>();
		foreach (DropEntry? entry in Entries)
		{
			if (entry is null)
			{
				errors.Add("DropTable: null entry");
			}
			else
			{
				errors.AddRange(entry.Validate());
			}
		}

		return errors;
	}
}
```

- [ ] **Step 4: Run**

```bash
RUNNER=cs tools/run_tests.sh | grep -E "DropTableTest|FAIL|  -|^----|!!"
```
Expected: four `PASS  Defs/DropTableTest::...` and `---- 112 passed, 0 failed ----`.

- [ ] **Step 5: Commit**

```bash
git add port/src/Defs port/tests/Defs
git commit -m "feat(defs): DropEntry, DropRoll and DropTable" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `ProjectileDef`, `EnemyDef`, `WaveEntry`, `WaveSchedule`

**Files:**
- Create: `port/src/Defs/ProjectileDef.cs`, `port/src/Defs/EnemyDef.cs`, `port/src/Defs/WaveEntry.cs`, `port/src/Defs/WaveSchedule.cs`
- Modify: `port/tests/Support/DefFactory.cs` (add `Enemy`)
- Test: `port/tests/Defs/ProjectileDefTest.cs`, `port/tests/Defs/EnemyDefTest.cs`, `port/tests/Defs/WaveScheduleTest.cs`

**Interfaces:**
- Consumes: `VisualDef`, `StatBlock`, `DropTable` (Tasks 1-2 and slice 1), `PhysicsLayers`.
- Produces:
  - `enum ProjectileTeam { Enemies, Allies }`; `ProjectileDef : Resource` — `[Export] Texture2D? Texture`, `float Speed = 400`, `float Lifetime = 0.7`, `int Pierce = 0`, `float HitRadius = 6`, `ProjectileTeam Hits`, `int HitMask()`, `List<string> Validate()`.
  - `enum TargetPolicy { Building, Player }`; `EnemyDef : Resource` — `[Export] StringName Id`, `PackedScene? Scene`, `VisualDef? Visual`, `float VisualScale = 1`, `Color Tint = White`, `StatBlock? Stats`, `float ContactDamage = 8`, `float ContactInterval = 0.9`, `TargetPolicy TargetPolicy = Building`, `float BodyRadius = 10`, `DropTable? Drops`, `List<string> Validate()`.
  - `WaveEntry : Resource` — `[Export] float Time = 30`, `EnemyDef? Enemy`, `int Count = 10`, `string Label = "Wave"`. `WaveSchedule : Resource` — `[Export] Array<WaveEntry> Entries`, `List<WaveEntry> SortedEntries()`, `float[] Times()`, `List<string> ValidateAgainst(float duration)`.
  - Tests: `DefFactory.Enemy(PackedScene? scene = null) : EnemyDef` (id `test_enemy`, empty `PackedScene`, factory visual, default `StatBlock`).

- [ ] **Step 1: Write the failing tests**

`port/tests/Defs/ProjectileDefTest.cs`:

```csharp
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class ProjectileDefTest : TestCase
{
	private static ProjectileDef Valid() => new() { Texture = DefFactory.Texture(8, 8) };

	public void TestDefaultsWithATextureAreValid()
	{
		Assert.NoErrors(Valid().Validate());
	}

	public void TestHitMaskFollowsTheTeam()
	{
		ProjectileDef def = Valid();
		Assert.Eq(def.HitMask(), PhysicsLayers.EnemyHurt);
		def.Hits = ProjectileTeam.Allies;
		Assert.Eq(def.HitMask(), PhysicsLayers.AllyHurt);
	}

	public void TestMissingTextureAndNonPositiveNumbersAreReported()
	{
		var def = new ProjectileDef { Texture = null, Speed = 0f };
		Assert.Eq(def.Validate().Count, 2);
	}
}
```

`port/tests/Defs/EnemyDefTest.cs`:

```csharp
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class EnemyDefTest : TestCase
{
	public void TestFactoryEnemyIsValid()
	{
		Assert.NoErrors(DefFactory.Enemy().Validate());
	}

	public void TestEmptyIdAndNullSceneAreReported()
	{
		EnemyDef def = DefFactory.Enemy();
		def.Id = string.Empty;
		def.Scene = null;
		Assert.Eq(def.Validate().Count, 2);
	}

	public void TestNonPositiveNumbersAreOneError()
	{
		EnemyDef def = DefFactory.Enemy();
		def.BodyRadius = 0f;
		def.ContactInterval = 0f;
		Assert.Eq(def.Validate().Count, 1);
	}

	public void TestNullVisualAndStatsAreReportedAndNestedErrorsPropagate()
	{
		EnemyDef def = DefFactory.Enemy();
		def.Visual = null;
		def.Stats = new StatBlock { MaxHealth = 0f };
		Assert.Eq(def.Validate().Count, 2, "null visual + the stat block's own error");
	}
}
```

`port/tests/Defs/WaveScheduleTest.cs`:

```csharp
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class WaveScheduleTest : TestCase
{
	private static WaveEntry MakeEntry(float time, int count = 5) => new()
	{
		Time = time,
		Count = count,
		Enemy = DefFactory.Enemy(),
	};

	public void TestSortedEntriesOrdersByTime()
	{
		var schedule = new WaveSchedule();
		schedule.Entries.Add(MakeEntry(40f));
		schedule.Entries.Add(MakeEntry(10f));
		Assert.Eq(schedule.SortedEntries()[0].Time, 10f);
		Assert.SequenceEq(schedule.Times(), new[] { 10f, 40f });
	}

	public void TestWaveAfterStageEndIsAnError()
	{
		var schedule = new WaveSchedule();
		schedule.Entries.Add(MakeEntry(500f));
		Assert.Eq(schedule.ValidateAgainst(100f).Count, 1);
	}

	public void TestWaveAtExactlyTheStageEndIsAnError()
	{
		var schedule = new WaveSchedule();
		schedule.Entries.Add(MakeEntry(100f));
		Assert.Eq(schedule.ValidateAgainst(100f).Count, 1);
	}

	public void TestNonPositiveCountAndNullEnemyAreErrors()
	{
		var schedule = new WaveSchedule();
		WaveEntry badCount = MakeEntry(5f, 0);
		WaveEntry noEnemy = MakeEntry(6f);
		noEnemy.Enemy = null;
		schedule.Entries.Add(badCount);
		schedule.Entries.Add(noEnemy);
		Assert.Eq(schedule.ValidateAgainst(100f).Count, 2);
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: count greater than 0.

- [ ] **Step 3: Implement**

`port/src/Defs/ProjectileDef.cs`:

```csharp
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;

namespace VampireSurvivor.Defs;

/// <summary>Which side a projectile damages: the player's arrows hit enemies; boss bolts hit allies (player and building).</summary>
public enum ProjectileTeam
{
	Enemies,
	Allies,
}

/// <summary>Data for a straight-flying projectile.</summary>
public partial class ProjectileDef : Resource
{
	[Export]
	public Texture2D? Texture { get; set; }

	[Export]
	public float Speed { get; set; } = 400f;

	[Export]
	public float Lifetime { get; set; } = 0.7f;

	/// <summary>Extra enemies the projectile passes through (0 = stops at the first hit).</summary>
	[Export]
	public int Pierce { get; set; }

	[Export]
	public float HitRadius { get; set; } = 6f;

	[Export]
	public ProjectileTeam Hits { get; set; } = ProjectileTeam.Enemies;

	public int HitMask() => Hits == ProjectileTeam.Allies ? PhysicsLayers.AllyHurt : PhysicsLayers.EnemyHurt;

	public List<string> Validate()
	{
		var errors = new List<string>();
		if (Texture is null)
		{
			errors.Add("ProjectileDef: texture is null");
		}

		if (Speed <= 0f || Lifetime <= 0f || HitRadius <= 0f)
		{
			errors.Add("ProjectileDef: speed, lifetime and hit_radius must be > 0");
		}

		return errors;
	}
}
```

`port/src/Defs/EnemyDef.cs`:

```csharp
using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.Defs;

public enum TargetPolicy
{
	Building,
	Player,
}

/// <summary>Everything that distinguishes one enemy type; the actor scene itself is generic.</summary>
public partial class EnemyDef : Resource
{
	[Export]
	public StringName Id { get; set; } = string.Empty;

	[Export]
	public PackedScene? Scene { get; set; }

	[Export]
	public VisualDef? Visual { get; set; }

	[Export]
	public float VisualScale { get; set; } = 1f;

	[Export]
	public Color Tint { get; set; } = Colors.White;

	[Export]
	public StatBlock? Stats { get; set; }

	[Export]
	public float ContactDamage { get; set; } = 8f;

	[Export]
	public float ContactInterval { get; set; } = 0.9f;

	[Export]
	public TargetPolicy TargetPolicy { get; set; } = TargetPolicy.Building;

	[Export]
	public float BodyRadius { get; set; } = 10f;

	[Export]
	public DropTable? Drops { get; set; }

	public List<string> Validate()
	{
		var errors = new List<string>();
		string id = Id.ToString();
		if (id.Length == 0)
		{
			errors.Add("EnemyDef: id is empty");
		}

		if (Scene is null)
		{
			errors.Add($"EnemyDef '{id}': scene is null");
		}

		if (BodyRadius <= 0f || ContactDamage < 0f || ContactInterval <= 0f || VisualScale <= 0f)
		{
			errors.Add($"EnemyDef '{id}': body_radius, contact_interval and visual_scale must be > 0, contact_damage >= 0");
		}

		if (Visual is null)
		{
			errors.Add($"EnemyDef '{id}': visual is null");
		}
		else
		{
			errors.AddRange(Visual.Validate());
		}

		if (Stats is null)
		{
			errors.Add($"EnemyDef '{id}': stats is null");
		}
		else
		{
			errors.AddRange(Stats.Validate());
		}

		if (Drops is not null)
		{
			errors.AddRange(Drops.Validate());
		}

		return errors;
	}
}
```

`port/src/Defs/WaveEntry.cs`:

```csharp
using Godot;

namespace VampireSurvivor.Defs;

/// <summary>One surge: <see cref="Count"/> enemies of one type arrive at stage time <see cref="Time"/>.</summary>
public partial class WaveEntry : Resource
{
	[Export]
	public float Time { get; set; } = 30f;

	[Export]
	public EnemyDef? Enemy { get; set; }

	[Export]
	public int Count { get; set; } = 10;

	[Export]
	public string Label { get; set; } = "Wave";
}
```

`port/src/Defs/WaveSchedule.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace VampireSurvivor.Defs;

/// <summary>Ordered surges of a stage. Entries may be authored in any order.</summary>
public partial class WaveSchedule : Resource
{
	[Export]
	public Godot.Collections.Array<WaveEntry> Entries { get; set; } = [];

	public List<WaveEntry> SortedEntries()
	{
		return [.. Entries.Where(e => e is not null).OrderBy(e => e.Time)];
	}

	public float[] Times() => [.. SortedEntries().Select(e => e.Time)];

	public List<string> ValidateAgainst(float duration)
	{
		var errors = new List<string>();
		foreach (WaveEntry? entry in Entries)
		{
			if (entry is null)
			{
				errors.Add("WaveSchedule: null entry");
				continue;
			}

			if (entry.Enemy is null)
			{
				errors.Add($"WaveEntry '{entry.Label}': enemy is null");
			}
			else
			{
				errors.AddRange(entry.Enemy.Validate());
			}

			if (entry.Count <= 0)
			{
				errors.Add($"WaveEntry '{entry.Label}': count must be > 0");
			}

			if (entry.Time < 0f || entry.Time >= duration)
			{
				errors.Add($"WaveEntry '{entry.Label}': time {entry.Time} is outside [0, stage duration {duration})");
			}
		}

		return errors;
	}
}
```

Add to `port/tests/Support/DefFactory.cs`:

```csharp
	public static EnemyDef Enemy(PackedScene? scene = null) => new()
	{
		Id = "test_enemy",
		Scene = scene ?? new PackedScene(),
		Visual = Visual(),
		Stats = new StatBlock(),
	};
```

- [ ] **Step 4: Run**

```bash
RUNNER=cs tools/run_tests.sh | grep -E "ProjectileDefTest|EnemyDefTest|WaveScheduleTest|FAIL|  -|^----|!!"
```
Expected: three + four + four `PASS` lines and `---- 123 passed, 0 failed ----`.

- [ ] **Step 5: Commit**

```bash
git add port/src/Defs port/tests/Support/DefFactory.cs port/tests/Defs
git commit -m "feat(defs): ProjectileDef, EnemyDef, WaveEntry and WaveSchedule" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `Mover`, `Hurtbox`, `Hitbox`

**Files:**
- Create: `port/src/Components/Mover.cs`, `port/src/Components/Hurtbox.cs`, `port/src/Components/Hitbox.cs`
- Test: `port/tests/Components/MoverTest.cs`, `port/tests/Components/HitDetectionTest.cs`

**Interfaces:**
- Consumes: `ActorEntity`, `Stats.MoveSpeed`, `ActorFixture.BareActor` (slice 1), `PhysicsLayers`.
- Produces (namespace `VampireSurvivor.Components`):
  - `Mover : Node` — `Bind(CharacterBody2D body, ActorEntity entity)`, `SetDirection(Vector2)`, `Vector2 Direction`, `Vector2 LastDirection`, `bool IsDashing`, `bool IsMoving`, `Dash(Vector2 direction, float speed, float duration)`, `CancelDash()`, `Step(double delta)` (also from `_PhysicsProcess`); signals `DashStarted`, `DashFinished`. Speed is the entity's `Stats.MoveSpeed` display value.
  - `Hurtbox : Area2D` — `ActorEntity? Entity`, `Node2D? Actor`, `Bind(ActorEntity entity, Node2D actor)`. Scene setup: `collision_layer` = owner's hurt layer, `collision_mask = 0`, `monitoring = false`.
  - `Hitbox : Area2D` — `[Export] bool OncePerTarget = true`, signal `HurtboxEntered(Hurtbox)`, `Reset()`, `List<Hurtbox> OverlappingHurtboxes()`. Scene setup: `collision_layer = 0`, `collision_mask` = target hurt layer, `monitorable = false`.

- [ ] **Step 1: Write the failing tests**

`port/tests/Components/MoverTest.cs`:

```csharp
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class MoverTest : TestCase
{
	private (CharacterBody2D Body, Mover Mover, ActorEntity Entity) Rig(float speed)
	{
		ActorEntity entity = ActorFixture.BareActor(this, new StatBlock { MoveSpeed = speed });
		var body = Adopt(new CharacterBody2D());
		var mover = Adopt(new Mover());
		mover.SetPhysicsProcess(false);
		mover.Bind(body, entity);
		return (body, mover, entity);
	}

	public void TestVelocityFollowsDirectionAndEntitySpeed()
	{
		var (body, mover, _) = Rig(100f);
		mover.SetDirection(Vector2.Right);
		mover.Step(0.1);
		Assert.Eq(body.Velocity, new Vector2(100, 0));
		mover.SetDirection(Vector2.Zero);
		mover.Step(0.1);
		Assert.Eq(body.Velocity, Vector2.Zero);
	}

	public void TestDiagonalInputIsNotFaster()
	{
		var (body, mover, _) = Rig(100f);
		mover.SetDirection(new Vector2(1, 1));
		mover.Step(0.1);
		Assert.AlmostEq(body.Velocity.Length(), 100.0, 0.01);
	}

	public void TestDashOverridesVelocityThenFinishes()
	{
		var (body, mover, _) = Rig(100f);
		var finished = new List<bool>();
		mover.DashFinished += () => finished.Add(true);
		mover.SetDirection(Vector2.Up);
		mover.Dash(Vector2.Right, 400f, 0.2f);
		Assert.True(mover.IsDashing);
		mover.Step(0.1);
		Assert.Eq(body.Velocity, new Vector2(400, 0));
		mover.Step(0.15);
		Assert.False(mover.IsDashing);
		Assert.Eq(finished.Count, 1);
		mover.Step(0.1);
		Assert.Eq(body.Velocity, new Vector2(0, -100), "back to walking");
	}

	public void TestDashWithoutDirectionUsesTheLastDirection()
	{
		var (body, mover, _) = Rig(100f);
		mover.SetDirection(Vector2.Left);
		mover.SetDirection(Vector2.Zero);
		mover.Dash(Vector2.Zero, 300f, 0.2f);
		mover.Step(0.05);
		Assert.Eq(body.Velocity, new Vector2(-300, 0));
	}

	public void TestStepBeforeBindOrAfterTheEntityWasFreedIsANoOp()
	{
		var unbound = Adopt(new Mover());
		unbound.SetPhysicsProcess(false);
		unbound.Step(0.1);

		var (body, mover, entity) = Rig(100f);
		mover.SetDirection(Vector2.Right);
		entity.Free();
		mover.Step(0.1);
		Assert.Eq(body.Velocity, Vector2.Zero);
	}
}
```

`port/tests/Components/HitDetectionTest.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;

namespace VampireSurvivor.Tests.Components;

public class HitDetectionTest : TestCase
{
	private T MakeArea<T>(T area, int layer, int mask)
		where T : Area2D
	{
		area.CollisionLayer = (uint)layer;
		area.CollisionMask = (uint)mask;
		var shape = new CollisionShape2D { Shape = new CircleShape2D { Radius = 10f } };
		area.AddChild(shape);
		return Adopt(area);
	}

	private (Hitbox Hitbox, Hurtbox Hurtbox) Pair()
	{
		Hitbox hitbox = MakeArea(new Hitbox(), 0, PhysicsLayers.EnemyHurt);
		Hurtbox hurtbox = MakeArea(new Hurtbox(), PhysicsLayers.EnemyHurt, 0);
		return (hitbox, hurtbox);
	}

	public async Task TestHitboxReportsOverlappingHurtbox()
	{
		var (hitbox, hurtbox) = Pair();
		var entered = new List<Hurtbox>();
		hitbox.HurtboxEntered += found => entered.Add(found);
		await Settle();
		Assert.SequenceEq(entered, new[] { hurtbox });
		Assert.SequenceEq(hitbox.OverlappingHurtboxes(), new[] { hurtbox });
	}

	public async Task TestOncePerTargetIgnoresReentryUntilReset()
	{
		var (hitbox, hurtbox) = Pair();
		var entered = new List<Hurtbox>();
		hitbox.HurtboxEntered += found => entered.Add(found);
		await Settle();
		hurtbox.Position = new Vector2(1000, 0);
		await Settle();
		hurtbox.Position = Vector2.Zero;
		await Settle();
		Assert.Eq(entered.Count, 1, "re-entry ignored");
		hurtbox.Position = new Vector2(1000, 0);
		await Settle();
		hitbox.Reset();
		hurtbox.Position = Vector2.Zero;
		await Settle();
		Assert.Eq(entered.Count, 2, "re-entry counted after reset");
	}

	public async Task TestNonHurtboxAreasAreIgnored()
	{
		Hitbox hitbox = MakeArea(new Hitbox(), 0, PhysicsLayers.EnemyHurt);
		MakeArea(new Area2D(), PhysicsLayers.EnemyHurt, 0);
		await Settle();
		Assert.Eq(hitbox.OverlappingHurtboxes().Count, 0);
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: count greater than 0.

- [ ] **Step 3: Implement**

`port/src/Components/Mover.cs`:

```csharp
using Godot;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Components;

/// <summary>Drives a CharacterBody2D at the entity's move speed, with a dash override.</summary>
public partial class Mover : Node
{
	[Signal]
	public delegate void DashStartedEventHandler();

	[Signal]
	public delegate void DashFinishedEventHandler();

	private CharacterBody2D? _body;
	private ActorEntity? _entity;
	private Vector2 _direction = Vector2.Zero;
	private Vector2 _dashVelocity = Vector2.Zero;
	private double _dashTimeLeft;

	public Vector2 LastDirection { get; private set; } = Vector2.Right;

	public Vector2 Direction => _direction;

	public bool IsDashing => _dashTimeLeft > 0.0;

	public bool IsMoving => _direction != Vector2.Zero || IsDashing;

	public void Bind(CharacterBody2D body, ActorEntity entity)
	{
		_body = body;
		_entity = entity;
	}

	public void SetDirection(Vector2 heading)
	{
		_direction = heading.LimitLength(1f);
		if (_direction != Vector2.Zero)
		{
			LastDirection = _direction.Normalized();
		}
	}

	public void Dash(Vector2 dashDirection, float speed, float duration)
	{
		Vector2 heading = dashDirection != Vector2.Zero ? dashDirection.Normalized() : LastDirection;
		_dashVelocity = heading * speed;
		_dashTimeLeft = duration;
		EmitSignal(SignalName.DashStarted);
	}

	/// <summary>Ends a running dash at once; normal movement resumes next step.</summary>
	public void CancelDash()
	{
		if (_dashTimeLeft <= 0.0)
		{
			return;
		}

		_dashTimeLeft = 0.0;
		EmitSignal(SignalName.DashFinished);
	}

	public void Step(double delta)
	{
		if (!IsInstanceValid(_body) || !IsInstanceValid(_entity))
		{
			return;
		}

		if (_dashTimeLeft > 0.0)
		{
			_dashTimeLeft -= delta;
			_body!.Velocity = _dashVelocity;
			if (_dashTimeLeft <= 0.0)
			{
				EmitSignal(SignalName.DashFinished);
			}
		}
		else
		{
			_body!.Velocity = _direction * _entity!.Attributes[Stats.MoveSpeed].DisplayValue;
		}

		_body.MoveAndSlide();
	}

	public override void _PhysicsProcess(double delta) => Step(delta);
}
```

`port/src/Components/Hurtbox.cs`:

```csharp
using Godot;

namespace VampireSurvivor.Components;

/// <summary>
/// Hit-receiving area. Knows which entity and actor it protects; holds no behaviour.
/// Configure in the scene: collision_layer = the owner's hurt layer, collision_mask = 0, monitoring = false.
/// </summary>
public partial class Hurtbox : Area2D
{
	public ActorEntity? Entity { get; private set; }

	public Node2D? Actor { get; private set; }

	public void Bind(ActorEntity entity, Node2D actor)
	{
		Entity = entity;
		Actor = actor;
	}
}
```

`port/src/Components/Hitbox.cs`:

```csharp
using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.Components;

/// <summary>
/// Hit-detecting area. Configure in the scene: collision_layer = 0, collision_mask = the target's hurt layer,
/// monitorable = false.
/// </summary>
public partial class Hitbox : Area2D
{
	private readonly HashSet<ulong> _alreadyHit = [];

	[Signal]
	public delegate void HurtboxEnteredEventHandler(Hurtbox hurtbox);

	/// <summary>When true, each hurtbox is reported once until <see cref="Reset"/> (projectiles). Contact damage polls instead.</summary>
	[Export]
	public bool OncePerTarget { get; set; } = true;

	public override void _Ready() => AreaEntered += OnAreaEntered;

	public void Reset() => _alreadyHit.Clear();

	public List<Hurtbox> OverlappingHurtboxes()
	{
		var result = new List<Hurtbox>();
		foreach (Area2D area in GetOverlappingAreas())
		{
			if (area is Hurtbox hurtbox)
			{
				result.Add(hurtbox);
			}
		}

		return result;
	}

	private void OnAreaEntered(Area2D area)
	{
		if (area is not Hurtbox hurtbox)
		{
			return;
		}

		if (OncePerTarget && !_alreadyHit.Add(hurtbox.GetInstanceId()))
		{
			return;
		}

		EmitSignal(SignalName.HurtboxEntered, hurtbox);
	}
}
```

- [ ] **Step 4: Run**

```bash
RUNNER=cs IMPORT=1 tools/run_tests.sh | grep -E "MoverTest|HitDetectionTest|FAIL|  -|^----|!!"
```
Expected: five + three `PASS` lines and `---- 131 passed, 0 failed ----`.

- [ ] **Step 5: Commit**

```bash
git add port/src/Components port/tests/Components
git commit -m "feat(components): Mover, Hurtbox and Hitbox" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 5: `ContactDamage`

**Files:**
- Create: `port/src/Components/ContactDamage.cs`
- Test: `port/tests/Components/ContactDamageTest.cs`

**Interfaces:**
- Consumes: `Hitbox`, `Hurtbox`, `ActorEntity`, `DamageEffects.Hit`, `Stats.Health`.
- Produces: `ContactDamage : Node` — `Bind(Hitbox hitbox, ActorEntity source, float damage, float interval)`, `Step(double delta)` (also `_PhysicsProcess`), signal `Attacked(Godot.Collections.Array<Hurtbox> targets)`. Damages every overlapping hurtbox's entity once per `interval`; skips freed entities.

- [ ] **Step 1: Write the failing test**

`port/tests/Components/ContactDamageTest.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class ContactDamageTest : TestCase
{
	private T MakeArea<T>(T area, int layer, int mask)
		where T : Area2D
	{
		area.CollisionLayer = (uint)layer;
		area.CollisionMask = (uint)mask;
		area.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 10f } });
		return Adopt(area);
	}

	private (ContactDamage Contact, ActorEntity Attacker, ActorEntity Victim, Hurtbox Hurtbox) Rig()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, new StatBlock());
		ActorEntity victim = ActorFixture.BareActor(this, new StatBlock());
		Hitbox hitbox = MakeArea(new Hitbox(), 0, PhysicsLayers.AllyHurt);
		hitbox.OncePerTarget = false;
		Hurtbox hurtbox = MakeArea(new Hurtbox(), PhysicsLayers.AllyHurt, 0);
		hurtbox.Bind(victim, hurtbox);
		var contact = Adopt(new ContactDamage());
		contact.SetPhysicsProcess(false);
		contact.Bind(hitbox, attacker, 10f, 1f);
		return (contact, attacker, victim, hurtbox);
	}

	private static double HealthOf(ActorEntity entity) => entity.Attributes[Stats.Health].DisplayValue;

	public async Task TestDamagesOverlappingHurtboxThenWaitsForTheInterval()
	{
		var (contact, _, victim, _) = Rig();
		await Settle();
		var attacks = new List<int>();
		contact.Attacked += targets => attacks.Add(targets.Count);
		contact.Step(0.0);
		Assert.AlmostEq(HealthOf(victim), 90.0);
		contact.Step(0.5);
		Assert.AlmostEq(HealthOf(victim), 90.0, 0.001, "still cooling down");
		contact.Step(0.6);
		Assert.AlmostEq(HealthOf(victim), 80.0);
		Assert.SequenceEq(attacks, new[] { 1, 1 });
	}

	public async Task TestNothingHappensWithoutOverlap()
	{
		var (contact, _, victim, hurtbox) = Rig();
		hurtbox.Position = new Vector2(1000, 0);
		await Settle();
		contact.Step(0.0);
		Assert.AlmostEq(HealthOf(victim), 100.0);
	}

	public async Task TestAFreedVictimOrSourceDoesNotThrow()
	{
		var (contact, attacker, victim, _) = Rig();
		await Settle();
		victim.Free();
		contact.Step(0.0);

		var (contact2, attacker2, victim2, _) = Rig();
		await Settle();
		attacker2.Free();
		contact2.Step(0.0);
		Assert.AlmostEq(HealthOf(victim2), 100.0, 0.001, "a freed source deals no damage");
		Assert.True(GodotObject.IsInstanceValid(attacker));
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: count greater than 0.

- [ ] **Step 3: Implement**

`port/src/Components/ContactDamage.cs`:

```csharp
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Components;

/// <summary>Periodically damages every hurtbox overlapping a hitbox (enemy melee, hazards).</summary>
public partial class ContactDamage : Node
{
	private Hitbox? _hitbox;
	private ActorEntity? _source;
	private float _damage;
	private float _interval = 1f;
	private double _cooldown;

	[Signal]
	public delegate void AttackedEventHandler(Godot.Collections.Array<Hurtbox> targets);

	public void Bind(Hitbox hitbox, ActorEntity source, float damage, float interval)
	{
		_hitbox = hitbox;
		_source = source;
		_damage = damage;
		_interval = interval;
	}

	public void Step(double delta)
	{
		_cooldown = System.Math.Max(_cooldown - delta, 0.0);
		if (_cooldown > 0.0 || !IsInstanceValid(_hitbox) || !IsInstanceValid(_source))
		{
			return;
		}

		List<Hurtbox> targets = _hitbox!.OverlappingHurtboxes();
		if (targets.Count == 0)
		{
			return;
		}

		foreach (Hurtbox target in targets)
		{
			if (target.Entity is { } entity && IsInstanceValid(entity))
			{
				DamageEffects.Hit(_source!, entity, _damage);
			}
		}

		_cooldown = _interval;
		EmitSignal(SignalName.Attacked, new Godot.Collections.Array<Hurtbox>(targets));
	}

	public override void _PhysicsProcess(double delta) => Step(delta);
}
```

- [ ] **Step 4: Run**

```bash
RUNNER=cs tools/run_tests.sh | grep -E "ContactDamageTest|FAIL|  -|^----|!!"
```
Expected: three `PASS` lines and `---- 134 passed, 0 failed ----`.

- [ ] **Step 5: Commit**

```bash
git add port/src/Components port/tests/Components
git commit -m "feat(components): ContactDamage" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 6: `LootDropper`

**Files:**
- Create: `port/src/Components/LootDropper.cs`
- Test: `port/tests/Components/LootDropperTest.cs`

**Interfaces:**
- Consumes: `Health` (slice 1), `DropTable`, `DropRoll`, `RunRng`, `DamageEffects.Hit`.
- Produces: `LootDropper : Node` — `const float ScatterRadius = 10`, `Bind(Health health, DropTable? table, RunRng rng, Node2D anchor)`, C# `event Action<DropRoll, Vector2> DropRequested` (a `DropRoll` is not Variant-compatible, so not a `[Signal]`). On the health's `Died` it rolls the table and requests each drop at `anchor.GlobalPosition + rng.OffsetInCircle(ScatterRadius)`.

- [ ] **Step 1: Write the failing test**

`port/tests/Components/LootDropperTest.cs`:

```csharp
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class LootDropperTest : TestCase
{
	private static DropTable MakeTable(float chance)
	{
		var table = new DropTable();
		table.Entries.Add(new DropEntry { Kind = CurrencyKind.Gold, Chance = chance, MinAmount = 3, MaxAmount = 3 });
		return table;
	}

	private List<(DropRoll Roll, Vector2 Position)> Kill(DropTable? table, bool anchorFreedBeforeDeath = false)
	{
		var stats = new StatBlock { MaxHealth = 10f };
		ActorEntity killer = ActorFixture.BareActor(this, stats);
		ActorEntity victim = ActorFixture.BareActor(this, stats);
		var anchor = Adopt(new Node2D { Position = new Vector2(40, 60) });
		var health = new Health();
		victim.AddChild(health);
		health.Bind(victim);
		var dropper = new LootDropper();
		victim.AddChild(dropper);
		dropper.Bind(health, table, new RunRng(3), anchor);
		var drops = new List<(DropRoll, Vector2)>();
		dropper.DropRequested += (roll, position) => drops.Add((roll, position));
		if (anchorFreedBeforeDeath)
		{
			anchor.Free();
		}

		DamageEffects.Hit(killer, victim, 999f);
		return drops;
	}

	public void TestDeathRequestsTheRolledDropsAtTheAnchor()
	{
		var drops = Kill(MakeTable(1f));
		Assert.Eq(drops.Count, 1);
		Assert.Eq(drops[0].Roll.Amount, 3);
		Assert.True(drops[0].Position.DistanceTo(new Vector2(40, 60)) <= 12f, "scattered near the anchor");
	}

	public void TestNoTableMeansNoDrops()
	{
		Assert.Eq(Kill(null).Count, 0);
	}

	public void TestFailedChanceMeansNoDrops()
	{
		Assert.Eq(Kill(MakeTable(0f)).Count, 0);
	}

	public void TestAFreedAnchorMeansNoDropsAndNoException()
	{
		Assert.Eq(Kill(MakeTable(1f), anchorFreedBeforeDeath: true).Count, 0);
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: count greater than 0.

- [ ] **Step 3: Implement**

`port/src/Components/LootDropper.cs`:

```csharp
using System;
using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Components;

/// <summary>Rolls an actor's <see cref="DropTable"/> when it dies and asks the world to spawn the pickups.</summary>
public partial class LootDropper : Node
{
	public const float ScatterRadius = 10f;

	private DropTable? _table;
	private RunRng? _rng;
	private Node2D? _anchor;

	public event Action<DropRoll, Vector2>? DropRequested;

	public void Bind(Health health, DropTable? table, RunRng rng, Node2D anchor)
	{
		_table = table;
		_rng = rng;
		_anchor = anchor;
		health.Died += OnDied;
	}

	private void OnDied()
	{
		if (_table is null || _rng is null || !IsInstanceValid(_anchor))
		{
			return;
		}

		foreach (DropRoll roll in _table.Roll(_rng))
		{
			DropRequested?.Invoke(roll, _anchor!.GlobalPosition + _rng.OffsetInCircle(ScatterRadius));
		}
	}
}
```

- [ ] **Step 4: Run**

```bash
RUNNER=cs tools/run_tests.sh | grep -E "LootDropperTest|FAIL|  -|^----|!!"
```
Expected: four `PASS` lines and `---- 138 passed, 0 failed ----`.

- [ ] **Step 5: Commit**

```bash
git add port/src/Components port/tests/Components
git commit -m "feat(components): LootDropper" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 7: `Lifetime` and `TargetProvider`

**Files:**
- Create: `port/src/Components/Lifetime.cs`, `port/src/Components/TargetProvider.cs`
- Test: `port/tests/Components/LifetimeTest.cs`, `port/tests/Components/TargetProviderTest.cs`

**Interfaces:**
- Consumes: `Groups.Enemies`, `TargetPolicy` (Task 3).
- Produces:
  - `Lifetime : Node` — `[Export] float Seconds = 1`, `Restart(float seconds)`, `Step(double delta)` (also `_PhysicsProcess`), signal `Expired` (emitted once per arming).
  - `TargetProvider : Node` — `Node2D? Building`, `Node2D? Player`, `Node2D? TargetFor(TargetPolicy)`, `Node2D? NearestEnemy(Vector2 origin, float maxRange)`, `List<Node2D> NearestEnemies(Vector2 origin, float maxRange, int count)` (nearest first; empty for `count <= 0`).

- [ ] **Step 1: Write the failing tests**

`port/tests/Components/LifetimeTest.cs`:

```csharp
using VampireSurvivor.Components;

namespace VampireSurvivor.Tests.Components;

public class LifetimeTest : TestCase
{
	public void TestExpiresOnceWhenTimeRunsOut()
	{
		var lifetime = Adopt(new Lifetime());
		lifetime.SetPhysicsProcess(false);
		int expired = 0;
		lifetime.Expired += () => expired++;
		lifetime.Restart(1f);
		lifetime.Step(0.5);
		Assert.Eq(expired, 0);
		lifetime.Step(0.6);
		lifetime.Step(5.0);
		Assert.Eq(expired, 1);
	}

	public void TestRestartRearmsTheCountdown()
	{
		var lifetime = Adopt(new Lifetime());
		lifetime.SetPhysicsProcess(false);
		int expired = 0;
		lifetime.Expired += () => expired++;
		lifetime.Restart(0.1f);
		lifetime.Step(0.2);
		lifetime.Restart(0.1f);
		lifetime.Step(0.2);
		Assert.Eq(expired, 2);
	}
}
```

`port/tests/Components/TargetProviderTest.cs`:

```csharp
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Tests.Components;

public class TargetProviderTest : TestCase
{
	private TargetProvider MakeProvider() => Adopt(new TargetProvider());

	private Node2D MakeEnemy(Vector2 at)
	{
		var node = Adopt(new Node2D());
		node.AddToGroup(Groups.Enemies);
		node.GlobalPosition = at;
		return node;
	}

	public void TestNearestEnemyPicksTheClosestWithinRange()
	{
		TargetProvider provider = MakeProvider();
		Node2D far = MakeEnemy(new Vector2(200, 0));
		Node2D near = MakeEnemy(new Vector2(50, 0));
		Assert.True(provider.NearestEnemy(Vector2.Zero, 300f) == near);
		Assert.True(provider.NearestEnemy(Vector2.Zero, 100f) == near);
		far.QueueFree();
	}

	public void TestNearestEnemyIsNullWhenNothingIsInRange()
	{
		TargetProvider provider = MakeProvider();
		MakeEnemy(new Vector2(500, 0));
		Assert.Null(provider.NearestEnemy(Vector2.Zero, 100f));
	}

	public void TestNearestEnemySkipsQueuedForDeletion()
	{
		TargetProvider provider = MakeProvider();
		Node2D doomed = MakeEnemy(new Vector2(10, 0));
		Node2D other = MakeEnemy(new Vector2(80, 0));
		doomed.QueueFree();
		Assert.True(provider.NearestEnemy(Vector2.Zero, 300f) == other);
	}

	public void TestNearestEnemySkipsNodesThatLeftTheGroup()
	{
		TargetProvider provider = MakeProvider();
		Node2D dying = MakeEnemy(new Vector2(10, 0));
		dying.RemoveFromGroup(Groups.Enemies);
		Assert.Null(provider.NearestEnemy(Vector2.Zero, 300f));
	}

	public void TestTargetForPrefersThePolicyTargetThenFallsBack()
	{
		TargetProvider provider = MakeProvider();
		var building = Adopt(new Node2D());
		var player = Adopt(new Node2D());
		provider.Building = building;
		provider.Player = player;
		Assert.True(provider.TargetFor(TargetPolicy.Building) == building);
		Assert.True(provider.TargetFor(TargetPolicy.Player) == player);
		player.Free();
		Assert.True(provider.TargetFor(TargetPolicy.Player) == building);
		building.Free();
		Assert.Null(provider.TargetFor(TargetPolicy.Building));
	}

	public void TestNearestEnemiesReturnsUpToCountSortedByDistance()
	{
		TargetProvider provider = MakeProvider();
		Node2D fifty = MakeEnemy(new Vector2(50, 0));
		MakeEnemy(new Vector2(150, 0));
		MakeEnemy(new Vector2(400, 0));
		Node2D ninety = MakeEnemy(new Vector2(0, 90));
		var two = provider.NearestEnemies(Vector2.Zero, 300f, 2);
		Assert.Eq(two.Count, 2);
		Assert.True(two[0] == fifty);
		Assert.True(two[1] == ninety);
		Assert.Eq(provider.NearestEnemies(Vector2.Zero, 300f, 10).Count, 3, "the one at 400 is out of range");
	}

	public void TestNonPositiveCountOrRangeYieldsNothing()
	{
		TargetProvider provider = MakeProvider();
		MakeEnemy(new Vector2(10, 0));
		Assert.Eq(provider.NearestEnemies(Vector2.Zero, 300f, 0).Count, 0);
		Assert.Eq(provider.NearestEnemies(Vector2.Zero, 300f, -3).Count, 0);
		Assert.Eq(provider.NearestEnemies(Vector2.Zero, 0f, 5).Count, 0, "a zero range reaches no one standing off-origin");
		Assert.Null(provider.NearestEnemy(Vector2.Zero, -5f));
	}

	public void TestNearestEnemiesSkipsDoomedNodes()
	{
		TargetProvider provider = MakeProvider();
		Node2D doomed = MakeEnemy(new Vector2(10, 0));
		Node2D other = MakeEnemy(new Vector2(80, 0));
		doomed.QueueFree();
		Assert.SequenceEq(provider.NearestEnemies(Vector2.Zero, 300f, 5), new[] { other });
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: count greater than 0.

- [ ] **Step 3: Implement**

`port/src/Components/Lifetime.cs`:

```csharp
using Godot;

namespace VampireSurvivor.Components;

/// <summary>Counts down and announces expiry; the owner decides what to do about it.</summary>
public partial class Lifetime : Node
{
	private double _remaining;
	private bool _hasExpired;

	[Signal]
	public delegate void ExpiredEventHandler();

	[Export]
	public float Seconds { get; set; } = 1f;

	public override void _Ready() => _remaining = Seconds;

	public void Restart(float newSeconds)
	{
		Seconds = newSeconds;
		_remaining = newSeconds;
		_hasExpired = false;
	}

	public void Step(double delta)
	{
		if (_hasExpired)
		{
			return;
		}

		_remaining -= delta;
		if (_remaining <= 0.0)
		{
			_hasExpired = true;
			EmitSignal(SignalName.Expired);
		}
	}

	public override void _PhysicsProcess(double delta) => Step(delta);
}
```

`port/src/Components/TargetProvider.cs`:

```csharp
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Components;

/// <summary>Answers targeting questions: who an enemy should chase, who an ability should shoot.</summary>
public partial class TargetProvider : Node
{
	public Node2D? Building { get; set; }

	public Node2D? Player { get; set; }

	public Node2D? TargetFor(TargetPolicy policy)
	{
		Node2D? preferred = policy == TargetPolicy.Player ? Player : Building;
		Node2D? fallback = policy == TargetPolicy.Player ? Building : Player;
		if (IsInstanceValid(preferred))
		{
			return preferred;
		}

		return IsInstanceValid(fallback) ? fallback : null;
	}

	public Node2D? NearestEnemy(Vector2 origin, float maxRange)
	{
		List<Node2D> nearest = NearestEnemies(origin, maxRange, 1);
		return nearest.Count > 0 ? nearest[0] : null;
	}

	/// <summary>Up to <paramref name="count"/> living enemies within <paramref name="maxRange"/>, nearest first.</summary>
	public List<Node2D> NearestEnemies(Vector2 origin, float maxRange, int count)
	{
		var found = new List<Node2D>();
		if (count <= 0 || maxRange < 0f)
		{
			return found;
		}

		float limit = maxRange * maxRange;
		foreach (Node node in GetTree().GetNodesInGroup(Groups.Enemies))
		{
			if (node is Node2D enemy && !enemy.IsQueuedForDeletion()
				&& origin.DistanceSquaredTo(enemy.GlobalPosition) <= limit)
			{
				found.Add(enemy);
			}
		}

		found.Sort((a, b) => origin.DistanceSquaredTo(a.GlobalPosition).CompareTo(origin.DistanceSquaredTo(b.GlobalPosition)));
		if (found.Count > count)
		{
			found.RemoveRange(count, found.Count - count);
		}

		return found;
	}
}
```

- [ ] **Step 4: Run**

```bash
RUNNER=cs tools/run_tests.sh | grep -E "LifetimeTest|TargetProviderTest|FAIL|  -|^----|!!"
```
Expected: two + eight `PASS` lines and `---- 148 passed, 0 failed ----`.
(The zero-range test relies on the enemy standing at (10, 0), not at the origin, so a zero range finds nobody; if you move that enemy to the origin the assertion would be wrong.)

- [ ] **Step 5: Commit**

```bash
git add port/src/Components port/tests/Components
git commit -m "feat(components): Lifetime and TargetProvider" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 8: `ActorVisual`, spec notes and the slice-2a check

**Files:**
- Create: `port/src/Components/ActorVisual.cs`
- Test: `port/tests/Components/ActorVisualTest.cs`
- Modify: `docs/superpowers/specs/2026-10-14-csharp-forge-port-design.md`

**Interfaces:**
- Consumes: `VisualDef`, `AnimNames`, `DefFactory.Texture`.
- Produces: `ActorVisual : AnimatedSprite2D` — `Apply(VisualDef def, float scaleFactor = 1f, Color? tint = null)`, `SetMoving(bool)`, `Face(float directionX)`, `PlayOnce(StringName animation)` (death is terminal), `FlashHit()`, signal `OneShotFinished(StringName animationName)`.

- [ ] **Step 1: Write the failing test**

`port/tests/Components/ActorVisualTest.cs`:

```csharp
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class ActorVisualTest : TestCase
{
	private static AnimationStrip MakeStrip(StringName animation, int frames, bool loop) => new()
	{
		Animation = animation,
		Texture = DefFactory.Texture(frames * 10, 10),
		FrameCount = frames,
		Loop = loop,
	};

	private static VisualDef MakeDef()
	{
		var def = new VisualDef();
		def.Strips.Add(MakeStrip(AnimNames.Idle, 6, true));
		def.Strips.Add(MakeStrip(AnimNames.Walk, 8, true));
		def.Strips.Add(MakeStrip(AnimNames.Hurt, 4, false));
		def.Strips.Add(MakeStrip(AnimNames.Death, 5, false));
		return def;
	}

	private ActorVisual MakeVisual()
	{
		var visual = Adopt(new ActorVisual());
		visual.Apply(MakeDef(), 2f, Colors.Red);
		return visual;
	}

	public void TestApplyBuildsFramesFromStrips()
	{
		ActorVisual visual = MakeVisual();
		Assert.Eq(visual.SpriteFrames.GetFrameCount(AnimNames.Idle), 6);
		Assert.Eq(visual.SpriteFrames.GetFrameCount(AnimNames.Walk), 8);
		Assert.False(visual.SpriteFrames.GetAnimationLoop(AnimNames.Hurt));
		Assert.Eq(visual.Animation, AnimNames.Idle);
		Assert.Eq(visual.Scale, new Vector2(2, 2));
		Assert.Eq(visual.Modulate, Colors.Red);
	}

	public void TestSetMovingSwitchesBetweenWalkAndIdle()
	{
		ActorVisual visual = MakeVisual();
		visual.SetMoving(true);
		Assert.Eq(visual.Animation, AnimNames.Walk);
		visual.SetMoving(false);
		Assert.Eq(visual.Animation, AnimNames.Idle);
	}

	public void TestOneShotIsNotInterruptedByLocomotion()
	{
		ActorVisual visual = MakeVisual();
		visual.PlayOnce(AnimNames.Hurt);
		visual.SetMoving(true);
		Assert.Eq(visual.Animation, AnimNames.Hurt);
	}

	public void TestMissingOneShotAnimationIsIgnored()
	{
		ActorVisual visual = MakeVisual();
		visual.PlayOnce(AnimNames.Attack);
		Assert.Eq(visual.Animation, AnimNames.Idle);
	}

	public void TestFaceFlipsHorizontallyOnlyWithRealInput()
	{
		ActorVisual visual = MakeVisual();
		visual.Face(-1f);
		Assert.True(visual.FlipH);
		visual.Face(0f);
		Assert.True(visual.FlipH, "zero input keeps the facing");
		visual.Face(1f);
		Assert.False(visual.FlipH);
	}

	public void TestApplyAgainResetsOneShotAndMovingState()
	{
		ActorVisual visual = MakeVisual();
		visual.PlayOnce(AnimNames.Death);
		visual.Apply(MakeDef(), 1f);
		visual.SetMoving(true);
		Assert.Eq(visual.Animation, AnimNames.Walk);
	}

	public void TestDeathIsTerminal()
	{
		ActorVisual visual = MakeVisual();
		visual.PlayOnce(AnimNames.Death);
		Color tint = visual.Modulate;
		visual.PlayOnce(AnimNames.Hurt);
		visual.FlashHit();
		Assert.Eq(visual.Animation, AnimNames.Death);
		Assert.Eq(visual.Modulate, tint);
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: count greater than 0.

- [ ] **Step 3: Implement**

`port/src/Components/ActorVisual.cs`:

```csharp
using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Components;

/// <summary>Plays an actor's animations from a <see cref="VisualDef"/> (whose SpriteFrames are shared between actors).</summary>
public partial class ActorVisual : AnimatedSprite2D
{
	private static readonly Color HitFlashColor = new(1f, 0.45f, 0.45f);
	private const float HitFlashSeconds = 0.15f;

	private Color _baseTint = Colors.White;
	private bool _moving;
	private bool _oneShotActive;
	private bool _dead;

	[Signal]
	public delegate void OneShotFinishedEventHandler(StringName animationName);

	public override void _Ready() => AnimationFinished += OnAnimationFinished;

	public void Apply(VisualDef def, float scaleFactor = 1f, Color? tint = null)
	{
		SpriteFrames = def.SpriteFrames();
		Position = def.Offset;
		Scale = Vector2.One * scaleFactor;
		_baseTint = tint ?? Colors.White;
		Modulate = _baseTint;
		_moving = false;
		_oneShotActive = false;
		_dead = false;
		PlayLocomotion();
	}

	public void SetMoving(bool moving)
	{
		_moving = moving;
		if (!_oneShotActive)
		{
			PlayLocomotion();
		}
	}

	public void Face(float directionX)
	{
		if (Mathf.Abs(directionX) > 0.01f)
		{
			FlipH = directionX < 0f;
		}
	}

	/// <summary>Plays a non-looping animation, then returns to idle/walk (death stays on its last frame).</summary>
	public void PlayOnce(StringName animationName)
	{
		if (_dead || SpriteFrames is null || !SpriteFrames.HasAnimation(animationName))
		{
			return;
		}

		_dead = animationName == AnimNames.Death;
		_oneShotActive = true;
		Play(animationName);
	}

	public void FlashHit()
	{
		if (_dead)
		{
			return;
		}

		Modulate = HitFlashColor;
		CreateTween().TweenProperty(this, "modulate", _baseTint, HitFlashSeconds);
	}

	private void PlayLocomotion()
	{
		if (SpriteFrames is null)
		{
			return;
		}

		StringName wanted = _moving ? AnimNames.Walk : AnimNames.Idle;
		if (!SpriteFrames.HasAnimation(wanted))
		{
			wanted = AnimNames.Idle;
		}

		if (Animation != wanted || !IsPlaying())
		{
			Play(wanted);
		}
	}

	private void OnAnimationFinished()
	{
		StringName finished = Animation;
		EmitSignal(SignalName.OneShotFinished, finished);
		if (finished == AnimNames.Death)
		{
			return;
		}

		_oneShotActive = false;
		PlayLocomotion();
	}
}
```

- [ ] **Step 4: Run both suites**

```bash
tools/run_tests.sh | grep -E "^==|ActorVisualTest|^----|FAIL|!!" | grep -v "^\[Main\]"
```
Expected: seven `PASS  Components/ActorVisualTest::...`, `---- 648 passed, 0 failed ----` for GDScript and `---- 155 passed, 0 failed ----` for C# (148 + 7).

- [ ] **Step 5: Update the spec and commit**

In `docs/superpowers/specs/2026-10-14-csharp-forge-port-design.md`, section 4 (mapping table), add this paragraph after the table:

```
Slice 2a notes: there is no `EffectApplier` in C# (`ContactDamage` calls `DamageEffects.Hit` and checks that the target entity is still alive); `EnemyDef.contact_effect` and `ProjectileDef.hit_effect` were dropped for the same reason; `ProjectileDef` uses `ProjectileTeam` and `EnemyDef` uses `TargetPolicy` as top-level enums; none of the Defs or Components is `[GlobalClass]` while the GDScript tree exists (they all share names with GDScript `class_name`s). `StageDef`, `PlayerProfile`, `GradeTable`, `EventSchedule` and `BossDef` come with slices 2c-4 because they reference types that do not exist yet.
```

```bash
git add port/src/Components port/tests/Components docs/superpowers/specs/2026-10-14-csharp-forge-port-design.md
git commit -m "feat(components): ActorVisual, and spec notes for slice 2a" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 6: Report**

Tell the owner: 2a is done (list the totals), then that **2b (Actors)** is next. 2b needs scenes for the player, enemy, building, projectile and pickup, the Volley/Dash behaviors and the first `.tres` data in `port/data`, so it gets its own plan after this one lands on `main`.
