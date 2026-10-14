# Phase 4b — Hub, stage select, sequential unlock

Date: 2026-10-14. Parent design: `2026-10-13-vampire-survivor-base-design.md` (Phase 4 — Meta). Builds on
`2026-10-14-phase4a-scene-flow-save-design.md` (SceneRouter, SaveService, versioned SaveData/SaveMigrations).
Phase 4 order: 4a (done) → **4b (this spec)** → 4c (gem skill tree).

## Intent

After 4b the flow is main menu → hub → stage → result → hub. The hub lists the stages in order; a stage unlocks when the
previous one has been cleared. Clears persist in the save. 4b is also the first real schema change, so it exercises
4a's migration path (v1 → v2). The same bar as 4a applies: a clean base, UI kept minimal.

Constraints carried over: `Stage` is not modified; stage order and content are data, not code; `meta/` and `app/` are
the only layers that touch autoloads; gem is the only persistent currency; saves never lose the player's data.

## Out of scope (4b)

Skill tree (4c — the hub only reserves a disabled button), stage thumbnails/descriptions, first-clear-only rewards
(re-clearing keeps paying gem, as in 4a), non-sequential unlock graphs, hub background art or transitions.

## Decisions

- **Unlock is derived, not stored.** Only `cleared_stages` is saved. Stage `i` is unlocked when `i == 0` or stage `i-1`
  is in `cleared_stages`. One source of truth: reordering or extending the catalog cannot desync a stored unlock list.
- **The hub is the stage select.** One screen; no separate stage-select route. 4c adds the Skill Tree button's behaviour.
- **A second stage ships as data only** (`stage_02.tres`, a duplicate of `stage_01` with its own id, name, duration,
  difficulty and `gem_reward`) so the unlock flow is observable in the real game without code.

## Modules

| Module | Layer | Responsibility | Interface |
|---|---|---|---|
| `defs/StageCatalog` + `data/stages/stage_catalog.tres` | defs | Ordered list of `StageDef`; order is unlock order | `stages: Array[StageDef]`, `index_of(id) -> int` (-1 when absent), `validate() -> PackedStringArray` (empty list, null entry, duplicate id, each stage's own `validate()`) |
| `data/stages/stage_02.tres` | data | Second stage, duplicate of `stage_01` with its own numbers | — |
| `meta/SaveData` (modified) | meta | Adds `cleared_stages: Array[StringName]` | `mark_cleared(id)` (no duplicates); `to_dict` stores strings; `from_dict` rejects a non-array or any non-string entry (corruption) |
| `meta/SaveMigrations` (modified) | meta | `CURRENT_VERSION = 2`; `steps()` gains the v1 → v2 step | v1 → v2 sets `cleared_stages = []` and keeps every other field |
| `meta/SaveService` (modified) | meta | Records clears | `record_clear(stage_id)` → `data.mark_cleared`, emits `changed` |
| `meta/StageProgress` | meta | Pure unlock calculation; stores nothing | `StageProgress.new(catalog, save_data)`, `enum Status { LOCKED, AVAILABLE, CLEARED }`, `status(index) -> Status`, `is_unlocked(index) -> bool` |
| `meta/hub/Hub` + `StageCard` | meta | One card per catalog stage plus Back and a disabled Skill Tree button; sends requests, owns no state | `catalog`, `router`, `save_service` injectable (default: the shipped catalog and the autoloads) |

Status rules: `CLEARED` if the stage id is in `cleared_stages`; otherwise `AVAILABLE` if it is unlocked; otherwise
`LOCKED`. A cleared stage is still playable. Ids in the save that are not in the catalog are kept and ignored.

### Routes
`SceneRouter.Route` gains `HUB` → `res://src/meta/hub/hub.tscn`. The existing "every route loads" test covers it.

### Changes to existing code
- `MainMenu`: Start goes to `HUB` (no params) and no longer preloads `stage_01`.
- `Main`: on `CLEARED` it calls `save_service.record_clear(stage_def.id)` next to the gem credit, before the single
  `save()`; a failed run records nothing. Enter on the result screen goes to `HUB` instead of `MAIN_MENU`. The existing
  `_result != null` guard also prevents a double record.
- `Stage` is unchanged. R-restart stays as a dev shortcut.

## Data flow

1. Main menu Start → `go(HUB)`.
2. `Hub._ready` validates the catalog once, builds a `StageProgress` from `save_service.data`, and builds one card per
   stage: `LOCKED` shows the name and "Locked" with Play disabled; `AVAILABLE` shows the name with Play; `CLEARED` shows
   the name and "Cleared" with Play still enabled.
3. Play → `go(STAGE, {PARAM_STAGE_DEF: stage})`. The handler re-checks the stage is unlocked, so a locked stage cannot
   start even if its button is somehow pressed.
4. `Main._on_finished` pays gem as in 4a; on `CLEARED` it also records the clear; one `save()`.
5. Enter on the result screen → `go(HUB)`; the hub rebuilds from the saved state, so the next stage is already open.
   Back → `MAIN_MENU`.

## Save format

`{"version": 2, "gem": 0, "cleared_stages": ["stage_01"], "settings": {"master_volume": 1.0}}`.
The v1 → v2 step is the only migration code added; it keeps all v1 fields.

## Error handling

- v1 save: migrated to v2 with `cleared_stages = []`; gem and volume preserved.
- `cleared_stages` that is not an array, or contains a non-string: treated as corruption (moved to `save.json.bak`, defaults), exactly as in 4a.
- Unknown id in `cleared_stages`: preserved and ignored.
- Empty or invalid catalog: the hub `push_error`s, shows a "No stages" label and the Back button, and does not crash.
- Duplicate clears: `mark_cleared` ignores an id that is already present.

## Testing

Tests mirror `src/` under `tests/`, extending `TestCase`.
- `StageCatalog`: empty list, null entry, duplicate id and a broken nested `StageDef` each give an error; the shipped catalog validates and is `stage_01`, `stage_02` in that order.
- `SaveData`: `cleared_stages` round trip; `mark_cleared` twice stores one id; non-array and non-string entries are rejected.
- `SaveMigrations`: v1 → v2 keeps gem and volume and adds an empty list; v2 is unchanged; `steps().size() == CURRENT_VERSION - 1`; the future-version rejection test still holds with the new current version.
- `SaveStore`: a hand-written v1 JSON file loads migrated, with no `.bak`.
- `SaveService`: `record_clear` emits `changed` and survives save + reload.
- `StageProgress`: first stage always unlocked; second locked until the first is cleared, then available; cleared stage reports `CLEARED`; ids not in the catalog are ignored.
- `Hub` (probe router, temp save): button enabled state per status; Play on an unlocked stage routes to `STAGE` with the right `StageDef`; the handler refuses a locked stage; Back routes to `MAIN_MENU`; an empty catalog shows the empty state.
- `MainMenu`: Start routes to `HUB`.
- `Main`: `CLEARED` records the clear and pays gem once; `FAILED` records nothing; a double `finished` records once; Enter routes to `HUB`.
- Smoke: menu → hub → `stage_01` cleared → hub shows `stage_02` available, using the shipped data.
