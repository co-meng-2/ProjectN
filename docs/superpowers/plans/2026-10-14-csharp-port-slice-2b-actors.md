# C# + Forge Port — Slice 2b (Actors) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port the actors of the core loop (Building, Projectile, Pickup + LootCollector, Enemy, Player) with their scenes, plus the two abilities the player starts with (Volley — which absorbs the old Arrow — and Dash), the `AutoCaster`, the `AbilityContext`, `PlayerProfile`, and the "invulnerable targets take no damage" rule.

**Architecture:** An actor is a Godot node (`CharacterBody2D`, `StaticBody2D`, `Area2D`, `Node2D`) that owns an `ActorEntity` child (the Forge seam from slice 1) and a handful of components from slice 2a, wired in its `Setup(...)`. Components never find their neighbours; the actor injects them. Abilities are `ActiveAbilityDef` resources plus an `IAbilityBehavior`; a behavior reaches the actor through `ActorEntity.Context` (an `AbilityContext`). Slice 2b adds no stage and no data files; the stage, HUD, first `.tres` data and the dev scene come in **2c**.

**Tech Stack:** Godot 4.7.2 .NET, C# net8.0, Forge 0.4.0 behind `Gas/` and `Components/ActorEntity`.

**Spec:** `docs/superpowers/specs/2026-10-14-csharp-forge-port-design.md` (sections 3, 4, 6, 7.3 slice 2). Previous plans: `...-slices-0-1.md`, `...-slice-2a-defs-components.md`.

## Global Constraints

- Work on a new branch `port/slice-2b` cut from an up-to-date `main`. Never stage `src/app/logo.gd` or `src/app/main.gd` (uncommitted owner work); always `git add` explicit paths.
- Commit messages end with `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>` (second `-m`).
- Tests run through `tools/run_tests.sh` (`RUNNER=cs` for the C# suite; the default runs both and the GDScript suite must stay at 648). The .NET editor may stay open. After adding a scene or a script that a scene references, run once with `IMPORT=1`.
- Layer order (a file may only `using` layers below it): `Core -> Defs -> Gas -> Components -> Actors -> Stage | Abilities | Boss -> Events -> Ui -> App | Meta`. Actors may not use Abilities; Abilities may use Actors. Project namespaces are never written fully qualified.
- **One global class namespace:** no new class carries `[GlobalClass]` while a GDScript `class_name` of the same name exists. All classes in this plan (`Building`, `Projectile`, `Pickup`, `LootCollector`, `Enemy`, `Player`, `ActorBody`, `AutoCaster`, `AbilityContext`, `PlayerProfile`, `VolleyDef`, `DashDef`) collide, so none gets the attribute; `GlobalClassCollisionTest` enforces it. Scenes and `.tres` reference scripts by path.
- Godot C# rules: a class extending a Godot type is `partial`; its file name equals the class name; `[Signal]` delegates end in `EventHandler`; payloads that are not Variant-compatible (`DropRoll`, enums, tuples) use C# `event`s.
- Scenes are hand-written text `.tscn`. A node-typed `[Export]` property needs both the header attribute `node_paths=PackedStringArray("Name", ...)` and a line `Name = NodePath("...")` in the node body.
- Damage always goes through `DamageEffects.Hit`. Timers and cooldowns are Forge effects ticked by `ActorEntity.Tick`; tests that need a deterministic clock call `entity.Tick(dt)` and the components' `Step(...)`.
- Validation returns `List<string>`. No new balance numbers in code except the constants the GDScript classes already had.
- Expected C# totals assume the suite starts at **155** tests.

## Review Focus

1. A projectile whose shooter was freed must damage nobody but must still be consumed; a projectile that already hit must not hit again after its pierce is spent (Task 4).
2. A pickup that leaves and re-enters the magnet must be collected exactly once (Task 5).
3. A dead enemy must leave the targetable group, stop dealing damage and free itself, and an enemy whose target was freed must stand still without throwing (Task 6).
4. A volley with no enemy in range must not start the cooldown, and a volley with fewer enemies than its arrow count fires only at those enemies (Task 7).
5. Revoking Dash mid-dash must end the dash and the invulnerability at once; a dead player must stop shooting (Tasks 8-9).

---

## File Structure

```
port/src/Gas/DamageEffects.cs                      (T1, modified)
port/src/Defs/{ActiveAbilityDef (modified),PlayerProfile}.cs
port/src/Components/{AbilityContext,AutoCaster,ActorBody}.cs, ActorEntity.cs (modified)
port/src/Actors/Building/{Building.cs,building.tscn}
port/src/Actors/Projectile/{Projectile.cs,projectile.tscn}
port/src/Actors/Pickup/{Pickup.cs,LootCollector.cs,pickup.tscn}
port/src/Actors/Enemy/{Enemy.cs,enemy.tscn}
port/src/Actors/Player/{Player.cs,player.tscn}
port/src/Abilities/Attacks/{VolleyDef,VolleyBehavior,DashDef,DashBehavior}.cs
port/tests/Support/{TestEffects,ActorFixture}.cs    (extended)
port/tests/{Gas,Components,Actors,Abilities,Defs}/...
```

---

### Task 1: Invulnerable targets take no damage

**Files:**
- Modify: `port/src/Gas/DamageEffects.cs`, `port/tests/Support/TestEffects.cs`
- Test: `port/tests/Gas/DamageTest.cs` (add two tests)

**Interfaces:**
- Produces: `DamageEffects.Hit` ignores a target whose `Tags.AllTags` contains `state.invulnerable`. Tests: `TestEffects.GrantTag(ActorEntity entity, string tag) : ActiveEffectHandle?` (INFINITE effect that adds the tag).

- [ ] **Step 1: Write the failing tests**

Add to `port/tests/Gas/DamageTest.cs` (inside the class):

```csharp
	public void TestInvulnerableTargetTakesNoDamageUntilTheTagIsRemoved()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f));
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(100f));
		Gamesmiths.Forge.Effects.ActiveEffectHandle? tag = TestEffects.GrantTag(victim, GameplayTags.StateInvulnerable);
		DamageEffects.Hit(attacker, victim, 10f);
		Assert.AlmostEq(HealthOf(victim), 100.0, 0.001, "refused while the tag is present");
		victim.EffectsManager.RemoveEffect(tag!);
		DamageEffects.Hit(attacker, victim, 10f);
		Assert.AlmostEq(HealthOf(victim), 90.0);
	}

	public void TestAnInvulnerableSourceStillDealsDamage()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f));
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(100f));
		TestEffects.GrantTag(attacker, GameplayTags.StateInvulnerable);
		DamageEffects.Hit(attacker, victim, 10f);
		Assert.AlmostEq(HealthOf(victim), 90.0);
	}
```

Add to `port/tests/Support/TestEffects.cs` (add `using Gamesmiths.Forge.Effects.Components;` and `using VampireSurvivor.Gas;`):

```csharp
	/// <summary>An INFINITE effect that adds <paramref name="tag"/> to the entity; remove the returned handle to take it off.</summary>
	public static ActiveEffectHandle? GrantTag(ActorEntity entity, string tag)
	{
		var data = new EffectData(
			$"TestTag:{tag}",
			new DurationData(DurationType.Infinite),
			effectComponents: [new ModifierTagsEffectComponent(GameplayTags.ContainerOf(tag))]);
		return entity.EffectsManager.ApplyEffect(new Effect(data, new EffectOwnership(entity, entity)));
	}
```

- [ ] **Step 2: Run to see it fail**

```bash
RUNNER=cs tools/run_tests.sh | grep -E "DamageTest.*Invulnerable|FAIL|  -|^----|error"
```
Expected: `FAIL  Gas/DamageTest::TestInvulnerableTargetTakesNoDamageUntilTheTagIsRemoved` with `refused while the tag is present` (the other new test passes already).

- [ ] **Step 3: Implement**

In `port/src/Gas/DamageEffects.cs` replace the guard and add the helper:

```csharp
		if (amount <= 0f || !Stats.TryGet(target, Stats.Health, out _) || IsInvulnerable(target))
		{
			return;
		}
```
```csharp
	private static bool IsInvulnerable(IForgeEntity target)
	{
		return target.Tags.AllTags.HasTag(GameplayTags.Request(GameplayTags.StateInvulnerable));
	}
```
Also update the XML summary of `Hit`: "...targets without vitals and invulnerable targets are ignored: a hit can never heal."

- [ ] **Step 4: Run**

```bash
RUNNER=cs tools/run_tests.sh | grep -E "DamageTest|FAIL|  -|^----|!!"
```
Expected: nine `PASS  Gas/DamageTest::...` and `---- 157 passed, 0 failed ----`.

- [ ] **Step 5: Commit**

```bash
git switch main && git pull --ff-only && git switch -c port/slice-2b
git add port/src/Gas/DamageEffects.cs port/tests/Support/TestEffects.cs port/tests/Gas/DamageTest.cs
git commit -m "feat(gas): invulnerable targets take no damage" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
(Do the `git switch -c` before editing files if you prefer; the edits carry over.)

---

### Task 2: `AbilityContext`, `AutoCaster`, `ActorEntity.Context` and `TryActivate`

**Files:**
- Create: `port/src/Components/AbilityContext.cs`, `port/src/Components/AutoCaster.cs`
- Modify: `port/src/Components/ActorEntity.cs`, `port/src/Defs/ActiveAbilityDef.cs`
- Test: `port/tests/Components/AutoCasterTest.cs`, `port/tests/Components/AbilityContextTest.cs`

**Interfaces:**
- Consumes: `AbilityGrant`, `ProbeAbilityDef`, `ActorFixture.BareActor`, `Mover`, `TargetProvider`, `Wallet`, `EnemyDef`.
- Produces (namespace `VampireSurvivor.Components`):
  - `AbilityContext(Node2D actor, Mover mover, TargetProvider targets, Node projectileLayer, Wallet wallet)` — read-only `Actor`, `Mover`, `Targets`, `ProjectileLayer`, `Wallet`; `Action<EnemyDef, Vector2>? Summon { get; set; }`; `event Action<StringName, Vector2>? AnimationRequested`; `void RequestAnimation(StringName animation, Vector2 facing)`.
  - `ActorEntity.Context : AbilityContext?` (settable) and `bool ActorEntity.TryActivate(string abilityTag, IForgeEntity? target = null)`.
  - `ActiveAbilityDef.AutoFire : bool` (`[Export]`, default false).
  - `AutoCaster : Node` — `bool Enabled = true`, `int Count`, `Add(AbilityGrant)`, `Remove(AbilityGrant?)`, `Step()` (also `_PhysicsProcess`): activates every added grant whose ability is valid and not already running; drops grants whose ability handle died.

- [ ] **Step 1: Write the failing tests**

`port/tests/Components/AbilityContextTest.cs`:

```csharp
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class AbilityContextTest : TestCase
{
	private AbilityContext MakeContext()
	{
		return new AbilityContext(
			Adopt(new Node2D()), Adopt(new Mover()), Adopt(new TargetProvider()), Adopt(new Node2D()), new Wallet());
	}

	public void TestRequestAnimationReachesSubscribers()
	{
		AbilityContext context = MakeContext();
		var seen = new List<(StringName, Vector2)>();
		context.AnimationRequested += (animation, facing) => seen.Add((animation, facing));
		context.RequestAnimation(AnimNames.Attack, Vector2.Left);
		Assert.Eq(seen.Count, 1);
		Assert.Eq(seen[0].Item1, AnimNames.Attack);
		Assert.Eq(seen[0].Item2, Vector2.Left);
	}

	public void TestRequestAnimationWithoutSubscribersIsHarmless()
	{
		MakeContext().RequestAnimation(AnimNames.Attack, Vector2.Right);
	}

	public void TestActorEntityTryActivateRunsAGrantedAbilityByTagThenCoolsDown()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		var probe = new ProbeAbilityDef { Tag = GameplayTags.AbilityProbe, Cooldown = DefFactory.CurveOf(1f, 1f, 1f, 1f, 1f, 1f) };
		actor.Grant(probe, 1);
		Assert.True(actor.TryActivate(GameplayTags.AbilityProbe));
		Assert.Eq(probe.Starts, 1);
		Assert.False(actor.TryActivate(GameplayTags.AbilityProbe), "cooling down");
		actor.Tick(1.1);
		Assert.True(actor.TryActivate(GameplayTags.AbilityProbe));
	}

	public void TestTryActivateWithoutAGrantReturnsFalse()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		Assert.False(actor.TryActivate(GameplayTags.AbilityProbe));
	}
}
```

`port/tests/Components/AutoCasterTest.cs`:

```csharp
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class AutoCasterTest : TestCase
{
	private static ProbeAbilityDef MakeProbe(float cooldown = 1f, bool stayActive = false) => new()
	{
		Tag = GameplayTags.AbilityProbe,
		Cooldown = DefFactory.CurveOf(cooldown, cooldown, cooldown, cooldown, cooldown, cooldown),
		StayActive = stayActive,
	};

	private AutoCaster MakeCaster()
	{
		var caster = Adopt(new AutoCaster());
		caster.SetPhysicsProcess(false);
		return caster;
	}

	public void TestStepActivatesAddedAbilitiesAndRespectsTheCooldown()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ProbeAbilityDef probe = MakeProbe();
		AutoCaster caster = MakeCaster();
		caster.Add(actor.Grant(probe, 1));
		caster.Step();
		Assert.Eq(probe.Starts, 1);
		caster.Step();
		Assert.Eq(probe.Starts, 1, "still cooling down");
		actor.Tick(1.1);
		caster.Step();
		Assert.Eq(probe.Starts, 2);
	}

	public void TestAbilitiesThatWereNotAddedAreLeftAlone()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ProbeAbilityDef probe = MakeProbe();
		actor.Grant(probe, 1);
		MakeCaster().Step();
		Assert.Eq(probe.Starts, 0);
	}

	public void TestARunningAbilityIsNotActivatedAgain()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ProbeAbilityDef probe = MakeProbe(0.1f, stayActive: true);
		AutoCaster caster = MakeCaster();
		caster.Add(actor.Grant(probe, 1));
		caster.Step();
		actor.Tick(0.5);
		caster.Step();
		Assert.Eq(probe.Starts, 1);
	}

	public void TestRevokedGrantsAreDroppedWithoutThrowing()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		AutoCaster caster = MakeCaster();
		AbilityGrant grant = actor.Grant(MakeProbe(), 1);
		caster.Add(grant);
		actor.Revoke(grant);
		caster.Step();
		Assert.Eq(caster.Count, 0);
	}

	public void TestDisabledCasterDoesNothingAndAddingTwiceCountsOnce()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ProbeAbilityDef probe = MakeProbe();
		AutoCaster caster = MakeCaster();
		AbilityGrant grant = actor.Grant(probe, 1);
		caster.Add(grant);
		caster.Add(grant);
		Assert.Eq(caster.Count, 1);
		caster.Enabled = false;
		caster.Step();
		Assert.Eq(probe.Starts, 0);
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: count greater than 0 (`AbilityContext`, `AutoCaster`, `TryActivate` missing).

