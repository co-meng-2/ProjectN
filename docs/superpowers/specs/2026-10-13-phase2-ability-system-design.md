# Phase 2 — Ability System: Design Spec

Date: 2026-10-13
Parent spec: `2026-10-13-vampire-survivor-base-design.md` (module contract, layers, conventions — still binding)
Builds on: Phase 1 (merged on `main`, see `CLAUDE.md`)

## 1. Intent

The player buys, upgrades and rerolls **abilities** during a stage through six HUD slots, spending the **gold** and **stone** that enemies drop. The system must be data-driven (new abilities are `.tres` files), built on GodotGAS (passives are infinite effects, attacks are granted abilities), and testable without scenes where it is pure logic.

## 2. Owner decisions (verbatim intent)

- The HUD shows **6 ability slots**. **Click** a slot = promote; **Ctrl+click** = reroll.
- `none → common` costs **100 gold**. The run starts with **200 gold**.
- Grades: `common < rare < epic < unique < legendary < revealed`.
  - `common → rare → epic → unique → legendary`: costs gold (rising); the ability is **re-rolled at random from the target grade's pool** (it may become a different ability).
  - `legendary → revealed`: costs **stone**; it is the **same ability's enhanced form** (deterministic).
  - Reroll costs **stone** and re-picks at random within the **same** grade.
- Ability kinds in scope: **stat passives, attacks (ranged and melee), hybrids**.
- Prototype tuning: enemy drops are multiplied by a **data variable** (`drop_multiplier`, set very high in the prototype stage) so every grade can be reached in a short test run.

## 3. Defaults chosen by the designer (all data, adjustable)

