# C# + Forge Port — Slice 3 (Shop) Implementation Plan

> **For agentic workers:** executed inline with superpowers:executing-plans (TDD per task, one final whole-branch review).
> This plan is written for an executor who has the GDScript reference open: every ported test names its GDScript source,
> and the exact numbers in the asserts are the ones in that source. New code (where the C# design differs from GDScript)
> is given in full or as a precise interface.

**Goal:** Port the ability shop: `AbilityDef` (shop catalog entry), `GradeTable`, `Loadout`/`AbilitySlot`, `AbilityPool`,
`GradeResult`, `GradeService`, `AbilityInstaller`, the two remaining attacks (Blade Sweep with `AreaStrike`, Bounty on
`event.kill`), the HUD `AbilityBar`/`AbilitySlotView`, the shop data (7 abilities + grade table), and wire it through
`StageRoot` and `DevStage` so the C# dev scene plays like GDScript Phase 2.

**Architecture:** Plain C# models in `Abilities/` (Loadout, pool, service, installer) with C# events; the installer mirrors
slots onto the player's `ActorEntity` (passives as effect handles, attacks as `AbilityGrant`s through `Player.Grant`).
The HUD bar only reads models and emits `PromoteRequested`/`RerollRequested`; `DevStage` (later `Main`) routes them.

**Spec:** `docs/superpowers/specs/2026-10-14-csharp-forge-port-design.md` (sections 3, 4, 5, 7.3 slice 3). GDScript
reference: `src/abilities/`, `src/defs/ability_def.gd`, `src/defs/grade_table.gd`, `src/components/area_strike.gd`,
`src/ui/hud/ability_bar.gd`, `src/ui/hud/ability_slot_view.gd`, `data/abilities/`, `data/grade_table.tres`; tests in
`tests/abilities/`, `tests/ui/test_ability_bar.gd`, `tests/defs/test_ability_def.gd`, `tests/defs/test_grade_table.gd`,
`tests/data/test_ability_data.gd`, the shop tests in `tests/stage/test_stage.gd`.

## Global Constraints

Same as slice 2c (`plans/2026-10-14-csharp-port-slice-2c-stage-hud.md`): branch `port/slice-3`; explicit `git add`;
commits end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; `RUNNER=cs` for the C# suite, GDScript stays
at 648; no `[GlobalClass]` on names that collide with GDScript `class_name`s (`AbilityDef`, `GradeTable`, `Loadout`,
`AbilitySlot`, `AbilityPool`, `GradeResult`, `GradeService`, `AbilityInstaller`, `AbilityBar`, `AbilitySlotView`,
`AreaStrike`); namespace = top-level folder; Godot signals for Node-to-Node, C# events for plain classes, plain events
unsubscribed in `_ExitTree`; damage only through `DamageEffects.Hit`; tags only via `GameplayTags` (letters and dots).
Expected C# totals start at **289**.

## Decisions (binding for this slice)

1. **Catalog entry.** `Defs/AbilityDef : Resource { Id, DisplayName, Description, Icon, RevealedIcon, MinGrade (Core.Grade),
   PassiveDef? Passive, ActiveAbilityDef? Active; Texture2D? IconFor(Grade); List<string> Validate() }`. Validate: id,
   display name, icon, rollable min grade, at least one of Passive/Active, nested `Validate()`. The GDScript "scene root
   must be a GameplayAbility" check is gone (typed at compile time, spec 3).
2. **Per-slot cooldowns (open decision from 2b, settled here).** GDScript gives every slot's attack its own timer, so two
   slots holding Volley fire twice as often (CLAUDE.md: "two slots holding the same ability stack on purpose"). Forge
   cooldowns block by tag, so the installer gives each slot its own cooldown tag: `GameplayTags.SlotCooldowns` =
   `cooldown.slot.a` … `cooldown.slot.j` (10 tags, letters only to satisfy the tag regex); `PlayerProfile.MaxSlots = 10`.
   `AbilityFactory.Build(def, string? cooldownTag = null)`, `ActorEntity.Grant(def, level, string? cooldownTag = null)` and
   `Player.Grant(def, level, string? cooldownTag = null)` pass it through; the null default keeps `cooldown.<tail>` for
   innate abilities. `ActorEntity`'s grant books are keyed by the effective cooldown tag (stored on `AbilityGrant` as
   `CooldownTag`), so sharing semantics are unchanged for innate abilities.
3. **Install order.** Passives: apply the new effect, then remove the old (a later heal source must not lose HP to a
   remove-then-apply dip; spec 4 "install before remove"). Attacks: revoke the old grant, then grant the new one, so the
   slot's cooldown book empties and a promoted/rerolled attack starts with a fresh cooldown (CLAUDE.md).
4. **Bounty.** `ActiveAbilityDef.TriggerEventTag` (string, empty = none). `AbilityFactory` sets
   `AbilityTriggerData.ForEvent<Node>(GameplayTags.Request(tag), 0)`; every game event payload is a `Node`.
   `BountyBehavior : IAbilityBehavior<Node>` credits `round(GoldPerKill.Sample(level))` gold (half away from zero) to
   `AbilityContext.Wallet` and ends. `AutoFire = false`.