- [ ] **Step 3: Implement**

`port/src/Components/AbilityContext.cs`:

```csharp
using System;
using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Components;

/// <summary>
/// What an actor offers its abilities. The actor builds it once, stores it on <see cref="ActorEntity.Context"/> and every
/// ability behavior reads it from there.
/// </summary>
public sealed class AbilityContext(Node2D actor, Mover mover, TargetProvider targets, Node projectileLayer, Wallet wallet)
{
	public Node2D Actor { get; } = actor;

	public Mover Mover { get; } = mover;

	public TargetProvider Targets { get; } = targets;

	public Node ProjectileLayer { get; } = projectileLayer;

	/// <summary>The run's currency; on-kill/economy abilities credit it directly.</summary>
	public Wallet Wallet { get; } = wallet;

	/// <summary>Boss summons, injected by the Stage so summoned enemies count as kills and drop loot. Null for the player.</summary>
	public Action<EnemyDef, Vector2>? Summon { get; set; }

	/// <summary>An ability asks its actor to show an animation; the actor decides how.</summary>
	public event Action<StringName, Vector2>? AnimationRequested;

	public void RequestAnimation(StringName animation, Vector2 facing) => AnimationRequested?.Invoke(animation, facing);
}
```

`port/src/Components/AutoCaster.cs`:

```csharp
using System.Collections.Generic;
using Gamesmiths.Forge.Abilities;
using Godot;

namespace VampireSurvivor.Components;

/// <summary>
/// Keeps trying to activate the abilities added to it. A failed activation (cooling down, nothing to shoot) costs nothing:
/// the ability itself decides when it actually fires and commits its cooldown.
/// </summary>
public partial class AutoCaster : Node
{
	private readonly List<AbilityGrant> _grants = [];

	public bool Enabled { get; set; } = true;

	public int Count => _grants.Count;

	public void Add(AbilityGrant grant)
	{
		if (!_grants.Contains(grant))
		{
			_grants.Add(grant);
		}
	}

	public void Remove(AbilityGrant? grant)
	{
		if (grant is not null)
		{
			_grants.Remove(grant);
		}
	}

	public void Step()
	{
		if (!Enabled)
		{
			return;
		}

		for (int i = _grants.Count - 1; i >= 0; i--)
		{
			AbilityHandle? ability = _grants[i].Ability;
			if (ability is null || !ability.IsValid)
			{
				_grants.RemoveAt(i);
				continue;
			}

			if (!ability.IsActive)
			{
				ability.Activate(out _);
			}
		}
	}

	public override void _PhysicsProcess(double delta) => Step();
}
```

In `port/src/Defs/ActiveAbilityDef.cs` add after `OwnedTags`:

```csharp
	/// <summary>When true the owner's <c>AutoCaster</c> keeps trying to activate it (attacks); false for ability on demand (dash).</summary>
	[Export]
	public bool AutoFire { get; set; }
```

In `port/src/Components/ActorEntity.cs` add the property and method (add `using Gamesmiths.Forge.Core;` if missing):

```csharp
	/// <summary>What this actor offers its abilities; set by the actor's Setup.</summary>
	public AbilityContext? Context { get; set; }

	/// <summary>Activates the granted abilities carrying <paramref name="abilityTag"/>. True when at least one started.</summary>
	public bool TryActivate(string abilityTag, IForgeEntity? target = null)
	{
		return Abilities.TryActivateAbilitiesByTag(GameplayTags.ContainerOf(abilityTag), target, out _);
	}
```

- [ ] **Step 4: Run**

```bash
RUNNER=cs tools/run_tests.sh | grep -E "AbilityContextTest|AutoCasterTest|FAIL|  -|^----|!!"
```
Expected: four + five `PASS` lines and `---- 166 passed, 0 failed ----`.

- [ ] **Step 5: Commit**

```bash
git add port/src/Components port/src/Defs/ActiveAbilityDef.cs port/tests/Components
git commit -m "feat(components): AbilityContext, AutoCaster and ActorEntity.TryActivate" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `Building`

**Files:**
- Create: `port/src/Actors/Building/Building.cs`, `port/src/Actors/Building/building.tscn`
- Test: `port/tests/Actors/BuildingTest.cs`

**Interfaces:**
- Produces (namespace `VampireSurvivor.Actors`): `Building : StaticBody2D` — `[Export]` nodes `Entity`, `Health`, `Hurtbox`, `Sprite`; `Setup(Texture2D texture, StatBlock stats)`; `[Signal] Destroyed` (once, when health reaches 0); joins group `Groups.Building` in `_Ready`. Scene `res://port/src/Actors/Building/building.tscn` (body layer 1, hurtbox on layer 8 = `AllyHurt`).

- [ ] **Step 1: Write the failing test**

`port/tests/Actors/BuildingTest.cs`:

```csharp
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Actors;

public class BuildingTest : TestCase
{
	private const string ScenePath = "res://port/src/Actors/Building/building.tscn";

	private Building MakeBuilding(float maxHealth)
	{
		var building = Adopt(GD.Load<PackedScene>(ScenePath).Instantiate<Building>());
		building.Setup(DefFactory.Texture(64, 64), new StatBlock { MaxHealth = maxHealth });
		return building;
	}

	private void Hit(Building building, float amount)
	{
		ActorEntity attacker = ActorFixture.BareActor(this, new StatBlock());
		DamageEffects.Hit(attacker, building.Entity, amount);
	}

	public void TestSetupAppliesStatsAndJoinsTheBuildingGroup()
	{
		Building building = MakeBuilding(300f);
		Assert.AlmostEq(building.Health.Current(), 300.0);
		Assert.True(building.IsInGroup(Groups.Building));
	}

	public void TestHurtboxSitsOnTheAllyHurtLayerAndProtectsTheBuildingsEntity()
	{
		Building building = MakeBuilding(300f);
		Assert.Eq((int)building.Hurtbox.CollisionLayer, PhysicsLayers.AllyHurt);
		Assert.True(building.Hurtbox.Entity == building.Entity);
		Assert.True(building.Hurtbox.Actor == building);
	}

	public void TestLethalDamageEmitsDestroyedOnce()
	{
		Building building = MakeBuilding(100f);
		int destroyed = 0;
		building.Destroyed += () => destroyed++;
		Hit(building, 60f);
		Assert.Eq(destroyed, 0);
		Hit(building, 60f);
		Hit(building, 60f);
		Assert.Eq(destroyed, 1);
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: count greater than 0 (`Building` missing).

- [ ] **Step 3: Implement**

`port/src/Actors/Building/Building.cs`:

```csharp
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Actors;

/// <summary>The structure the player defends. Emits <see cref="Destroyed"/> when its health reaches zero.</summary>
public partial class Building : StaticBody2D
{
	[Signal]
	public delegate void DestroyedEventHandler();

	[Export]
	public ActorEntity Entity { get; set; } = null!;

	[Export]
	public Health Health { get; set; } = null!;

	[Export]
	public Hurtbox Hurtbox { get; set; } = null!;

	[Export]
	public Sprite2D Sprite { get; set; } = null!;

	public override void _Ready() => AddToGroup(Groups.Building);

	/// <summary>Call after the building has entered the tree.</summary>
	public void Setup(Texture2D texture, StatBlock stats)
	{
		Sprite.Texture = texture;
		Entity.ApplyStats(stats);
		Health.Bind(Entity);
		Hurtbox.Bind(Entity, this);
		Health.Died += () => EmitSignal(SignalName.Destroyed);
	}
}
```

`port/src/Actors/Building/building.tscn`:

```
[gd_scene load_steps=8 format=3]

[ext_resource type="Script" path="res://port/src/Actors/Building/Building.cs" id="1_building"]
[ext_resource type="PackedScene" path="res://port/src/Components/actor_entity.tscn" id="2_entity"]
[ext_resource type="Script" path="res://port/src/Components/Health.cs" id="3_health"]
[ext_resource type="Script" path="res://port/src/Components/Hurtbox.cs" id="4_hurtbox"]

[sub_resource type="RectangleShape2D" id="RectangleShape2D_body"]
size = Vector2(68, 32)

[sub_resource type="RectangleShape2D" id="RectangleShape2D_hurt"]
size = Vector2(84, 86)

[node name="Building" type="StaticBody2D" node_paths=PackedStringArray("Entity", "Health", "Hurtbox", "Sprite")]
collision_layer = 1
collision_mask = 0
script = ExtResource("1_building")
Entity = NodePath("ActorEntity")
Health = NodePath("Health")
Hurtbox = NodePath("Hurtbox")
Sprite = NodePath("Sprite2D")

[node name="Sprite2D" type="Sprite2D" parent="."]

[node name="CollisionShape2D" type="CollisionShape2D" parent="."]
position = Vector2(0, 28)
shape = SubResource("RectangleShape2D_body")

[node name="ActorEntity" parent="." instance=ExtResource("2_entity")]

[node name="Health" type="Node" parent="."]
script = ExtResource("3_health")

[node name="Hurtbox" type="Area2D" parent="."]
collision_layer = 8
collision_mask = 0
monitoring = false
script = ExtResource("4_hurtbox")

[node name="CollisionShape2D" type="CollisionShape2D" parent="Hurtbox"]
position = Vector2(0, 2)
shape = SubResource("RectangleShape2D_hurt")
```

- [ ] **Step 4: Run**

```bash
RUNNER=cs IMPORT=1 tools/run_tests.sh | grep -E "BuildingTest|FAIL|  -|^----|!!"
```
Expected: three `PASS  Actors/BuildingTest::...` and `---- 169 passed, 0 failed ----`. If the exported node properties are `null`, check that the scene header lists every exported name in `node_paths` and that the property names match the C# names exactly.

- [ ] **Step 5: Commit**

```bash
git add port/src/Actors port/tests/Actors
git commit -m "feat(actors): Building" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `Projectile`

**Files:**
- Create: `port/src/Actors/Projectile/Projectile.cs`, `port/src/Actors/Projectile/projectile.tscn`
- Modify: `port/tests/Support/ActorFixture.cs` (add `HurtTarget`)
- Test: `port/tests/Actors/ProjectileTest.cs`

