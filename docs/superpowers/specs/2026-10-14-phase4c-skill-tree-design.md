# Phase 4c — Gem skill tree

Date: 2026-10-14. Parent design: `2026-10-13-vampire-survivor-base-design.md` (Phase 4 — Meta). Builds on
`2026-10-14-phase4a-scene-flow-save-design.md` (SceneRouter, SaveService, versioned save) and
`2026-10-14-phase4b-hub-stage-select-design.md` (hub, derived unlock, `StageCatalog` pattern).
Phase 4 order: 4a (done) → 4b (done) → **4c (this spec)**.

## Intent

Gem earned from cleared stages is spent in the hub's Skill Tree on permanent upgrades; the upgrades apply from the next run.
The tree has prerequisites (a node opens once the nodes it requires have been bought), but the UI is a list with lock
markers, not a drawn graph. The bar is the same as 4a/4b: a clean base, content as data, minimal UI.

Constraints carried over: `Stage` and `StatBlock` are not modified; node content, costs and order are data, never code;
`meta/` and `app/` are the only layers that touch autoloads; gem is the only persistent currency; saves never lose the
player's data (unknown ids are kept, bad structure goes to `.bak`).

## Out of scope (4c)

Refund/respec, drawing the tree as a graph, skill icon art, nodes that unlock abilities, effects that differ per level
(a level is a point on one scaling curve, exactly like a passive ability).

## Decisions

- **Stat bonuses are GAS effects, consistent with passives.** A node's `effect` is an INFINITE `GameplayEffect` whose
  modifiers scale with the purchased level through `scaling_curve` and `magnitude = 1.0` (the same authoring and the same
  validation as `AbilityDef.passive_effect`). It is applied to the player's ASC with `ability_level = purchased level`.
- **Slot count is the exception.** `Loadout` reads `PlayerProfile.slot_count`, which is not a GAS attribute, so a node's
  `slot_bonus` (per level) changes the derived profile directly. The result is a deliberate hybrid: stats via GAS,
  slots via the profile.
- **`Stage` stays unchanged.** Bonuses travel on the profile (`PlayerProfile.skill_bonuses`); `Player.setup` applies them.
- **Unlock is derived**, as in 4b: a node is available when every id in `requires` has level >= 1. Only
  `skill_levels` is saved. Order rule: a node may only require nodes listed **before** it, which makes cycles impossible.
- **Purchases are all-or-nothing:** one operation checks availability, level cap and gem, then deducts and levels up.

## Modules

| Module | Layer | Responsibility | Interface |
|---|---|---|---|
| `defs/SkillNodeDef` | defs | One node: `id`, `display_name`, `description`, `requires: Array[StringName]`, `max_level`, `costs: PackedInt32Array` (cost of level `i+1` is `costs[i]`), `effect: GameplayEffect`, `slot_bonus: int` (per level) | `validate()`: empty id/name, `max_level < 1`, `costs.size() != max_level`, any cost <= 0, neither effect nor `slot_bonus > 0`, negative `slot_bonus`, effect not INFINITE, passive-style scaling mistakes (reuses the `AbilityDef` passive checks, extracted into a shared static helper) |
| `defs/SkillTreeDef` + `data/skill_tree/skill_tree.tres` | defs | Ordered `nodes`; order is display order and dependency order | `index_of(id)`, `node_by_id(id)`, `validate()`: empty list, null node, duplicate id, a `requires` id that does not exist or is not listed earlier, each node's own `validate()` |
| `defs/SkillBonus` | defs | One bonus on a profile: `effect: GameplayEffect`, `level: int` | `_init(effect, level)` |
| `defs/PlayerProfile` (modified) | defs | Gains `var skill_bonuses: Array[SkillBonus]` (not exported; set by `ProfileBuilder`) | `validate()` rejects a null effect or `level < 1` |
| `meta/SaveData` (modified) | meta | Gains `skill_levels: Dictionary[StringName, int]` | `level_of(id) -> int` (0 when absent), `set_level(id, n)`, `spend_gem(n) -> bool` (false and no change when short); JSON `{id: level}` |
| `meta/SaveMigrations` (modified) | meta | `CURRENT_VERSION = 3`; `steps()` gains v2 → v3 | sets `skill_levels = {}`, keeps every other field |
| `meta/skill_tree/SkillTreeProgress` | meta | Availability and purchase rules; stores nothing | `SkillTreeProgress.new(tree, save)`, `enum Status { LOCKED, AVAILABLE, MAXED }`, `status(index)`, `next_cost(index) -> int` (-1 when maxed), `can_buy(index) -> bool`, `buy(index) -> bool` |
| `meta/skill_tree/ProfileBuilder` | meta | Pure: derives the run's profile | `static build(base: PlayerProfile, tree: SkillTreeDef, levels: Dictionary[StringName, int]) -> PlayerProfile` |
| `meta/skill_tree/SkillTreeScreen` + `SkillNodeRow` | meta | Gem balance, one row per node, Back; sends requests only | `tree`, `router`, `save_service` injectable (default: shipped tree and the autoloads) |
| `actors/player/Player.setup` (modified) | actors | Applies `skill_bonuses`, fills health to the modified max, then binds health | — |