5. **AreaStrike.** `Components/AreaStrike : Node2D` (+ `area_strike.tscn`: Hitbox on EnemyHurt, Lifetime):
   `Strike(ActorEntity attacker, float damage, float radius, float seconds, int targetMask = PhysicsLayers.EnemyHurt,
   Node2D? onlyActor = null)`, `[Signal] Struck(Hurtbox)`, hits each hurtbox once via `DamageEffects.Hit`, draws a ring,
   frees itself on expiry. GDScript `MeleeStrike` was only a name; Blade Sweep spawns `area_strike.tscn` directly.
6. **AutoCaster churn** (open decision from 2b): unchanged. The player holds at most ~8 auto-fire grants; an activation
   that finds nothing ends in the same call and commits no cooldown, matching GDScript's "a miss costs nothing".
7. **Out of scope:** the 300-enemy frame cost (separate follow-up), ProfileBuilder/skill bonuses (slice 5).

## Review Focus

1. Two slots holding the same attack must both fire on their own cooldowns (Task 3, 7).
2. A promoted or rerolled attack fires at once (fresh cooldown), while an untouched slot keeps cooling down (Task 7).
3. Clearing or replacing a slot mid-activation removes the ability's owned tags at once (Task 7).
4. A refused shop action never spends anything, ticket included (Task 6).
5. A rebound or freed ability bar never reacts to the old loadout/wallet/service (Task 9).

---

## Tasks

Each task: write the listed tests first, watch them fail (compile failure counts), implement, run `RUNNER=cs`, commit.

### Task 1: `GradeTable`, `AbilityDef`, profile catalog, stage grade table
- Create `port/src/Defs/GradeTable.cs` (`PromoteSteps = 5`; `[Export] int[] PromoteGold`, `RevealStone`, `RerollStone`;
  `Price? PromotePrice(Grade)`, `Price? RerollPrice(Grade)`, `Validate()`), `port/src/Defs/AbilityDef.cs` (decision 1).
- `PlayerProfile`: `[Export] Array<AbilityDef> Abilities`, `const int MaxSlots = 10`, validate `1 <= SlotCount <= MaxSlots`
  and each ability. `StageDef`: `[Export] GradeTable? GradeTable` (required; nested validate).
- `DefFactory`: `GradeTable()` (100,150,250,400,600 / 10 / 2), `Ability(id, PassiveDef? passive = null,
  ActiveAbilityDef? active = null, Grade minGrade = Common)`, `VitalityAbility()`; `Stage()` sets the grade table.
- Tests: `Defs/GradeTableTest` (5, from `test_grade_table.gd`, the Price test included), `Defs/AbilityDefTest`
  (valid passive/active/hybrid; empty def = 4 errors; min grade None/Revealed = 1 error each; nested passive error
  propagates; `IconFor`), `Defs/PlayerProfileTest` + 1 (slot count above MaxSlots), `Defs/StageDefTest` + 1 (missing grade
  table and its nested errors). Port data (`stage_01.tres`) gets the grade table in Task 10; until then
  `DataFilesTest` would fail, so this task also adds `port/data/grade_table.tres` and references it from `stage_01.tres`.

### Task 2: `ActiveAbilityDef.TriggerEventTag` and `AbilityFactory` trigger
- Tests (`Components/AbilityGrantTest`): an ability with `TriggerEventTag = event.kill` starts when the entity raises
  `event.kill` (Raise<Node>) and receives the payload; it ignores other events; a revoked grant no longer reacts;
  `AbilityFactory.Validate` reports an unregistered trigger tag. Uses a test def implementing `IAbilityBehavior<Node>`.

### Task 3: per-slot cooldown tags
- `GameplayTags.SlotCooldowns` (10 tags `cooldown.slot.a`..`j`, added to `All`), regenerate `forge_data.tres` with
  `port/tools/sync_tags.tscn`; `AbilityGrant` gains `string CooldownTag`; `AbilityFactory.Build(def, cooldownTag)`,
  `ActorEntity.Grant(def, level, cooldownTag)`, `Player.Grant(def, level, cooldownTag)`; books keyed by cooldown tag.
- Tests: two grants of one def with different slot tags each fire and cool down independently; a grant with a slot tag
  carries that tag, not `cooldown.<tail>`; `GameplayTags.SlotCooldowns.Length == PlayerProfile.MaxSlots`; existing
  shared-cooldown test still green.

### Task 4: `AreaStrike` + Blade Sweep
- `port/src/Components/AreaStrike.cs` + `area_strike.tscn` (decision 5); `port/src/Abilities/Attacks/BladeSweepDef.cs`
  (`StrikeScene`, `Radius`, `Damage` curves, `StrikeSeconds`; validate) + `BladeSweepBehavior` (nearest enemy within
  `Radius(level)`, else end without cooldown; commit cooldown; spawn strike at the actor on the projectile layer; request
  the attack animation).