**Interfaces:**
- Consumes: `ProjectileDef`, `Hitbox`, `Lifetime`, `ActorEntity`, `DamageEffects.Hit`.
- Produces: `Projectile : Node2D` — `[Export]` nodes `Sprite`, `Hitbox`, `HitShape`, `Lifetime`; `Launch(ProjectileDef def, ActorEntity shooter, float damage, Vector2 direction)` (call after entering the tree); flies at `def.Speed`, hits via `DamageEffects.Hit(shooter, hurtbox.Entity, damage)`, ends after `Pierce + 1` hits or `Lifetime`. A freed shooter or victim means no damage but the projectile is still consumed. Scene `res://port/src/Actors/Projectile/projectile.tscn`.
- Tests: `ActorFixture.HurtTarget(TestCase test, Vector2 position, int layer = PhysicsLayers.EnemyHurt, float maxHealth = 100f, bool inEnemyGroup = false) : (ActorEntity Entity, Hurtbox Hurtbox)` — an entity plus a circle hurtbox (radius 12) under a `Node2D` at `position`.

- [ ] **Step 1: Write the failing tests**

Add to `port/tests/Support/ActorFixture.cs` (add `using VampireSurvivor.Components;` and `using VampireSurvivor.Core;` if missing):

```csharp
	/// <summary>
	/// An entity with a circle hurtbox (radius 12) under a Node2D at <paramref name="position"/>, so projectiles and sweeps can
	/// damage it. Read its health through <c>Entity.Attributes[Stats.Health]</c>.
	/// </summary>
	public static (ActorEntity Entity, Hurtbox Hurtbox) HurtTarget(
		TestCase test,
		Vector2 position,
		int layer = PhysicsLayers.EnemyHurt,
		float maxHealth = 100f,
		bool inEnemyGroup = false)
	{
		ActorEntity entity = BareActor(test, new StatBlock { MaxHealth = maxHealth });
		Node2D holder = test.Adopt(new Node2D { Position = position });
		if (inEnemyGroup)
		{
			holder.AddToGroup(Groups.Enemies);
		}

		var hurtbox = new Hurtbox { CollisionLayer = (uint)layer, CollisionMask = 0, Monitoring = false };
		hurtbox.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 12f } });
		holder.AddChild(hurtbox);
		hurtbox.Bind(entity, holder);
		return (entity, hurtbox);
	}
```

`port/tests/Actors/ProjectileTest.cs`:

```csharp
using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Actors;

public class ProjectileTest : TestCase
{
	private const string ScenePath = "res://port/src/Actors/Projectile/projectile.tscn";

	private static ProjectileDef MakeDef(int pierce = 0, float lifetime = 1f) => new()
	{
		Texture = DefFactory.Texture(32, 32),
		Speed = 50f,
		Lifetime = lifetime,
		Pierce = pierce,
		HitRadius = 8f,
	};

	private ActorEntity MakeShooter() => ActorFixture.BareActor(this, new StatBlock());

	private Projectile Fire(ActorEntity shooter, ProjectileDef def)
	{
		var projectile = Adopt(GD.Load<PackedScene>(ScenePath).Instantiate<Projectile>());
		projectile.Launch(def, shooter, 10f, Vector2.Right);
		return projectile;
	}

	private static double HealthOf(ActorEntity entity) => entity.Attributes[Stats.Health].DisplayValue;

	private static bool Consumed(Projectile projectile) => !GodotObject.IsInstanceValid(projectile) || projectile.IsQueuedForDeletion();

	public async Task TestProjectileDamagesAVictimAndIsConsumed()
	{
		ActorEntity shooter = MakeShooter();
		var (victim, _) = ActorFixture.HurtTarget(this, Vector2.Zero);
		Projectile projectile = Fire(shooter, MakeDef());
		await Settle();
		await NextPhysicsFrame();
		Assert.AlmostEq(HealthOf(victim), 90.0);
		Assert.True(Consumed(projectile));
	}

	public async Task TestWithoutPierceHitsOnlyOneOfTwoOverlappingVictims()
	{
		ActorEntity shooter = MakeShooter();
		var (first, _) = ActorFixture.HurtTarget(this, Vector2.Zero);
		var (second, _) = ActorFixture.HurtTarget(this, Vector2.Zero);
		Fire(shooter, MakeDef(0));
		await Settle();
		await NextPhysicsFrame();
		int damaged = (HealthOf(first) < 100.0 ? 1 : 0) + (HealthOf(second) < 100.0 ? 1 : 0);
		Assert.Eq(damaged, 1);
	}

	public async Task TestWithPierceHitsBothAndNeverHitsAThirdTime()
	{
		ActorEntity shooter = MakeShooter();
		var (first, _) = ActorFixture.HurtTarget(this, Vector2.Zero);
		var (second, _) = ActorFixture.HurtTarget(this, Vector2.Zero);
		var (third, _) = ActorFixture.HurtTarget(this, Vector2.Zero);
		Fire(shooter, MakeDef(1));
		await Settle();
		await NextPhysicsFrame();
		Assert.Eq((HealthOf(first) < 100.0 ? 1 : 0) + (HealthOf(second) < 100.0 ? 1 : 0) + (HealthOf(third) < 100.0 ? 1 : 0), 2,
			"pierce 1 = two hits in total");
	}

	public async Task TestAFreedShooterMeansNoDamageButTheProjectileIsStillConsumed()
	{
		ActorEntity shooter = MakeShooter();
		var (victim, _) = ActorFixture.HurtTarget(this, Vector2.Zero);
		Projectile projectile = Fire(shooter, MakeDef());
		shooter.Free();
		await Settle();
		await NextPhysicsFrame();
		Assert.AlmostEq(HealthOf(victim), 100.0, 0.001, "no damage without a living source");
		Assert.True(Consumed(projectile));
	}

	public async Task TestProjectileExpiresAfterItsLifetime()
	{
		Projectile projectile = Fire(MakeShooter(), MakeDef(0, 0.1f));
		await WaitSeconds(0.4);
		Assert.True(Consumed(projectile));
	}

	public async Task TestProjectileFliesAlongItsDirection()
	{
		Projectile projectile = Fire(MakeShooter(), MakeDef());
		Vector2 start = projectile.GlobalPosition;
		await WaitSeconds(0.2);
		Assert.True(projectile.GlobalPosition.X > start.X + 3f);
		Assert.AlmostEq(projectile.GlobalPosition.Y, start.Y, 0.01);
	}

	public async Task TestAnAllyHittingProjectileDamagesAlliesAndPassesEnemies()
	{
		ActorEntity shooter = MakeShooter();
		ProjectileDef def = MakeDef(1);
		def.Hits = ProjectileTeam.Allies;
		var (ally, _) = ActorFixture.HurtTarget(this, new Vector2(4, 0), PhysicsLayers.AllyHurt);
		var (enemy, _) = ActorFixture.HurtTarget(this, new Vector2(-4, 0), PhysicsLayers.EnemyHurt);
		Projectile projectile = Fire(shooter, def);
		Assert.Eq((int)projectile.Hitbox.CollisionMask, PhysicsLayers.AllyHurt);
		await Settle();
		await NextPhysicsFrame();
		Assert.AlmostEq(HealthOf(ally), 90.0);
		Assert.AlmostEq(HealthOf(enemy), 100.0, 0.001, "pierce 1 would have hit it too if the mask matched");
	}

	public void TestLaunchResetsTheMaskFromTheDefSoProjectilesHitEnemiesByDefault()
	{
		var projectile = Adopt(GD.Load<PackedScene>(ScenePath).Instantiate<Projectile>());
		projectile.Hitbox.CollisionMask = (uint)PhysicsLayers.AllyHurt;
		projectile.Launch(MakeDef(), MakeShooter(), 10f, Vector2.Right);
		Assert.Eq((int)projectile.Hitbox.CollisionMask, PhysicsLayers.EnemyHurt);
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: count greater than 0.

- [ ] **Step 3: Implement**

`port/src/Actors/Projectile/Projectile.cs`:

```csharp
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Actors;

/// <summary>A straight-flying projectile that damages the hurtboxes it touches through <see cref="DamageEffects.Hit"/>.</summary>
public partial class Projectile : Node2D
{
	private ProjectileDef? _def;
	private ActorEntity? _shooter;
	private float _damage;
	private Vector2 _velocity = Vector2.Zero;
	private int _pierceLeft;
	private bool _spent;

	[Export]
	public Sprite2D Sprite { get; set; } = null!;

	[Export]
	public Hitbox Hitbox { get; set; } = null!;

	[Export]
	public CollisionShape2D HitShape { get; set; } = null!;

	[Export]
	public Lifetime Lifetime { get; set; } = null!;

	public override void _Ready()
	{
		Hitbox.HurtboxEntered += OnHurtboxEntered;
		Lifetime.Expired += QueueFree;
	}

	/// <summary>Call after the projectile has entered the tree.</summary>
	public void Launch(ProjectileDef def, ActorEntity shooter, float damage, Vector2 direction)
	{
		_def = def;
		_shooter = shooter;
		_damage = damage;
		_pierceLeft = def.Pierce;
		_velocity = direction.Normalized() * def.Speed;
		Rotation = direction.Angle();
		Sprite.Texture = def.Texture;
		((CircleShape2D)HitShape.Shape).Radius = def.HitRadius;
		Hitbox.CollisionMask = (uint)def.HitMask();
		Lifetime.Restart(def.Lifetime);
	}

	public override void _PhysicsProcess(double delta) => GlobalPosition += _velocity * (float)delta;

	private void OnHurtboxEntered(Hurtbox hurtbox)
	{
		if (_spent)
		{
			return;
		}

		if (IsInstanceValid(_shooter) && hurtbox.Entity is { } victim && IsInstanceValid(victim))
		{
			DamageEffects.Hit(_shooter!, victim, _damage);
		}

		if (_pierceLeft > 0)
		{
			_pierceLeft--;
			return;
		}

		_spent = true;
		QueueFree();
	}
}
```

`port/src/Actors/Projectile/projectile.tscn`:

```
[gd_scene load_steps=5 format=3]

[ext_resource type="Script" path="res://port/src/Actors/Projectile/Projectile.cs" id="1_projectile"]
[ext_resource type="Script" path="res://port/src/Components/Hitbox.cs" id="2_hitbox"]
[ext_resource type="Script" path="res://port/src/Components/Lifetime.cs" id="3_lifetime"]

[sub_resource type="CircleShape2D" id="CircleShape2D_hit"]
resource_local_to_scene = true
radius = 6.0

[node name="Projectile" type="Node2D" node_paths=PackedStringArray("Sprite", "Hitbox", "HitShape", "Lifetime")]
script = ExtResource("1_projectile")
Sprite = NodePath("Sprite2D")
Hitbox = NodePath("Hitbox")
HitShape = NodePath("Hitbox/CollisionShape2D")
Lifetime = NodePath("Lifetime")

[node name="Sprite2D" type="Sprite2D" parent="."]

[node name="Hitbox" type="Area2D" parent="."]
collision_layer = 0
collision_mask = 16
monitorable = false
script = ExtResource("2_hitbox")

[node name="CollisionShape2D" type="CollisionShape2D" parent="Hitbox"]
shape = SubResource("CircleShape2D_hit")

[node name="Lifetime" type="Node" parent="."]
script = ExtResource("3_lifetime")
```

- [ ] **Step 4: Run**

```bash
RUNNER=cs IMPORT=1 tools/run_tests.sh | grep -E "ProjectileTest|FAIL|  -|^----|!!"
```
Expected: eight `PASS  Actors/ProjectileTest::...` and `---- 177 passed, 0 failed ----`.
These tests depend on real physics frames; if one is flaky (fails once, passes on re-run) report it instead of adding waits.

- [ ] **Step 5: Commit**

```bash
git add port/src/Actors port/tests/Actors port/tests/Support/ActorFixture.cs
git commit -m "feat(actors): Projectile" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 5: `Pickup` and `LootCollector`

**Files:**
- Create: `port/src/Actors/Pickup/Pickup.cs`, `port/src/Actors/Pickup/LootCollector.cs`, `port/src/Actors/Pickup/pickup.tscn`
- Test: `port/tests/Actors/PickupTest.cs`

**Interfaces:**
- Consumes: `DropRoll`, `CurrencyKind`, `PhysicsLayers.Pickup`.
- Produces: `Pickup : Area2D` — `[Export] float AttractSpeed = 260`, `ReachDistance = 8`; `[Export] Sprite2D Sprite`; `CurrencyKind Kind`, `int Amount`; `Setup(DropRoll roll)` (icon scaled to fit 16 px); `AttractTo(Node2D? target)`; `Step(double delta)` (also `_PhysicsProcess`); `[Signal] Reached(Pickup pickup)` once. `LootCollector : Area2D` — `Bind(Node2D anchor)`, C# `event Action<CurrencyKind, int> Collected`; a pickup that leaves and re-enters is subscribed to only once. Scene `pickup.tscn` (layer 32 = `Pickup`, `monitoring = false`).