| Item | Value |
|---|---|
| none → common | 100 gold (owner) |
| common → rare | 150 gold |
| rare → epic | 250 gold |
| epic → unique | 400 gold |
| unique → legendary | 600 gold |
| legendary → revealed | 10 stone |
| reroll | 2 stone; not allowed on `none` or `revealed` |
| start gold / start stone | 200 / 0 (owner: 200 gold) |
| slot count | 6 (owner) |
| `drop_multiplier` | field default 1.0; `stage_01.tres` sets 10000 (owner's prototype suggestion; set back to 1 for balanced play) |

## 4. Data model

- `core/Grade`: `enum Value { NONE, COMMON, RARE, EPIC, UNIQUE, LEGENDARY, REVEALED }`; `next(grade)`, `level(grade) -> float` (COMMON 1 … REVEALED 6; this is the GAS `ability_level` / curve X), `display_name(grade)`, `color(grade)`.
- `defs/AbilityDef` (Resource): `id`, `display_name`, `description`, `icon`, optional `revealed_icon`, `min_grade` (lowest grade at which the ability appears in a pool), optional `passive_effect: GameplayEffect` (INFINITE; modifiers use `scaling_curve` over grade level), optional `ability_scene: PackedScene` (root script extends `ActorAbility`), `validate()`. An ability may have either or both (a hybrid has both or an event-triggered `ability_scene`).
- `defs/GradeTable` (Resource): `promote_gold: PackedInt32Array` (5 entries indexed by the grade being left: NONE, COMMON, RARE, EPIC, UNIQUE), `reveal_stone`, `reroll_stone`, `validate()`.
- `StageDef` gains: `start_gold`, `start_stone`, `drop_multiplier`, `grade_table: GradeTable`. `PlayerProfile` gains: `slot_count := 6`, `abilities: Array[AbilityDef]` (the catalog the pools are built from; Phase 4 skill tree will extend it).
- Everything above has `validate()` and is validated by `Stage.start` like the Phase 1 defs.

## 5. Modules

New folder `src/abilities/` (layer 6, beside `stage/`): may use actors/components/gas/defs/core; `ui/` may use it.

| Module | Single responsibility | Interface |
|---|---|---|
| `abilities/AbilitySlot` (RefCounted) | One slot's state | `def: AbilityDef`, `grade: Grade.Value` |
| `abilities/Loadout` (RefCounted) | The N slots | `signal slot_changed(index, slot)`; `slot(i)`, `set_slot(i, def, grade)`, `size()` |
| `abilities/AbilityPool` (RefCounted) | Which defs can appear at a grade | `_init(defs)`, `defs_for(grade)`, `roll(grade, rng, exclude: AbilityDef = null) -> AbilityDef` (null when empty) |
| `abilities/GradeService` (RefCounted) | The rules, pure logic over `Wallet`, `RunRng`, `Loadout`, `AbilityPool`, `GradeTable` | `promote(i) -> Result`, `reroll(i) -> Result`, `promote_cost(i)`, `reroll_cost(i)` (for the UI); `Result.Outcome { OK, NOT_ENOUGH, NOT_APPLICABLE, EMPTY_POOL }`; atomic: roll first, then spend, then write the slot |
| `abilities/AbilityInstaller` (Node) | Mirrors the loadout onto the player's ASC | `bind(loadout, player)`; on `slot_changed` removes the old slot's effect handle/granted ability, then applies `passive_effect` at `Grade.level(grade)` and/or grants a fresh `ability_scene` instance (`ability_level = level`) through `Player.grant_ability` |
| `actors/player/Player` (extended) | Hosts granted abilities | `ability_context: AbilityContext`, `grant_ability(ability: ActorAbility)`, `revoke_ability(ability)` — no ability-specific knowledge |
| `ui/hud/AbilityBar` + `AbilitySlotView` | View + input of the 6 slots | `bind(loadout, wallet, service)`; emits `promote_requested(index)`, `reroll_requested(index)` (Ctrl+click); shows icon, grade-coloured frame, next cost, dimmed when unaffordable |

`Stage` (composition root) creates `AbilityPool`, `Loadout`, `GradeService`, `AbilityInstaller` from the defs and exposes `loadout` and `grade_service`; `Main` binds the `AbilityBar` and routes its request signals to `grade_service` (the stage still knows nothing about `ui/`). `RunState` gets the starting gold/stone. `AbilityContext` gains `wallet`.

## 6. Abilities shipped in Phase 2 (7, all `.tres` data + scenes)

Stat passives (one `passive_effect` each, `scaling_curve` per modifier gives the value per grade level 1–6, revealed = 6):
1. **Prospector** — `gold_gain` (+).
2. **Swift Boots** — `move_speed` (+%).
3. **Vitality** — `max_health` (+).
4. **Might** — `attack` (+%).

Attacks (`ActorAbility` scenes, auto-fire like `ArrowAbility`, scaling from `ability_level`):
5. **Volley** — ranged: periodically fires N extra arrows at the N nearest enemies (N and cooldown scale with level).
6. **Blade Sweep** — melee: periodic radial hit around the player (radius/damage/cooldown scale with level); reuse `Hitbox`/`EffectApplier`/`Lifetime`, no new damage path.

Hybrid:
7. **Bounty** — `ActorAbility` with `trigger_event_tag = Event.Kill`: on every kill adds gold to the wallet (amount scales with level).

Kill events: `Stage` already counts kills; it additionally sends `Event.Kill` (new tag) to the player's ASC for every enemy death (payload = the enemy). Any future on-kill ability reuses it.

Each ability's numbers live in its `.tres` (curves) — none in code. Icons are copied into `res://assets/art/` (apple-style icon families from the art library fit the apple → shiny apple idea; `revealed_icon` or a shiny tint marks revealed).

## 7. Behaviour rules

- Click on an **empty** slot: pay `promote_gold[NONE]`, roll a COMMON def. Click on a filled slot: promote as per §2. Ctrl+click: reroll.
- Insufficient currency or non-applicable action: nothing changes; the slot view flashes/dims (no error).
- Re-rolling or promoting to a pool that offers the same ability again is allowed (reroll prefers a different one when the pool has alternatives).
- Removing the old slot content always happens before installing the new one (no stacked leftovers); installed GAS handles are tracked per slot.
- Two slots may hold the same ability; effects/abilities stack through GAS.
- The stage keeps running while the player clicks (no pause).

## 8. Out of scope (later phases)

Events/boss (Phase 3), menus/skill tree/save (Phase 4), selling/removing abilities, ability tooltips beyond name + cost, sound.

## 9. Testing & acceptance

Pure logic (`Grade`, `AbilityPool`, `GradeService`, `Loadout`) has unit tests with seeded `RunRng`. `AbilityInstaller` is tested with a real `Player`: promoting installs the effect/ability, promoting again replaces it, rerolling replaces it, nothing leaks after many changes. Each ability has a behaviour test (stat changes per grade level; Volley fires N projectiles; Sweep hits enemies in radius; Bounty credits gold on `Event.Kill`). `AbilityBar` is tested for signals and displayed costs. A `Stage` integration test buys/promotes/rerolls through `grade_service` and checks the wallet and the player's attributes. Editor acceptance: run the stage with the prototype drop multiplier, buy and promote slots by mouse, Ctrl+click reroll, observe stat/attack changes, no log errors.

## 10. Risks

- Currency values: `drop_multiplier` makes the economy trivial on purpose; real tuning is a later data pass.
- `max_health` passives do not heal; documented behaviour.
- GAS INFINITE effects stack under `FREE` stacking; same-ability duplicates multiply by design.
- Mouse clicks on HUD slots must not leak into gameplay (arrows are auto-fired, so there is no click-to-shoot conflict).

## 11. As built (2026-10-13)

Deviations from this design:
- Outcomes are a `GradeResult` class (`Outcome {OK, NOT_ENOUGH, NOT_APPLICABLE, EMPTY_POOL}`), and costs are `Price(kind, amount)` objects.
- Attack cooldowns are `PeriodicAbility` curves + `step(delta)`, not GAS cooldown effects.
- `Player.asc` is public; `Player.setup` takes the `Wallet`; `AbilityContext` carries it.
- The `AbilityBar` lives inside `hud.tscn` and is bound by `Main`; `Stage` does not know the HUD.
- The Blade Sweep tag is `Ability.Sweep` (the GAS registry lowercases segment tails, so camel-case tags cannot round-trip).
- `AbilityInstaller.validate_defs` (each `ability_scene` root must be an `ActorAbility`) is called from `Stage.start` beside `profile.validate()`.
- `VitalsAttributeSet.post_attribute_change` re-clamps health when `max_health` drops below it (no heal on raise).
- `addons/` are vendored copies; `assets/art/icons` are generated placeholders pending the real art library.
- `stage/Stage` (the composition root) references `abilities/`; no other `stage/` file does.