Status rules: `MAXED` when `level >= max_level`; otherwise `AVAILABLE` when every `requires` id has level >= 1; else `LOCKED`.
`can_buy` = `AVAILABLE` and gem >= `next_cost`. `buy` re-checks `can_buy`, then `spend_gem` and `set_level(level + 1)`.

`ProfileBuilder.build`: duplicate `base` (stats, visual and the abilities array are shared references, not mutated);
for every node with level >= 1: clamp the level to `max_level`, append `SkillBonus(effect, level)` when `effect != null`,
and add `slot_bonus * level` to `slot_count`. Ids in `levels` that are not in the tree are ignored (and stay in the save).
The base profile resource is never modified.

### Shipped content (data)
Four nodes in `data/skill_tree/`: `Vitality` (max health, 3 levels), `Swiftness` (move speed, 3 levels),
`Might` (attack, 3 levels, requires Vitality), `Extra Slot` (`slot_bonus = 1`, 1 level, requires Might). Costs live in the node files.

### Routes and existing code
- `SceneRouter.Route` gains `SKILL_TREE` (appended last) → `res://src/meta/skill_tree/skill_tree_screen.tscn`.
- `Hub`: the reserved Skill Tree button is enabled and goes to `SKILL_TREE`.
- `Main._start_run` builds the run's profile with `ProfileBuilder.build(BASE_PROFILE, SKILL_TREE, save_service.data.skill_levels)`;
  an invalid tree falls back to the base profile (with a `push_error`) instead of crashing the stage.
- `Stage`, `StatBlock`, `AbilityInstaller` are unchanged.

## Data flow

1. Hub Skill Tree → `go(SKILL_TREE)`.
2. The screen validates the tree once, builds a `SkillTreeProgress`, shows the gem balance and one row per node:
   `LOCKED` (name, "Requires: …", Buy disabled), `AVAILABLE` (name, `Lv n/max`, next cost, Buy enabled when affordable),
   `MAXED` (name, `Lv max/max`, "Max").
3. Buy → `progress.buy(index)`; on success `save_service.save()` once and the screen redraws (balance, rows, newly opened children).
   A failed `buy` changes nothing.
4. `Main._start_run` derives the profile from the saved levels and calls `Stage.start`. R-restart rebuilds it from the current save.
5. `Player.setup`: attribute overrides → apply each `SkillBonus` effect at its level → set health to the (modified) max health →
   `health.bind(asc)`. Binding must stay after the stats so the initial values do not count as damage.

## Save format

`{"version": 3, "gem": 30, "cleared_stages": ["stage_01"], "skill_levels": {"vitality": 2}, "settings": {"master_volume": 1.0}}`.
v2 → v3 only adds `skill_levels = {}`.

## Error handling

- v2 save: migrated; gem, clears and volume preserved.
- `skill_levels` that is not an object, a non-integer value (JSON `2.0` is accepted as 2), or a negative level: corruption →
  `.bak` and defaults, exactly as in 4a/4b.
- Level above `max_level` or an id missing from the tree: kept in the save; at profile build the level is clamped and unknown ids ignored.
- A child's saved level whose prerequisites are not met (tree changed later): its effect still applies; prerequisites are checked only when buying.
- Invalid tree: the screen shows a "No skills" label and Back; `Main` starts with the unmodified base profile.
- `buy` when locked, maxed or short on gem returns false and changes nothing; the row handler re-checks so a disabled button cannot buy.

## Testing

Tests mirror `src/` under `tests/`, extending `TestCase`.
- `SkillNodeDef`: each invalid case above is an error; every shipped node validates.
- `SkillTreeDef`: empty, null, duplicate id, unknown requirement, requirement listed later, nested node error; the shipped tree validates with the expected order and requirements.
- `SaveData`: `skill_levels` round trip, `2.0` accepted, negative/string/array/non-object rejected, unknown id preserved, `spend_gem` short leaves gem unchanged.
- `SaveMigrations`/`SaveStore`: v2 file upgrades keeping gem and clears with no `.bak`; malformed `skill_levels` quarantined; the "future version rejected" test holds for the new current version.
- `SkillTreeProgress`: no requirement → available; unmet → locked; met → available; `buy` deducts gem and levels up; short gem, locked and maxed return false with no change; cost follows `costs[level]`; `next_cost` is -1 when maxed.
- `ProfileBuilder`: no purchases equals the base; the base resource is not mutated; one `SkillBonus` per bought effect node; slot bonuses add up; levels clamp to `max_level`; unknown ids ignored.
- `Player` (real scene): a max-health bonus starts the run at full modified health; move-speed and attack bonuses reach the attributes; a profile without bonuses behaves as before.
- `PlayerProfile.validate`: null effect and level < 1 rejected.
- `SkillTreeScreen` (probe router, temp save): row states, successful buy updates balance/level/children and saves, handler calls for locked/short/maxed change nothing, Back goes to the hub, invalid tree shows the empty state.
- `Hub`: the Skill Tree button is enabled and goes to `SKILL_TREE` (replaces the 4b "reserved but disabled" test).
- `Main`: bought levels reach the profile given to `Stage.start`; an invalid tree falls back to the base profile.
- Smoke: clear a stage for gem → buy in the tree → a new run's player starts strengthened, using shipped data.