- [ ] **Step 1: Write the failing test**

`port/tests/Actors/PickupTest.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Actors;

public class PickupTest : TestCase
{
	private const string ScenePath = "res://port/src/Actors/Pickup/pickup.tscn";

	private static DropRoll MakeRoll(CurrencyKind kind, int amount) => new()
	{
		Kind = kind,
		Amount = amount,
		Icon = DefFactory.Texture(32, 32),
	};

	private Pickup MakePickup(Vector2 position)
	{
		var pickup = Adopt(GD.Load<PackedScene>(ScenePath).Instantiate<Pickup>());
		pickup.GlobalPosition = position;
		pickup.Setup(MakeRoll(CurrencyKind.Stone, 4));
		return pickup;
	}

	private (Node2D Anchor, LootCollector Collector, List<(CurrencyKind, int)> Collected) MakeCollector()
	{
		var anchor = Adopt(new Node2D());
		var collector = new LootCollector
		{
			CollisionLayer = 0,
			CollisionMask = (uint)PhysicsLayers.Pickup,
			Monitorable = false,
		};
		collector.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 60f } });
		anchor.AddChild(collector);
		collector.Bind(anchor);
		var collected = new List<(CurrencyKind, int)>();
		collector.Collected += (kind, amount) => collected.Add((kind, amount));
		return (anchor, collector, collected);
	}

	public void TestSetupCopiesKindAmountAndFitsTheIcon()
	{
		Pickup pickup = MakePickup(Vector2.Zero);
		Assert.Eq(pickup.Kind, CurrencyKind.Stone);
		Assert.Eq(pickup.Amount, 4);
		Assert.AlmostEq(pickup.Sprite.Scale.X, 0.5);
	}

	public void TestPickupWithoutATargetStaysPut()
	{
		Pickup pickup = MakePickup(new Vector2(30, 0));
		pickup.SetPhysicsProcess(false);
		pickup.Step(1.0);
		Assert.Eq(pickup.GlobalPosition, new Vector2(30, 0));
	}

	public void TestAttractedPickupFliesToTheTargetAndReportsOnce()
	{
		var target = Adopt(new Node2D());
		Pickup pickup = MakePickup(new Vector2(100, 0));
		pickup.SetPhysicsProcess(false);
		int reached = 0;
		pickup.Reached += _ => reached++;
		pickup.AttractTo(target);
		for (int i = 0; i < 40; i++)
		{
			pickup.Step(0.05);
		}

		Assert.Eq(reached, 1);
		Assert.True(pickup.IsQueuedForDeletion());
	}

	public async Task TestCollectorAttractsAndReportsCollectedAmounts()
	{
		var (_, _, collected) = MakeCollector();
		MakePickup(new Vector2(30, 0));
		await WaitSeconds(0.6);
		Assert.SequenceEq(collected, new[] { (CurrencyKind.Stone, 4) });
	}

	public async Task TestPickupLeavingAndReenteringTheMagnetIsCollectedOnce()
	{
		var (_, _, collected) = MakeCollector();
		Pickup pickup = MakePickup(new Vector2(50, 0));
		pickup.SetPhysicsProcess(false);
		await Settle();
		pickup.GlobalPosition = new Vector2(400, 0);
		await Settle();
		pickup.GlobalPosition = new Vector2(50, 0);
		pickup.SetPhysicsProcess(true);
		await WaitSeconds(0.6);
		Assert.SequenceEq(collected, new[] { (CurrencyKind.Stone, 4) });
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: count greater than 0.

- [ ] **Step 3: Implement**

`port/src/Actors/Pickup/Pickup.cs`:

```csharp
using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Actors;

/// <summary>A currency drop. Sits still until a collector attracts it, then flies to the target and is consumed.</summary>
public partial class Pickup : Area2D
{
	private const float IconSize = 16f;

	private Node2D? _target;

	[Signal]
	public delegate void ReachedEventHandler(Pickup pickup);

	[Export]
	public float AttractSpeed { get; set; } = 260f;

	[Export]
	public float ReachDistance { get; set; } = 8f;

	[Export]
	public Sprite2D Sprite { get; set; } = null!;

	public CurrencyKind Kind { get; private set; } = CurrencyKind.Gold;

	public int Amount { get; private set; }

	/// <summary>Call after the pickup has entered the tree.</summary>
	public void Setup(DropRoll roll)
	{
		Kind = roll.Kind;
		Amount = roll.Amount;
		Sprite.Texture = roll.Icon;
		if (roll.Icon is not null)
		{
			float largest = Mathf.Max(roll.Icon.GetWidth(), roll.Icon.GetHeight());
			Sprite.Scale = Vector2.One * (IconSize / largest);
		}
	}

	public void AttractTo(Node2D? target) => _target = target;

	public void Step(double delta)
	{
		if (!IsInstanceValid(_target))
		{
			return;
		}

		Vector2 offset = _target!.GlobalPosition - GlobalPosition;
		float travel = AttractSpeed * (float)delta;
		if (offset.Length() <= ReachDistance + travel)
		{
			_target = null;
			EmitSignal(SignalName.Reached, this);
			QueueFree();
			return;
		}

		GlobalPosition += offset.Normalized() * travel;
	}

	public override void _PhysicsProcess(double delta) => Step(delta);
}
```

`port/src/Actors/Pickup/LootCollector.cs`:

```csharp
using System;
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;

namespace VampireSurvivor.Actors;

/// <summary>
/// Magnet area. Pickups entering it are attracted to the anchor; <see cref="Collected"/> fires when they arrive.
/// Configure in the scene: collision_layer = 0, collision_mask = Pickup, monitorable = false.
/// </summary>
public partial class LootCollector : Area2D
{
	private readonly HashSet<ulong> _subscribed = [];
	private Node2D? _anchor;

	public event Action<CurrencyKind, int>? Collected;

	public void Bind(Node2D anchor)
	{
		_anchor = anchor;
		AreaEntered += OnAreaEntered;
	}

	private void OnAreaEntered(Area2D area)
	{
		if (area is not Pickup pickup)
		{
			return;
		}

		// A pickup can leave and re-enter the magnet (dash), so subscribe only once.
		if (_subscribed.Add(pickup.GetInstanceId()))
		{
			pickup.Reached += OnPickupReached;
		}

		pickup.AttractTo(_anchor);
	}

	private void OnPickupReached(Pickup pickup)
	{
		_subscribed.Remove(pickup.GetInstanceId());
		Collected?.Invoke(pickup.Kind, pickup.Amount);
	}
}
```

`port/src/Actors/Pickup/pickup.tscn`:

```
[gd_scene load_steps=3 format=3]

[ext_resource type="Script" path="res://port/src/Actors/Pickup/Pickup.cs" id="1_pickup"]

[sub_resource type="CircleShape2D" id="CircleShape2D_pickup"]
radius = 6.0

[node name="Pickup" type="Area2D" node_paths=PackedStringArray("Sprite")]
collision_layer = 32
collision_mask = 0
monitoring = false
script = ExtResource("1_pickup")
Sprite = NodePath("Sprite2D")

[node name="Sprite2D" type="Sprite2D" parent="."]

[node name="CollisionShape2D" type="CollisionShape2D" parent="."]
shape = SubResource("CircleShape2D_pickup")
```

- [ ] **Step 4: Run**

```bash
RUNNER=cs IMPORT=1 tools/run_tests.sh | grep -E "PickupTest|FAIL|  -|^----|!!"
```
Expected: five `PASS  Actors/PickupTest::...` and `---- 182 passed, 0 failed ----`.

- [ ] **Step 5: Commit**

```bash
git add port/src/Actors port/tests/Actors
git commit -m "feat(actors): Pickup and LootCollector" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 6: `ActorBody` and `Enemy`

**Files:**
- Create: `port/src/Components/ActorBody.cs`, `port/src/Actors/Enemy/Enemy.cs`, `port/src/Actors/Enemy/enemy.tscn`
- Test: `port/tests/Actors/EnemyTest.cs`

**Interfaces:**
- Consumes: Tasks 3-5 (`Building` for the contact test), all slice-2a components, `EnemyDef`, `RunRng`.
- Produces:
  - `ActorBody` (plain class, `Components`) — ctor `(Node2D actor, ActorEntity entity, ActorVisual visual, Hurtbox hurtbox, Hitbox contactHitbox, CollisionShape2D bodyShape)`; `const float HurtboxPadding = 2`, `ContactPadding = 4`; `SizeShapes(float radius, CollisionShape2D hurtShape, CollisionShape2D contactShape)`; `WireContact(ContactDamage contact, float damage, float interval)`; `Die(IEnumerable<Node> stopped, float freeDelay)` — leaves the enemy group, stops processing, disables the colliders (deferred), `entity.CleanupDeferred()`, plays death, frees the actor after `freeDelay` (only if it still exists).
  - `Enemy : CharacterBody2D` — `[Export]` nodes `Entity`, `Health`, `Hurtbox`, `ContactHitbox`, `ContactDamage`, `Mover`, `Visual`, `LootDropper`, `BodyShape`, `HurtShape`, `ContactShape`; `EnemyDef? Def`; `Setup(EnemyDef def, Node2D target, RunRng rng)`; `[Signal] Died(Enemy enemy)`; C# `event Action<DropRoll, Vector2> LootDropped`; joins `Groups.Enemies`. Scene `res://port/src/Actors/Enemy/enemy.tscn` (body layer 4, mask 1; hurtbox layer 16; contact hitbox mask 8, `OncePerTarget = false`).

- [ ] **Step 1: Write the failing test**

`port/tests/Actors/EnemyTest.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Actors;

public class EnemyTest : TestCase
{
	private const string EnemyScene = "res://port/src/Actors/Enemy/enemy.tscn";
	private const string BuildingScene = "res://port/src/Actors/Building/building.tscn";

	private static EnemyDef MakeDef(float maxHealth = 30f, bool withDrop = false)
	{
		EnemyDef def = DefFactory.Enemy(GD.Load<PackedScene>(EnemyScene));
		def.Stats = new StatBlock { MaxHealth = maxHealth, MoveSpeed = 120f };
		def.ContactDamage = 10f;
		if (withDrop)
		{
			def.Drops = new DropTable();
			def.Drops.Entries.Add(new DropEntry { Chance = 1f, Icon = DefFactory.Texture(8, 8) });
		}

		return def;
	}

	private Enemy MakeEnemy(EnemyDef def, Node2D target, Vector2? at = null)
	{
		var enemy = Adopt(GD.Load<PackedScene>(EnemyScene).Instantiate<Enemy>());
		enemy.GlobalPosition = at ?? Vector2.Zero;
		enemy.Setup(def, target, new RunRng(5));
		return enemy;
	}

	private Building MakeBuilding(float maxHealth)
	{
		var building = Adopt(GD.Load<PackedScene>(BuildingScene).Instantiate<Building>());
		building.Setup(DefFactory.Texture(64, 64), new StatBlock { MaxHealth = maxHealth });
		return building;
	}

	private void Kill(Enemy enemy)
	{
		ActorEntity killer = ActorFixture.BareActor(this, new StatBlock());
		DamageEffects.Hit(killer, enemy.Entity, 9999f);
	}

	public void TestSetupAppliesStatsAndJoinsTheEnemyGroup()
	{
		var target = Adopt(new Node2D());
		Enemy enemy = MakeEnemy(MakeDef(45f), target);
		Assert.AlmostEq(enemy.Health.Current(), 45.0);
		Assert.True(enemy.IsInGroup(Groups.Enemies));
	}

	public async Task TestEnemyWalksTowardItsTarget()
	{
		var target = Adopt(new Node2D { GlobalPosition = new Vector2(300, 0) });
		Enemy enemy = MakeEnemy(MakeDef(), target);
		await WaitSeconds(0.3);
		Assert.True(enemy.GlobalPosition.X > 5f, "moved toward +x");
		Assert.AlmostEq(enemy.GlobalPosition.Y, 0.0, 0.5);
	}

	public async Task TestEnemyWithoutAValidTargetStandsStill()
	{
		var target = Adopt(new Node2D());
		Enemy enemy = MakeEnemy(MakeDef(), target);
		target.Free();
		await WaitSeconds(0.2);
		Assert.AlmostEq(enemy.GlobalPosition.Length(), 0.0, 0.5);
	}

	public async Task TestEnemyDamagesAnOverlappingAllyHurtbox()
	{
		Building building = MakeBuilding(200f);
		MakeEnemy(MakeDef(), building);
		await WaitSeconds(0.4);
		Assert.True(building.Health.Current() < 200f);
	}

	public async Task TestDeathEmitsSignalsLeavesTheGroupAndFreesItself()
	{
		var target = Adopt(new Node2D());
		Enemy enemy = MakeEnemy(MakeDef(30f, withDrop: true), target, new Vector2(10, 20));
		var deaths = new List<Enemy>();
		var drops = new List<(DropRoll, Vector2)>();
		enemy.Died += found => deaths.Add(found);
		enemy.LootDropped += (roll, position) => drops.Add((roll, position));
		Kill(enemy);
		Assert.SequenceEq(deaths, new[] { enemy });
		Assert.Eq(drops.Count, 1);
		Assert.False(enemy.IsInGroup(Groups.Enemies));
		await WaitSeconds(0.9);
		Assert.False(GodotObject.IsInstanceValid(enemy), "freed after the death delay");
	}

	public void TestDeadEnemyIsNotTargetable()
	{
		var provider = Adopt(new TargetProvider());
		var target = Adopt(new Node2D());
		Enemy enemy = MakeEnemy(MakeDef(), target, new Vector2(40, 0));
		Assert.True(provider.NearestEnemy(Vector2.Zero, 200f) == enemy);
		Kill(enemy);
		Assert.Null(provider.NearestEnemy(Vector2.Zero, 200f));
	}

	public async Task TestDeadEnemyStopsDealingDamage()
	{
		Building building = MakeBuilding(200f);
		EnemyDef def = MakeDef();
		def.ContactInterval = 0.2f;
		Enemy enemy = MakeEnemy(def, building);
		await WaitSeconds(0.3);
		Assert.True(building.Health.Current() < 200f, "enemy hit the building before dying");
		Kill(enemy);
		float healthAtDeath = building.Health.Current();
		await WaitSeconds(0.5);
		Assert.AlmostEq(building.Health.Current(), healthAtDeath);
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: count greater than 0.

- [ ] **Step 3: Implement**

`port/src/Components/ActorBody.cs`:

```csharp
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;