- Tests: `Components/AreaStrikeTest` (from `test_melee_strike.gd`: damages each hurtbox in radius once then frees; radius
  is per instance; `onlyActor` filters), `Abilities/BladeSweepTest` (from `test_blade_sweep_ability.gd`: reach 40 at level
  1; cooldown 1.4 s at level 2; level 6 radius 95 / damage 45 → victim 55; validate counts).

### Task 5: Bounty
- `BountyDef` (`GoldPerKill` curve, `TriggerEventTag = event.kill` set in the constructor) + `BountyBehavior`.
- Tests (from `test_bounty_ability.gd`): level 4 pays 5 per kill (payload any node, or a plain Node); level 1 → 1,
  level 6 → 12; other events ignored; revoked → nothing; no context/wallet → nothing, no throw; validate needs the curve.

### Task 6: `AbilitySlot`, `Loadout`, `AbilityPool`, `GradeResult`, `GradeService`
- Plain classes in `port/src/Abilities/`. `Loadout.SlotChanged` is `event Action<int, AbilitySlot>`;
  `GradeService.Resolved` is `event Action<int, GradeResult>`; `GradeOutcome { Ok, NotEnough, NotApplicable, EmptyPool }`.
- Tests: `LoadoutTest` (5, `test_loadout.gd`), `AbilityPoolTest` (6, `test_ability_pool.gd`), `GradeServiceTest`
  (18, `test_grade_service.gd`).

### Task 7: `AbilityInstaller`
- Plain class: `Bind(Loadout, Player)` (unbinds a previous loadout, uninstalls everything, installs current slots),
  `Unbind()`, `ActiveEffectHandle? InstalledEffect(int)`, `AbilityGrant? InstalledGrant(int)`, `UninstallAll()`;
  decisions 2 and 3.
- Tests (from `test_ability_installer.gd`, adapted): passive at grade level and replaced not stacked; binding installs
  pre-filled slots; changing the ability removes the old passive; two slots same passive stack; attacks granted and
  replaced (fresh grant at the new level, old revoked); clearing uninstalls everything; 40 random changes leave base stats
  and no grants; changing a slot mid-activation strips owned tags; rebinding does not double install; uninstall after the
  player's entity was cleaned up does not throw; clearing Vitality reclamps health; raising max health does not heal;
  **new:** two slots holding the same attack both fire (stacking, Review Focus 1); a promoted attack fires at once while
  another slot keeps its cooldown (Review Focus 2).

### Task 8: `StageRoot` shop wiring
- `StageRoot` builds `Loadout(profile.SlotCount)`, `GradeService(loadout, new AbilityPool(profile.Abilities),
  def.GradeTable, wallet, rng)` and `AbilityInstaller.Bind(loadout, Player)` in `Start`; exposes `Loadout`, `GradeService`.
- Tests (from `test_stage.gd` shop tests): stage builds loadout and service from the profile (2 slots, 200 gold, promote
  cost 100, no reroll on empty); buying/promoting/revealing Vitality changes max health 120 → 260 → 340 with the exact
  wallet numbers; an attack (Volley) is granted to the player and auto-fires; Bounty pays 1 gold per kill at Common.

### Task 9: `AbilitySlotView` + `AbilityBar` + HUD
- `port/src/Ui/Hud/AbilitySlotView.cs` + `ability_slot_view.tscn`, `AbilityBar.cs` + `ability_bar.tscn` (ports of the
  GDScript views; `KindSuffixes` g/s/T; tooltip text identical); `Hud` gains `[Export] AbilityBar AbilityBar` placed as in
  the GDScript `hud.tscn`.
- Tests: `Ui/AbilityBarTest` (8, `test_ability_bar.gd`), `Ui/HudTest` + 1 (the HUD hosts an unbound ability bar), plus
  **new:** a freed bar does not throw when the old wallet/loadout/service change (Review Focus 5).

### Task 10: shop data + DevStage routing
- `port/data/abilities/{prospector,swift_boots,vitality,might,volley,blade_sweep,bounty}.tres` (numbers and icons from
  `data/abilities/*.tres` and the ability scenes), `area_strike` scene reference for Blade Sweep; default profile
  `Abilities` lists all seven in the GDScript order.
- `DevStage` binds `Hud.AbilityBar` to `Stage.Loadout`, the wallet and `Stage.GradeService`, and routes
  `PromoteRequested`/`RerollRequested` to the service only while no result is shown.
- Tests: `Data/DataFilesTest` + passives (Prospector gold gain 1.25 / 3.5, Swift Boots move speed 110 / 185, Vitality
  max health 120 / 340, Might attack 1.1 / 2.0 at levels 1 / 6, strictly increasing curves), every catalog def validates,
  profile offers the seven in order; `App/DevStageTest` + 2 (a promote request buys a slot; requests after the result
  are ignored).

### Task 11: notes, full suite, review, merge
- Status note (slice 3 done, slice 4 next, totals), spec slice 3 notes (decisions 2-6), full `IMPORT=1` run, commit
  `.cs.uid`, fresh-context final review, fix Critical/Important test-first, merge and push (owner pushes if auth fails).