namespace VampireSurvivor.Components;

/// <summary>
/// The body plumbing Enemy and Boss share: sizing the three collision circles from one radius, wiring the contact-damage
/// component, and the physical part of dying. The actor keeps its own signals and extras.
/// </summary>
public sealed class ActorBody(
	Node2D actor,
	ActorEntity entity,
	ActorVisual visual,
	Hurtbox hurtbox,
	Hitbox contactHitbox,
	CollisionShape2D bodyShape)
{
	public const float HurtboxPadding = 2f;
	public const float ContactPadding = 4f;

	/// <summary>Sizes the body, hurtbox and contact circles (each a CircleShape2D) around <paramref name="radius"/>.</summary>
	public void SizeShapes(float radius, CollisionShape2D hurtShape, CollisionShape2D contactShape)
	{
		((CircleShape2D)bodyShape.Shape).Radius = radius;
		((CircleShape2D)hurtShape.Shape).Radius = radius + HurtboxPadding;
		((CircleShape2D)contactShape.Shape).Radius = radius + ContactPadding;
	}

	public void WireContact(ContactDamage contact, float damage, float interval)
	{
		contact.Bind(contactHitbox, entity, damage, interval);
	}

	/// <summary>
	/// Leaves the enemy group, stops the actor and every node in <paramref name="stopped"/> processing, disables the colliders,
	/// schedules the entity cleanup, plays the death animation and frees the actor after <paramref name="freeDelay"/>.
	/// The caller emits its own <c>Died</c> afterwards.
	/// </summary>
	public void Die(IEnumerable<Node> stopped, float freeDelay)
	{
		actor.RemoveFromGroup(Groups.Enemies);
		actor.SetPhysicsProcess(false);
		foreach (Node node in stopped)
		{
			node.SetPhysicsProcess(false);
		}

		hurtbox.SetDeferred(Area2D.PropertyName.Monitorable, false);
		contactHitbox.SetDeferred(Area2D.PropertyName.Monitoring, false);
		bodyShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		entity.CleanupDeferred();
		visual.PlayOnce(AnimNames.Death);
		actor.GetTree().CreateTimer(freeDelay).Timeout += () =>
		{
			if (GodotObject.IsInstanceValid(actor))
			{
				actor.QueueFree();
			}
		};
	}
}
```

`port/src/Actors/Enemy/Enemy.cs`:

```csharp
using System;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Actors;

/// <summary>Generic enemy configured by an <see cref="EnemyDef"/>: chases a target and damages allies it touches.</summary>
public partial class Enemy : CharacterBody2D
{
	private const float FreeDelayAfterDeath = 0.6f;

	private Node2D? _target;
	private ActorBody? _body;

	[Signal]
	public delegate void DiedEventHandler(Enemy enemy);

	public event Action<DropRoll, Vector2>? LootDropped;

	[Export]
	public ActorEntity Entity { get; set; } = null!;

	[Export]
	public Health Health { get; set; } = null!;

	[Export]
	public Hurtbox Hurtbox { get; set; } = null!;

	[Export]
	public Hitbox ContactHitbox { get; set; } = null!;

	[Export]
	public ContactDamage ContactDamage { get; set; } = null!;

	[Export]
	public Mover Mover { get; set; } = null!;

	[Export]
	public ActorVisual Visual { get; set; } = null!;

	[Export]
	public LootDropper LootDropper { get; set; } = null!;

	[Export]
	public CollisionShape2D BodyShape { get; set; } = null!;

	[Export]
	public CollisionShape2D HurtShape { get; set; } = null!;

	[Export]
	public CollisionShape2D ContactShape { get; set; } = null!;

	public EnemyDef? Def { get; private set; }

	/// <summary>Call after the enemy has entered the tree.</summary>
	public void Setup(EnemyDef def, Node2D target, RunRng rng)
	{
		Def = def;
		_target = target;
		Entity.ApplyStats(def.Stats!);
		Health.Bind(Entity);
		Hurtbox.Bind(Entity, this);
		Mover.Bind(this, Entity);
		_body = new ActorBody(this, Entity, Visual, Hurtbox, ContactHitbox, BodyShape);
		_body.WireContact(ContactDamage, def.ContactDamage, def.ContactInterval);
		LootDropper.Bind(Health, def.Drops, rng, this);
		Visual.Apply(def.Visual!, def.VisualScale, def.Tint);
		_body.SizeShapes(def.BodyRadius, HurtShape, ContactShape);
		Health.Damaged += _ => Visual.FlashHit();
		Health.Died += OnDied;
		LootDropper.DropRequested += (roll, position) => LootDropped?.Invoke(roll, position);
		ContactDamage.Attacked += _ => Visual.PlayOnce(AnimNames.Attack);
		AddToGroup(Groups.Enemies);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsInstanceValid(_target))
		{
			Mover.SetDirection(Vector2.Zero);
			Visual.SetMoving(false);
			return;
		}

		Vector2 toTarget = _target!.GlobalPosition - GlobalPosition;
		Mover.SetDirection(toTarget.Normalized());
		Visual.Face(toTarget.X);
		Visual.SetMoving(true);
	}

	private void OnDied()
	{
		_body!.Die([Mover, ContactDamage], FreeDelayAfterDeath);
		EmitSignal(SignalName.Died, this);
	}
}
```

`port/src/Actors/Enemy/enemy.tscn`:

```
[gd_scene load_steps=14 format=3]

[ext_resource type="Script" path="res://port/src/Actors/Enemy/Enemy.cs" id="1_enemy"]
[ext_resource type="PackedScene" path="res://port/src/Components/actor_entity.tscn" id="2_entity"]
[ext_resource type="Script" path="res://port/src/Components/Health.cs" id="3_health"]
[ext_resource type="Script" path="res://port/src/Components/Hurtbox.cs" id="4_hurtbox"]
[ext_resource type="Script" path="res://port/src/Components/Hitbox.cs" id="5_hitbox"]
[ext_resource type="Script" path="res://port/src/Components/ContactDamage.cs" id="6_contact"]
[ext_resource type="Script" path="res://port/src/Components/Mover.cs" id="7_mover"]
[ext_resource type="Script" path="res://port/src/Components/ActorVisual.cs" id="8_visual"]
[ext_resource type="Script" path="res://port/src/Components/LootDropper.cs" id="9_dropper"]

[sub_resource type="CircleShape2D" id="CircleShape2D_body"]
resource_local_to_scene = true
radius = 10.0

[sub_resource type="CircleShape2D" id="CircleShape2D_hurt"]
resource_local_to_scene = true
radius = 12.0

[sub_resource type="CircleShape2D" id="CircleShape2D_contact"]
resource_local_to_scene = true
radius = 14.0

[node name="Enemy" type="CharacterBody2D" node_paths=PackedStringArray("Entity", "Health", "Hurtbox", "ContactHitbox", "ContactDamage", "Mover", "Visual", "LootDropper", "BodyShape", "HurtShape", "ContactShape")]
collision_layer = 4
collision_mask = 1
motion_mode = 1
script = ExtResource("1_enemy")
Entity = NodePath("ActorEntity")
Health = NodePath("Health")
Hurtbox = NodePath("Hurtbox")
ContactHitbox = NodePath("ContactHitbox")
ContactDamage = NodePath("ContactDamage")
Mover = NodePath("Mover")
Visual = NodePath("Visual")
LootDropper = NodePath("LootDropper")
BodyShape = NodePath("CollisionShape2D")
HurtShape = NodePath("Hurtbox/CollisionShape2D")
ContactShape = NodePath("ContactHitbox/CollisionShape2D")

[node name="CollisionShape2D" type="CollisionShape2D" parent="."]
shape = SubResource("CircleShape2D_body")

[node name="ActorEntity" parent="." instance=ExtResource("2_entity")]

[node name="Health" type="Node" parent="."]
script = ExtResource("3_health")

[node name="Hurtbox" type="Area2D" parent="."]
collision_layer = 16
collision_mask = 0
monitoring = false
script = ExtResource("4_hurtbox")

[node name="CollisionShape2D" type="CollisionShape2D" parent="Hurtbox"]
shape = SubResource("CircleShape2D_hurt")

[node name="ContactHitbox" type="Area2D" parent="."]
collision_layer = 0
collision_mask = 8
monitorable = false
script = ExtResource("5_hitbox")
OncePerTarget = false

[node name="CollisionShape2D" type="CollisionShape2D" parent="ContactHitbox"]
shape = SubResource("CircleShape2D_contact")

[node name="ContactDamage" type="Node" parent="."]
script = ExtResource("6_contact")

[node name="Mover" type="Node" parent="."]
script = ExtResource("7_mover")

[node name="Visual" type="AnimatedSprite2D" parent="."]
script = ExtResource("8_visual")

[node name="LootDropper" type="Node" parent="."]
script = ExtResource("9_dropper")
```

- [ ] **Step 4: Run**

```bash
RUNNER=cs IMPORT=1 tools/run_tests.sh | grep -E "EnemyTest|FAIL|  -|^----|!!"
```
Expected: seven `PASS  Actors/EnemyTest::...` and `---- 189 passed, 0 failed ----`.

- [ ] **Step 5: Commit**

```bash
git add port/src/Components/ActorBody.cs port/src/Actors port/tests/Actors
git commit -m "feat(actors): ActorBody and Enemy" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 7: `VolleyDef` and `VolleyBehavior`

**Files:**
- Create: `port/src/Abilities/Attacks/VolleyDef.cs`, `port/src/Abilities/Attacks/VolleyBehavior.cs`
- Modify: `port/tests/Support/ActorFixture.cs` (add `Context`)
- Test: `port/tests/Abilities/VolleyTest.cs`

**Interfaces:**
- Consumes: `ActiveAbilityDef`, `AbilityContext`, `Projectile`, `TargetProvider.NearestEnemies`, `ActorFixture.HurtTarget`.
- Produces (namespace `VampireSurvivor.Abilities`):
  - `VolleyDef : ActiveAbilityDef` — `[Export] PackedScene? ProjectileScene`, `ProjectileDef? Projectile`, `Curve? ArrowCount` (arrows per cast over the ability level), `Curve? Damage` (per arrow, display units), `float AttackRange`; `CreateBehavior()` returns `VolleyBehavior`; `Validate()` adds the missing-piece errors. The old innate Arrow is a `VolleyDef` whose `ArrowCount` is 1 at every level.
  - `VolleyBehavior : IAbilityBehavior` — on start: picks `round(ArrowCount(level))` nearest enemies within `AttackRange`; with none it ends **without** committing the cooldown; otherwise commits the cooldown, spawns one projectile per target under `ctx.ProjectileLayer` (damage `Damage(level)` each), calls `ctx.RequestAnimation(Attack, direction)` and ends.
  - Tests: `ActorFixture.Context(TestCase test, ActorEntity entity, Vector2 at) : AbilityContext` — builds an actor `Node2D` at `at`, `Mover`, `TargetProvider`, a projectile layer and a `Wallet`, stores it on `entity.Context` and returns it.

- [ ] **Step 1: Write the failing tests**

Add to `port/tests/Support/ActorFixture.cs` (add `using VampireSurvivor.Core;` if missing):

```csharp
	/// <summary>An ability context for <paramref name="entity"/>: an actor node at <paramref name="at"/>, plus the helpers abilities need.</summary>
	public static AbilityContext Context(TestCase test, ActorEntity entity, Vector2 at)
	{
		var actor = test.Adopt(new Node2D { Position = at });
		var context = new AbilityContext(
			actor, test.Adopt(new Mover()), test.Adopt(new TargetProvider()), test.Adopt(new Node2D()), new Wallet());
		entity.Context = context;
		return context;
	}
```

`port/tests/Abilities/VolleyTest.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Abilities;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Abilities;

public class VolleyTest : TestCase
{
	private const string ProjectileScene = "res://port/src/Actors/Projectile/projectile.tscn";

	private static VolleyDef MakeVolley(float count = 2f) => new()
	{
		Tag = GameplayTags.AbilityVolley,
		Cooldown = DefFactory.CurveOf(2.4f, 2.1f, 1.8f, 1.5f, 1.2f, 0.9f),
		ProjectileScene = GD.Load<PackedScene>(ProjectileScene),
		Projectile = new ProjectileDef { Texture = DefFactory.Texture(8, 8), Speed = 420f, Lifetime = 0.7f },
		ArrowCount = DefFactory.CurveOf(count, count, count, count, count, count),
		Damage = DefFactory.CurveOf(8f, 10f, 13f, 17f, 22f, 30f),
		AttackRange = 300f,
	};

	private (ActorEntity Entity, AbilityContext Context, AbilityGrant Grant, VolleyDef Def) Rig(int level = 1, float count = 2f)
	{
		ActorEntity entity = ActorFixture.BareActor(this, new StatBlock());
		AbilityContext context = ActorFixture.Context(this, entity, Vector2.Zero);
		VolleyDef def = MakeVolley(count);
		return (entity, context, entity.Grant(def, level), def);
	}

	private static bool Fire(AbilityGrant grant) => grant.Ability!.Activate(out AbilityActivationFailures _);

	private Node2D MakeEnemy(Vector2 at)
	{
		var node = Adopt(new Node2D { GlobalPosition = at });
		node.AddToGroup(Groups.Enemies);
		return node;
	}

	public void TestFiresOneArrowAtEachOfTheNearestEnemies()
	{
		var (_, context, grant, _) = Rig(count: 2f);
		MakeEnemy(new Vector2(100, 0));
		MakeEnemy(new Vector2(0, 150));
		MakeEnemy(new Vector2(500, 0));
		Assert.True(Fire(grant));
		Assert.Eq(context.ProjectileLayer.GetChildCount(), 2);
	}

	public void TestFewerEnemiesThanArrowsFiresOnlyAtThoseEnemies()
	{
		var (_, context, grant, _) = Rig(count: 5f);
		MakeEnemy(new Vector2(100, 0));
		Assert.True(Fire(grant));
		Assert.Eq(context.ProjectileLayer.GetChildCount(), 1);
	}

	public void TestNoEnemyInRangeFiresNothingAndStartsNoCooldown()
	{
		var (entity, context, grant, _) = Rig();
		MakeEnemy(new Vector2(900, 0));
		Assert.True(Fire(grant), "the activation itself succeeds");
		Assert.Eq(context.ProjectileLayer.GetChildCount(), 0);
		Assert.False(entity.Tags.AllTags.HasTag(GameplayTags.Request(GameplayTags.CooldownVolley)), "a miss costs nothing");
		Assert.True(Fire(grant), "so it can try again immediately");
	}

	public void TestAShotStartsTheCooldownWhichScalesWithTheLevel()
	{
		var (entity, _, grant, _) = Rig(level: 6);
		MakeEnemy(new Vector2(100, 0));
		Assert.True(Fire(grant));
		Assert.True(entity.Tags.AllTags.HasTag(GameplayTags.Request(GameplayTags.CooldownVolley)));
		entity.Tick(0.5);
		Assert.False(Fire(grant), "level 6 cooldown is 0.9 s");
		entity.Tick(0.5);
		Assert.True(Fire(grant));
	}

	public void TestAnimationIsRequestedTowardTheFirstTarget()
	{
		var (_, context, grant, _) = Rig();
		var seen = new List<(StringName, Vector2)>();
		context.AnimationRequested += (animation, facing) => seen.Add((animation, facing));
		MakeEnemy(new Vector2(100, 0));
		Fire(grant);
		Assert.Eq(seen.Count, 1);
		Assert.Eq(seen[0].Item1, AnimNames.Attack);
		Assert.AlmostEq(seen[0].Item2.X, 1.0, 0.001);
	}

	public async Task TestDamageComesFromTheLevelCurveAndTheShootersAttack()
	{
		var (_, _, grant, _) = Rig(level: 3, count: 1f);
		var (victim, _) = ActorFixture.HurtTarget(this, new Vector2(100, 0), maxHealth: 100f, inEnemyGroup: true);
		Fire(grant);
		await WaitSeconds(0.5);
		Assert.AlmostEq(victim.Attributes[Stats.Health].DisplayValue, 87.0, 0.01, "level 3 damage is 13");
	}

	public void TestValidateReportsMissingPieces()
	{
		var def = new VolleyDef { Tag = GameplayTags.AbilityVolley };
		Assert.True(def.Validate().Count >= 3, "scene/projectile, curves and range are all missing");
		Assert.NoErrors(MakeVolley().Validate());
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: count greater than 0.

- [ ] **Step 3: Implement**

`port/src/Abilities/Attacks/VolleyDef.cs`:

```csharp
using System.Collections.Generic;
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Abilities;

/// <summary>
/// Fires one arrow at each of the N nearest enemies in range. N and the damage are curves over the ability level.
/// The player's innate arrow is a VolleyDef whose arrow count is 1 at every level.
/// </summary>
public partial class VolleyDef : ActiveAbilityDef
{
	[Export]
	public PackedScene? ProjectileScene { get; set; }

	[Export]
	public ProjectileDef? Projectile { get; set; }

	/// <summary>Arrows per cast over the ability level (x = 1..6).</summary>
	[Export]
	public Curve? ArrowCount { get; set; }

	/// <summary>Damage of each arrow over the ability level, in display units.</summary>
	[Export]
	public Curve? Damage { get; set; }

	[Export]
	public float AttackRange { get; set; }

	public override IAbilityBehavior CreateBehavior() => new VolleyBehavior(this);

	public override List<string> Validate()
	{
		List<string> errors = base.Validate();
		if (ProjectileScene is null || Projectile is null)
		{
			errors.Add("Volley: projectile scene and projectile def are required");
		}
		else
		{
			errors.AddRange(Projectile.Validate());
		}

		if (ArrowCount is null || Damage is null)
		{
			errors.Add("Volley: arrow count and damage curves are required");
		}

		if (AttackRange <= 0f)
		{
			errors.Add("Volley: attack range must be > 0");
		}

		return errors;
	}
}
```

`port/src/Abilities/Attacks/VolleyBehavior.cs`:

```csharp
using System.Collections.Generic;
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;

namespace VampireSurvivor.Abilities;

/// <summary>One cast of a <see cref="VolleyDef"/>. It ends in the same call: shooting is instantaneous.</summary>
public sealed class VolleyBehavior(VolleyDef def) : IAbilityBehavior
{
	public void OnStarted(AbilityBehaviorContext context)
	{
		try
		{
			Fire(context);
		}
		finally
		{
			context.InstanceHandle.End();
		}
	}

	public void OnEnded(AbilityBehaviorContext context)
	{
	}

	private void Fire(AbilityBehaviorContext context)
	{
		if (context.Owner is not ActorEntity entity || entity.Context is not { } ctx
			|| def.ArrowCount is null || def.Damage is null || def.ProjectileScene is null || def.Projectile is null)
		{
			return;
		}

		Vector2 origin = ctx.Actor.GlobalPosition;
		int level = context.Level;
		int count = Mathf.RoundToInt(def.ArrowCount.Sample(level));
		List<Node2D> targets = ctx.Targets.NearestEnemies(origin, def.AttackRange, count);
		if (targets.Count == 0)
		{
			return; // a miss costs nothing: no cooldown
		}

		context.AbilityHandle.CommitCooldown();
		float amount = def.Damage.Sample(level);
		foreach (Node2D target in targets)
		{
			Vector2 direction = (target.GlobalPosition - origin).Normalized();
			var projectile = def.ProjectileScene.Instantiate<Projectile>();
			ctx.ProjectileLayer.AddChild(projectile);
			projectile.GlobalPosition = origin;
			projectile.Launch(def.Projectile, entity, amount, direction);
		}

		ctx.RequestAnimation(AnimNames.Attack, (targets[0].GlobalPosition - origin).Normalized());
	}
}
```

- [ ] **Step 4: Run**

```bash
RUNNER=cs tools/run_tests.sh | grep -E "VolleyTest|FAIL|  -|^----|!!"
```
Expected: seven `PASS  Abilities/VolleyTest::...` and `---- 196 passed, 0 failed ----`.
`Curve.Sample` interpolates between the six points; at integer levels it returns the points exactly. `TestDamageComesFromTheLevelCurve...` relies on the damage reaching its target at speed 420 within 0.5 s (distance 100 px).

- [ ] **Step 5: Commit**

```bash
git add port/src/Abilities port/tests/Abilities port/tests/Support/ActorFixture.cs
git commit -m "feat(abilities): VolleyDef and VolleyBehavior" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 8: `DashDef` and `DashBehavior`

**Files:**
- Create: `port/src/Abilities/Attacks/DashDef.cs`, `port/src/Abilities/Attacks/DashBehavior.cs`
- Test: `port/tests/Abilities/DashTest.cs`

**Interfaces:**
- Consumes: `ActiveAbilityDef` (`OwnedTags`), `AbilityContext.Mover`, `Mover.Dash/CancelDash/IsDashing`, `DamageEffects` invulnerability (Task 1).
- Produces: `DashDef : ActiveAbilityDef` — `[Export] float Speed = 480`, `float Duration = 0.18`; the constructor sets `OwnedTags = [state.invulnerable]`; `DashBehavior` — on start commits the cooldown and starts the mover's dash in the mover's current direction; `OnUpdate(dt)` ends the instance when the duration has passed; `OnEnded` cancels a dash that is still running (revoke mid-dash).

- [ ] **Step 1: Write the failing tests**

`port/tests/Abilities/DashTest.cs`:

```csharp
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Abilities;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Abilities;

public class DashTest : TestCase
{
	private static DashDef MakeDash() => new()
	{
		Tag = GameplayTags.AbilityDash,
		Cooldown = DefFactory.CurveOf(1.2f, 1.2f, 1.2f, 1.2f, 1.2f, 1.2f),
		Speed = 480f,
		Duration = 0.18f,
	};

	private (ActorEntity Entity, AbilityContext Context, AbilityGrant Grant) Rig()
	{
		ActorEntity entity = ActorFixture.BareActor(this, new StatBlock());
		AbilityContext context = ActorFixture.Context(this, entity, Vector2.Zero);
		return (entity, context, entity.Grant(MakeDash(), 1));
	}

	private bool Attacked(ActorEntity target)
	{
		ActorEntity attacker = ActorFixture.BareActor(this, new StatBlock());
		double before = target.Attributes[Stats.Health].DisplayValue;
		DamageEffects.Hit(attacker, target, 10f);
		return target.Attributes[Stats.Health].DisplayValue < before;
	}

	public void TestDashIsInvulnerableOnlyWhileActive()
	{
		var (entity, _, grant) = Rig();
		Assert.True(grant.Ability!.Activate(out AbilityActivationFailures _));
		Assert.False(Attacked(entity), "damage refused while dashing");
		entity.Tick(0.3);
		Assert.True(Attacked(entity), "damage accepted once the dash is over");
	}

	public void TestDashMovesTheMoverForItsDurationThenStops()
	{
		var (entity, context, grant) = Rig();
		context.Mover.SetDirection(Vector2.Up);
		grant.Ability!.Activate(out AbilityActivationFailures _);
		Assert.True(context.Mover.IsDashing);
		entity.Tick(0.1);
		Assert.True(context.Mover.IsDashing, "0.1 s of 0.18 s");
		entity.Tick(0.1);
		Assert.False(grant.Ability.IsActive, "the instance ended");
	}

	public void TestDashCoolsDownAfterwards()
	{
		var (entity, _, grant) = Rig();
		Assert.True(grant.Ability!.Activate(out AbilityActivationFailures _));
		entity.Tick(0.3);
		Assert.False(grant.Ability.Activate(out AbilityActivationFailures _), "on cooldown");
		entity.Tick(1.0);
		Assert.True(grant.Ability.Activate(out AbilityActivationFailures _));
	}

	public void TestRevokingMidDashStopsTheDashAndTheInvulnerability()
	{
		var (entity, context, grant) = Rig();
		grant.Ability!.Activate(out AbilityActivationFailures _);
		entity.Tick(0.05);
		Assert.True(context.Mover.IsDashing);
		entity.Revoke(grant);
		Assert.False(context.Mover.IsDashing, "the dash was cancelled");
		Assert.True(Attacked(entity), "and so was the invulnerability");
	}

	public void TestDashDefOwnsTheInvulnerableTagByDefault()
	{
		Assert.SequenceEq(new DashDef().OwnedTags, new[] { GameplayTags.StateInvulnerable });
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: count greater than 0.

- [ ] **Step 3: Implement**

`port/src/Abilities/Attacks/DashDef.cs`:

```csharp
using System.Collections.Generic;
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Abilities;

/// <summary>A short burst of speed. Invulnerability comes from the owned tag <c>state.invulnerable</c> while it runs.</summary>
public partial class DashDef : ActiveAbilityDef
{
	public DashDef()
	{
		OwnedTags = [GameplayTags.StateInvulnerable];
	}

	[Export]
	public float Speed { get; set; } = 480f;

	[Export]
	public float Duration { get; set; } = 0.18f;

	public override IAbilityBehavior CreateBehavior() => new DashBehavior(this);

	public override List<string> Validate()
	{
		List<string> errors = base.Validate();
		if (Speed <= 0f || Duration <= 0f)
		{
			errors.Add("Dash: speed and duration must be > 0");
		}

		return errors;
	}
}
```

`port/src/Abilities/Attacks/DashBehavior.cs`:

```csharp
using Gamesmiths.Forge.Abilities;
using VampireSurvivor.Components;

namespace VampireSurvivor.Abilities;

/// <summary>One dash: starts the mover's dash, then ends the instance when the duration has passed.</summary>
public sealed class DashBehavior(DashDef def) : IAbilityBehavior
{
	private AbilityBehaviorContext? _context;
	private Mover? _mover;
	private double _remaining;

	public void OnStarted(AbilityBehaviorContext context)
	{
		if (context.Owner is not ActorEntity entity || entity.Context is not { } ctx)
		{
			context.InstanceHandle.End();
			return;
		}

		_context = context;
		_mover = ctx.Mover;
		_remaining = def.Duration;
		context.AbilityHandle.CommitCooldown();
		_mover.Dash(_mover.Direction, def.Speed, def.Duration);
	}

	public void OnUpdate(double deltaTime)
	{
		if (_context is null)
		{
			return;
		}

		_remaining -= deltaTime;
		if (_remaining <= 0.0)
		{
			AbilityBehaviorContext context = _context;
			_context = null;
			context.InstanceHandle.End();
		}
	}

	public void OnEnded(AbilityBehaviorContext context)
	{
		_context = null;
		_mover?.CancelDash();
	}
}
```

- [ ] **Step 4: Run**

```bash
RUNNER=cs tools/run_tests.sh | grep -E "DashTest|FAIL|  -|^----|!!"
```
Expected: five `PASS  Abilities/DashTest::...` and `---- 201 passed, 0 failed ----`.
If `TestDashIsInvulnerableOnlyWhileActive` fails at the first assertion, the owned tag was not applied on activation: check `AbilityFactory.Build` passes `activationOwnedTags` and that `OwnedTags` reached the def (the constructor runs before the object initializer, so a test assigning `OwnedTags` replaces it).

- [ ] **Step 5: Commit**

```bash
git add port/src/Abilities port/tests/Abilities
git commit -m "feat(abilities): DashDef and DashBehavior" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 9: `PlayerProfile` and `Player`

**Files:**
- Create: `port/src/Defs/PlayerProfile.cs`, `port/src/Actors/Player/Player.cs`, `port/src/Actors/Player/player.tscn`
- Modify: `port/tests/Support/DefFactory.cs` (add `Profile`), `port/tests/Support/ActorFixture.cs` (add `Player`)
- Test: `port/tests/Defs/PlayerProfileTest.cs`, `port/tests/Actors/PlayerTest.cs`

**Interfaces:**
- Consumes: everything above.
- Produces:
  - `PlayerProfile : Resource` (`Defs`) — `[Export] StatBlock? Stats`, `VisualDef? Visual`, `int SlotCount = 6`, `Godot.Collections.Array<ActiveAbilityDef> InnateAbilities`; `List<string> Validate()`.
  - `Player : CharacterBody2D` (`Actors`) — `[Export]` nodes `Entity`, `Health`, `Hurtbox`, `Mover`, `Visual`, `Collector`, `Camera`, `AutoCaster`; `Setup(PlayerProfile profile, TargetProvider targets, Node projectileLayer, Wallet wallet)` (after entering the tree); `AbilityContext? Context`; `AbilityGrant Grant(ActiveAbilityDef def, int level)` (adds auto-fire abilities to the `AutoCaster`); `void Revoke(AbilityGrant? grant)`; `float GainMultiplier(CurrencyKind kind)`; `LimitCamera(Rect2)`; `[Signal] Died`; C# `event Action<CurrencyKind, int> LootCollected`; joins `Groups.Player`. Input: movement via `InputActions.MoveVector()`, dash key activates `ability.dash`. On death: stops moving and shooting, plays death, `Entity.CleanupDeferred()`, emits `Died`. Scene `res://port/src/Actors/Player/player.tscn`.
  - Tests: `DefFactory.Profile() : PlayerProfile` (default stats, factory visual, no abilities), `ActorFixture.Player(TestCase test, PlayerProfile? profile = null, Wallet? wallet = null) : Player` (scene instantiated and set up with a fresh `TargetProvider` and projectile layer).

- [ ] **Step 1: Write the failing tests**

Add to `port/tests/Support/DefFactory.cs`:

```csharp
	public static PlayerProfile Profile() => new()
	{
		Stats = new StatBlock(),
		Visual = Visual(),
	};
```

Add to `port/tests/Support/ActorFixture.cs` (add `using VampireSurvivor.Actors;`):

```csharp
	/// <summary>A set-up Player (real scene) in the test tree with a fresh TargetProvider and projectile layer (see <c>Context</c>).</summary>
	public static Player Player(TestCase test, PlayerProfile? profile = null, Wallet? wallet = null)
	{
		var layer = test.Adopt(new Node2D());
		var targets = test.Adopt(new TargetProvider());
		var player = test.Adopt(GD.Load<PackedScene>("res://port/src/Actors/Player/player.tscn").Instantiate<Player>());
		player.Setup(profile ?? DefFactory.Profile(), targets, layer, wallet ?? new Wallet());
		targets.Player = player;
		return player;
	}
```

`port/tests/Defs/PlayerProfileTest.cs`:

```csharp
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class PlayerProfileTest : TestCase
{
	public void TestFactoryProfileIsValid()
	{
		Assert.NoErrors(DefFactory.Profile().Validate());
	}

	public void TestNullStatsAndVisualAreReported()
	{
		var profile = new PlayerProfile { Stats = null, Visual = null };
		Assert.Eq(profile.Validate().Count, 2);
	}

	public void TestSlotCountMustBePositive()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.SlotCount = 0;
		Assert.Eq(profile.Validate().Count, 1);
	}

	public void TestNullAndInvalidInnateAbilitiesAreReported()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.InnateAbilities.Add(null!);
		profile.InnateAbilities.Add(new ProbeAbilityDef { Tag = string.Empty });
		Assert.Eq(profile.Validate().Count, 2);
	}
}
```

`port/tests/Actors/PlayerTest.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Abilities;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Actors;

public class PlayerTest : TestCase
{
	private static VolleyDef MakeArrow(bool autoFire) => new()
	{
		Tag = GameplayTags.AbilityArrow,
		Cooldown = DefFactory.CurveOf(0.7f, 0.7f, 0.7f, 0.7f, 0.7f, 0.7f),
		AutoFire = autoFire,
		ProjectileScene = GD.Load<PackedScene>("res://port/src/Actors/Projectile/projectile.tscn"),
		Projectile = new ProjectileDef { Texture = DefFactory.Texture(8, 8), Speed = 420f },
		ArrowCount = DefFactory.CurveOf(1f, 1f, 1f, 1f, 1f, 1f),
		Damage = DefFactory.CurveOf(12f, 24f, 36f, 48f, 60f, 72f),
		AttackRange = 260f,
	};

	private static DashDef MakeDash() => new()
	{
		Tag = GameplayTags.AbilityDash,
		Cooldown = DefFactory.CurveOf(1.2f, 1.2f, 1.2f, 1.2f, 1.2f, 1.2f),
	};

	private Node2D MakeEnemy(Vector2 at)
	{
		var enemy = Adopt(new Node2D { GlobalPosition = at });
		enemy.AddToGroup(Groups.Enemies);
		return enemy;
	}

	private static Node Layer(Player player) => player.Context!.ProjectileLayer;

	private bool Hit(Player player, float amount)
	{
		ActorEntity attacker = ActorFixture.BareActor(this, new StatBlock());
		double before = player.Health.Current();
		DamageEffects.Hit(attacker, player.Entity, amount);
		return player.Health.Current() < before;
	}

	public void TestSetupAppliesProfileStatsAndJoinsTheGroup()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.Stats = new StatBlock { MaxHealth = 77f, MoveSpeed = 140f };
		Player player = ActorFixture.Player(this, profile);
		Assert.AlmostEq(player.Health.Current(), 77.0);
		Assert.AlmostEq(player.Entity.Attributes[Stats.MoveSpeed].DisplayValue, 140.0);
		Assert.True(player.IsInGroup(Groups.Player));
		Assert.AlmostEq(player.GainMultiplier(CurrencyKind.Gold), 1.0);
	}

	public void TestContextCarriesTheActorAndTheRunsWallet()
	{
		var wallet = new Wallet();
		Player player = ActorFixture.Player(this, wallet: wallet);
		Assert.True(player.Context!.Actor == player);
		Assert.True(player.Context.Wallet == wallet);
	}

	public void TestInnateAbilitiesAreGrantedOnSetup()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.InnateAbilities.Add(MakeDash());
		Player player = ActorFixture.Player(this, profile);
		Assert.Eq(player.Entity.Abilities.GrantedAbilities.Count, 1);
	}

	public void TestVolleyWithoutATargetFiresNothingAndStartsNoCooldown()
	{
		Player player = ActorFixture.Player(this);
		AbilityGrant arrow = player.Grant(MakeArrow(autoFire: false), 1);
		for (int i = 0; i < 3; i++)
		{
			arrow.Ability!.Activate(out AbilityActivationFailures _);
		}

		Assert.False(player.Entity.Tags.AllTags.HasTag(GameplayTags.Request(GameplayTags.CooldownArrow)));
		Assert.Eq(Layer(player).GetChildCount(), 0);
	}

	public void TestVolleyFiresAtTheNearestEnemyThenCoolsDown()
	{
		Player player = ActorFixture.Player(this);
		MakeEnemy(new Vector2(400, 0));
		MakeEnemy(new Vector2(120, 0));
		AbilityGrant arrow = player.Grant(MakeArrow(autoFire: false), 1);
		Assert.True(arrow.Ability!.Activate(out AbilityActivationFailures _));
		Assert.Eq(Layer(player).GetChildCount(), 1, "only the enemy within 260 px");
		Assert.True(player.Entity.Tags.AllTags.HasTag(GameplayTags.Request(GameplayTags.CooldownArrow)));
		Assert.False(arrow.Ability.Activate(out AbilityActivationFailures _), "blocked by cooldown");
	}

	public async Task TestAutoFireShootsWhenAnEnemyComesIntoRange()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.InnateAbilities.Add(MakeArrow(autoFire: true));
		Player player = ActorFixture.Player(this, profile);
		await WaitSeconds(0.2);
		Assert.Eq(Layer(player).GetChildCount(), 0, "nothing to shoot yet");
		MakeEnemy(new Vector2(100, 0));
		await WaitSeconds(0.3);
		Assert.True(Layer(player).GetChildCount() >= 1);
		Assert.True(player.Entity.Tags.AllTags.HasTag(GameplayTags.Request(GameplayTags.CooldownArrow)));
	}

	public async Task TestDashKeyAbilityIsInvulnerableOnlyWhileActive()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.InnateAbilities.Add(MakeDash());
		Player player = ActorFixture.Player(this, profile);
		Assert.True(player.Entity.TryActivate(GameplayTags.AbilityDash));
		Assert.False(Hit(player, 10f), "damage refused while dashing");
		await WaitSeconds(0.5);
		Assert.True(Hit(player, 10f));
		Assert.AlmostEq(player.Health.Current(), 90.0);
	}

	public async Task TestLethalDamageEmitsDiedAndStopsTheShooting()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.InnateAbilities.Add(MakeArrow(autoFire: true));
		Player player = ActorFixture.Player(this, profile);
		int deaths = 0;
		player.Died += () => deaths++;
		Hit(player, 9999f);
		Assert.Eq(deaths, 1);
		MakeEnemy(new Vector2(100, 0));
		await WaitSeconds(0.3);
		Assert.Eq(Layer(player).GetChildCount(), 0, "a dead player no longer shoots");
	}

	public void TestGrantAndRevokeKeepTheAutoCasterInSync()
	{
		Player player = ActorFixture.Player(this);
		AbilityGrant grant = player.Grant(MakeArrow(autoFire: true), 1);
		Assert.Eq(player.AutoCaster.Count, 1);
		player.Revoke(grant);
		Assert.Eq(player.AutoCaster.Count, 0);
		Assert.Eq(player.Entity.Abilities.GrantedAbilities.Count, 0);
		player.Revoke(null);
	}

	public void TestPlayerAttackScalesWithTheAttackStat()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.Stats = new StatBlock { Attack = 2f };
		Player player = ActorFixture.Player(this, profile);
		var (victim, _) = ActorFixture.HurtTarget(this, new Vector2(10, 0));
		DamageEffects.Hit(player.Entity, victim, 10f);
		Assert.AlmostEq(victim.Attributes[Stats.Health].DisplayValue, 80.0);
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: count greater than 0.

- [ ] **Step 3: Implement**

`port/src/Defs/PlayerProfile.cs`:

```csharp
using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.Defs;

/// <summary>
/// The player's starting stats, look and abilities for a run. The shop catalog (slice 3) and the skill-tree bonuses (slice 5)
/// are added by later slices.
/// </summary>
public partial class PlayerProfile : Resource
{
	[Export]
	public StatBlock? Stats { get; set; }

	[Export]
	public VisualDef? Visual { get; set; }

	/// <summary>Ability slots shown in the HUD.</summary>
	[Export]
	public int SlotCount { get; set; } = 6;

	/// <summary>Abilities the player starts with, granted at level 1 (the arrow, the dash).</summary>
	[Export]
	public Godot.Collections.Array<ActiveAbilityDef> InnateAbilities { get; set; } = [];

	public List<string> Validate()
	{
		var errors = new List<string>();
		if (Stats is null)
		{
			errors.Add("PlayerProfile: stats is null");
		}
		else
		{
			errors.AddRange(Stats.Validate());
		}

		if (Visual is null)
		{
			errors.Add("PlayerProfile: visual is null");
		}
		else
		{
			errors.AddRange(Visual.Validate());
		}

		if (SlotCount < 1)
		{
			errors.Add("PlayerProfile: slot_count must be >= 1");
		}

		foreach (ActiveAbilityDef? ability in InnateAbilities)
		{
			if (ability is null)
			{
				errors.Add("PlayerProfile: null innate ability");
			}
			else
			{
				errors.AddRange(ability.Validate());
			}
		}

		return errors;
	}
}
```

`port/src/Actors/Player/Player.cs`:

```csharp
using System;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Actors;

/// <summary>
/// The player's actor: input -> Mover, dash key -> ability tag, loot magnet, camera. It does not know which abilities it has:
/// every ability is a grant on its entity, whether innate (from the profile) or granted at runtime by the loadout.
/// </summary>
public partial class Player : CharacterBody2D
{
	[Signal]
	public delegate void DiedEventHandler();

	public event Action<CurrencyKind, int>? LootCollected;

	[Export]
	public ActorEntity Entity { get; set; } = null!;

	[Export]
	public Health Health { get; set; } = null!;

	[Export]
	public Hurtbox Hurtbox { get; set; } = null!;

	[Export]
	public Mover Mover { get; set; } = null!;

	[Export]
	public ActorVisual Visual { get; set; } = null!;

	[Export]
	public LootCollector Collector { get; set; } = null!;

	[Export]
	public Camera2D Camera { get; set; } = null!;

	[Export]
	public AutoCaster AutoCaster { get; set; } = null!;

	public AbilityContext? Context => Entity.Context;

	/// <summary>Call after the player has entered the tree.</summary>
	public void Setup(PlayerProfile profile, TargetProvider targets, Node projectileLayer, Wallet wallet)
	{
		Entity.ApplyStats(profile.Stats!);
		Health.Bind(Entity);
		Hurtbox.Bind(Entity, this);
		Mover.Bind(this, Entity);
		Visual.Apply(profile.Visual!);
		Collector.Bind(this);
		Entity.Context = new AbilityContext(this, Mover, targets, projectileLayer, wallet);
		Entity.Context.AnimationRequested += OnAnimationRequested;
		foreach (ActiveAbilityDef ability in profile.InnateAbilities)
		{
			Grant(ability, 1);
		}

		Collector.Collected += (kind, amount) => LootCollected?.Invoke(kind, amount);
		Health.Damaged += _ => Visual.FlashHit();
		Health.Died += OnDied;
		AddToGroup(Groups.Player);
	}

	public float GainMultiplier(CurrencyKind kind)
	{
		return Entity.Attributes[kind == CurrencyKind.Gold ? Stats.GoldGain : Stats.StoneGain].DisplayValue;
	}

	/// <summary>Grants an ability; attacks (<see cref="ActiveAbilityDef.AutoFire"/>) are also handed to the auto-caster.</summary>
	public AbilityGrant Grant(ActiveAbilityDef def, int level)
	{
		AbilityGrant grant = Entity.Grant(def, level);
		if (def.AutoFire)
		{
			AutoCaster.Add(grant);
		}

		return grant;
	}

	public void Revoke(AbilityGrant? grant)
	{
		AutoCaster.Remove(grant);
		Entity.Revoke(grant);
	}

	public void LimitCamera(Rect2 bounds)
	{
		Camera.LimitLeft = (int)bounds.Position.X;
		Camera.LimitTop = (int)bounds.Position.Y;
		Camera.LimitRight = (int)bounds.End.X;
		Camera.LimitBottom = (int)bounds.End.Y;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Health.IsDead)
		{
			Mover.SetDirection(Vector2.Zero);
			return;
		}

		Vector2 input = InputActions.MoveVector();
		Mover.SetDirection(input);
		Visual.SetMoving(Mover.IsMoving);
		Visual.Face(input.X);
		if (Input.IsActionJustPressed(InputActions.Dash))
		{
			Entity.TryActivate(GameplayTags.AbilityDash);
		}
	}

	private void OnAnimationRequested(StringName animation, Vector2 facing)
	{
		Visual.Face(facing.X);
		Visual.PlayOnce(animation);
	}

	private void OnDied()
	{
		AutoCaster.Enabled = false;
		Visual.PlayOnce(AnimNames.Death);
		Entity.CleanupDeferred();
		EmitSignal(SignalName.Died);
	}
}
```

`port/src/Actors/Player/player.tscn`:

```
[gd_scene load_steps=14 format=3]

[ext_resource type="Script" path="res://port/src/Actors/Player/Player.cs" id="1_player"]
[ext_resource type="PackedScene" path="res://port/src/Components/actor_entity.tscn" id="2_entity"]
[ext_resource type="Script" path="res://port/src/Components/Health.cs" id="3_health"]
[ext_resource type="Script" path="res://port/src/Components/Hurtbox.cs" id="4_hurtbox"]
[ext_resource type="Script" path="res://port/src/Components/Mover.cs" id="5_mover"]
[ext_resource type="Script" path="res://port/src/Components/ActorVisual.cs" id="6_visual"]
[ext_resource type="Script" path="res://port/src/Actors/Pickup/LootCollector.cs" id="7_collector"]
[ext_resource type="Script" path="res://port/src/Components/AutoCaster.cs" id="8_caster"]

[sub_resource type="CircleShape2D" id="CircleShape2D_body"]
radius = 8.0

[sub_resource type="CircleShape2D" id="CircleShape2D_hurt"]
radius = 10.0

[sub_resource type="CircleShape2D" id="CircleShape2D_magnet"]
radius = 70.0

[node name="Player" type="CharacterBody2D" node_paths=PackedStringArray("Entity", "Health", "Hurtbox", "Mover", "Visual", "Collector", "Camera", "AutoCaster")]
collision_layer = 2
collision_mask = 1
motion_mode = 1
script = ExtResource("1_player")
Entity = NodePath("ActorEntity")
Health = NodePath("Health")
Hurtbox = NodePath("Hurtbox")
Mover = NodePath("Mover")
Visual = NodePath("Visual")
Collector = NodePath("Collector")
Camera = NodePath("Camera2D")
AutoCaster = NodePath("AutoCaster")

[node name="CollisionShape2D" type="CollisionShape2D" parent="."]
shape = SubResource("CircleShape2D_body")

[node name="ActorEntity" parent="." instance=ExtResource("2_entity")]

[node name="Health" type="Node" parent="."]
script = ExtResource("3_health")

[node name="Hurtbox" type="Area2D" parent="."]
collision_layer = 8
collision_mask = 0
monitoring = false
script = ExtResource("4_hurtbox")

[node name="CollisionShape2D" type="CollisionShape2D" parent="Hurtbox"]
shape = SubResource("CircleShape2D_hurt")

[node name="Mover" type="Node" parent="."]
script = ExtResource("5_mover")

[node name="Visual" type="AnimatedSprite2D" parent="."]
script = ExtResource("6_visual")

[node name="Collector" type="Area2D" parent="."]
collision_layer = 0
collision_mask = 32
monitorable = false
script = ExtResource("7_collector")

[node name="CollisionShape2D" type="CollisionShape2D" parent="Collector"]
shape = SubResource("CircleShape2D_magnet")

[node name="AutoCaster" type="Node" parent="."]
script = ExtResource("8_caster")

[node name="Camera2D" type="Camera2D" parent="."]
zoom = Vector2(2, 2)
position_smoothing_enabled = true
```

- [ ] **Step 4: Run**

```bash
RUNNER=cs IMPORT=1 tools/run_tests.sh | grep -E "PlayerProfileTest|PlayerTest|FAIL|  -|^----|!!"
```
Expected: four + ten `PASS` lines and `---- 215 passed, 0 failed ----`.
`TestPlayerAttackScalesWithTheAttackStat` checks the Forge path (source Attack 2.0 x 10 = 20 damage); the other Player tests need real time (0.2-0.5 s waits).

- [ ] **Step 5: Commit**

```bash
git add port/src/Defs/PlayerProfile.cs port/src/Actors/Player port/tests
git commit -m "feat(actors): PlayerProfile and Player" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Spec notes and the slice-2b check

**Files:**
- Modify: `docs/superpowers/specs/2026-10-14-csharp-forge-port-design.md`

- [ ] **Step 1: Run both suites**

```bash
tools/run_tests.sh | grep -E "^==|^----|FAIL|!!" | grep -v "^\[Main\]"
```
Expected: GDScript `---- 648 passed, 0 failed ----` and C# `---- 215 passed, 0 failed ----`.

- [ ] **Step 2: Update the spec**

In section 4 (after the "Slice 2a notes" paragraph) add:

```
Slice 2b notes: actors are Godot nodes that own an `ActorEntity` child and are wired in `Setup(...)`; node-typed exports use `node_paths` in the hand-written `.tscn`. `AbilityContext` (plain class) lives on `ActorEntity.Context`, which is how ability behaviors reach the mover, the targets and the projectile layer; animations are requested through its `AnimationRequested` event. Attacks are added to the owner's `AutoCaster` by the owner (`Player.Grant` does it for `ActiveAbilityDef.AutoFire` abilities); on-demand abilities (dash) are activated by tag (`ActorEntity.TryActivate`). The innate Arrow is a `VolleyDef` with an arrow count of 1 at every level. `DamageEffects.Hit` refuses a target carrying `state.invulnerable` (checked on the target's tags before the effect is built). `PlayerProfile` currently holds stats, visual, slot count and innate abilities; the shop catalog (slice 3) and skill bonuses (slice 5) are added later. Enemy/projectile/pickup signal payloads that are not Variant-compatible (`DropRoll`, `CurrencyKind`) are C# events.
```

- [ ] **Step 3: Commit and report**

```bash
git add docs/superpowers/specs/2026-10-14-csharp-forge-port-design.md docs/superpowers/plans/2026-10-14-csharp-port-slice-2b-actors.md
git commit -m "docs: spec notes for slice 2b" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
Tell the owner 2b is done and that **2c (Stage + HUD + dev scene + first `.tres` data + the 100-enemy FPS check)** is next; it needs the legacy stage/HUD code and the textures in `assets/`, so it gets its own plan.
