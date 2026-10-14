# C# Port Slice 5 (Meta + Cutover) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port `src/app` and `src/meta` (save, hub, menus, skill tree, the real stage screen) to C#, then promote the C# tree over the GDScript tree (spec 7.2) so the game boots and runs on C# + Forge only.

**Architecture:** Part A builds `port/src/{App,Meta}` plus the remaining Defs (skill tree, catalog, gem reward, skill bonuses) next to the GDScript tree, with every GDScript meta/app test ported to `port/tests`. The C# `SceneRouter`/`SaveService` are plain `Node` classes that are *not* autoloads until cutover (the GDScript autoloads own those names); screens take them as injectable properties and fall back to `App/Autoloads` lookups. Part B deletes the legacy tree, moves `port/*` to the root, rewrites `res://port/` paths, swaps the autoloads to the C# scripts and re-points the main scene.

**Tech Stack:** Godot 4.7.2 .NET, C# (nullable as errors), Forge 0.4.0, `System.Text.Json` (`JsonNode`) for the save file.

**Spec:** `docs/superpowers/specs/2026-10-14-csharp-forge-port-design.md` (sections 3, 4, 7.2, 7.3 slice 5, 9).

## Global Constraints

- Layer order `Core -> Defs -> Gas -> Components -> Actors -> Stage | Abilities | Boss -> Events -> Ui -> App | Meta`; App and Meta may use each other (`LayerRules`).
- `/root/...` lookups only in `App/Autoloads`.
- Save: JSON at `user://save.json`, schema version 3, migrations one step per version, unreadable files moved to `save.json.bak`, atomic write through `<path>.tmp`.
- `ProfileBuilder.Build(base, tree, levels)` is pure; `SkillBonus(PassiveDef, level)`.
- Unlock state is derived (`StageProgress`), never stored; unknown ids in the save are kept.
- No balance numbers in code; content is `.tres`.
- Every `.cs` gets its `.cs.uid` committed; GDScript suite stays green (648) until Part B deletes it.
- Commits end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. A save file with hostile content (strings for numbers, negative levels, floats like `5.5`, nested garbage, a future version) must load as defaults with the file kept as `.bak`, never crash.
2. A skill node whose passive curve has `max_level` points (not 6) must validate and apply the right value per purchased level.
3. Finishing a run twice (clear + later death in overtime) must credit gem and record the clear exactly once.
4. After cutover no file references `res://port/`, `res://src/...gd`, `GodotGAS`, or `GameplayCueManager`; the project boots from `res://src/App/logo.tscn`.
5. Screens must not crash with an invalid catalog/tree (empty-state label, Back still works).

---

## Part A — port next to the legacy tree

### Task 1: Defs for meta

**Files:** Modify `port/src/Defs/{PassiveDef,PlayerProfile,StageDef,RunResult}.cs`; Create `port/src/Defs/{SkillBonus,SkillNodeDef,SkillTreeDef,StageCatalog}.cs`; Tests `port/tests/Defs/{SkillNodeDefTest,SkillTreeDefTest,StageCatalogTest}.cs` + additions to `PassiveDefTest`, `PlayerProfileTest`, `StageDefTest`.

**Produces:**
- `PassiveDef.Validate(int levelCount = LevelCount)` — points at x = 1..levelCount.
- `sealed record SkillBonus(PassiveDef Passive, int Level)` (plain C#, not a Resource).
- `PlayerProfile.SkillBonuses : List<SkillBonus>` (runtime only, not exported); `Validate` reports null bonus / null passive / level < 1 and nested passive errors (validated at the bonus's own level count = its level is within 1..point count).
- `SkillNodeDef : Resource { StringName Id; string DisplayName; string Description; Array<StringName> Requires; int MaxLevel = 1; int[] Costs; PassiveDef? Passive; int SlotBonus; List<string> Validate() }` — same rules as `skill_node_def.gd`, passive validated with `Validate(MaxLevel)`.
- `SkillTreeDef : Resource { Array<SkillNodeDef> Nodes; int IndexOf(StringName); SkillNodeDef? NodeById(StringName); List<string> Validate() }`.
- `StageCatalog : Resource { Array<StageDef> Stages; int IndexOf(StringName); List<string> Validate() }`.
- `StageDef.GemReward` (int, >= 0 validated) and `int GemFor(RunOutcome)`.
- `RunResult.Gem { get; set; }`.

Tests port `tests/defs/test_skill_node_def.gd` (10), `test_skill_tree_def.gd` (8), `test_stage_catalog.gd` (6), the three `test_player_profile.gd` bonus tests and the stage-def gem tests; plus `PassiveDefTest.TestALevelCountOtherThanSixValidates`.

### Task 2: Player applies skill bonuses

**Files:** Modify `port/src/Actors/Player/Player.cs`; Test `port/tests/Actors/PlayerTest.cs`.

`Setup` applies every `profile.SkillBonuses` through `Entity.ApplyPassive(bonus.Passive, bonus.Level)` after `ApplyStats`, then `Entity.FillHealth()` so a max-health bonus starts at full modified health. Tests port `test_a_max_health_bonus_starts_the_player_at_full_modified_health`, `test_move_speed_and_attack_bonuses_reach_the_attributes`, `test_a_profile_without_bonuses_keeps_its_base_health`.

### Task 3: Save model

**Files:** Create `port/src/Meta/{SettingsData,SaveMigrations,SaveData}.cs`; Tests `port/tests/Meta/{SettingsDataTest,SaveMigrationsTest,SaveDataTest}.cs`.

**Produces:**
- `SettingsData { float MasterVolume (clamped 0..1); JsonObject ToJson(); static SettingsData? FromJson(JsonObject) }`.
- `SaveMigrations { const int CurrentVersion = 3; const double MaxSafeInt = 9e15; static IReadOnlyList<Func<JsonObject, JsonObject>> Steps(); static JsonObject? Migrate(JsonObject); static JsonObject? MigrateWith(JsonObject, IReadOnlyList<Func<JsonObject,JsonObject>>, int current); static long? ReadInt(JsonNode?) }` — `Migrate` deep-copies (`JsonNode.DeepClone`) and returns null where the GDScript returned `{}`.
- `SaveData { long Gem; List<StringName> ClearedStages; Dictionary<StringName,int> SkillLevels; SettingsData Settings; JsonObject ToJson(); static SaveData? FromJson(JsonObject); MarkCleared; LevelOf; SetLevel; bool SpendGem(long) }`.

Tests port `test_settings_data.gd` (6), `test_save_migrations.gd` (13), `test_save_data.gd` (20).

### Task 4: Save store, service, audio settings

**Files:** Create `port/src/Meta/{SaveStore,SaveService}.cs`, `port/src/App/AudioSettings.cs`; Tests `port/tests/Meta/{SaveStoreTest,SaveServiceTest}.cs`, `port/tests/App/AudioSettingsTest.cs`.

**Produces:** `SaveStore.LoadOrDefault(string path) -> SaveData`, `SaveStore.Write(string path, SaveData) -> bool` (tmp + rename, quarantine to `.bak`); `SaveService : Node { const string SavePath = "user://save.json"; string Path; SaveData Data; event Action? Changed; Reload(); AddGem(long); RecordClear(StringName); bool Save() }` (reloads in `_Ready`); `AudioSettings.Apply(SettingsData)`, `const float MinLinear = 0.0001f`.

Tests port `test_save_store.gd` (17), `test_save_service.gd` (7), `test_audio_settings.gd` (3), using temp paths under `user://test_saves/`.

### Task 5: Progress rules and profile builder

**Files:** Create `port/src/Meta/StageProgress.cs`, `port/src/Meta/SkillTree/{SkillTreeProgress,ProfileBuilder}.cs`; Tests `port/tests/Meta/{StageProgressTest,SkillTreeProgressTest,ProfileBuilderTest}.cs`.

`StageProgress(StageCatalog, SaveData)` with `enum StageStatus { Locked, Available, Cleared }` (top-level), `Status(int)`, `IsUnlocked(int)`; `SkillTreeProgress(SkillTreeDef, SaveData)` with `enum SkillStatus { Locked, Available, Maxed }`, `Level`, `Status`, `NextCost`, `CanBuy`, `Buy`; `ProfileBuilder.Build(PlayerProfile base, SkillTreeDef tree, IReadOnlyDictionary<StringName,int> levels) -> PlayerProfile` copying every field (stats, visual, slot count, innate abilities, abilities, bonuses). Tests port 6 + 11 + 7.

### Task 6: Scene router and autoload lookup

**Files:** Create `port/src/App/{SceneRouter,Autoloads}.cs`; Test `port/tests/App/SceneRouterTest.cs`, support `port/tests/Support/SceneRouterProbe.cs`.

`SceneRouter : Node { enum Route { Logo, MainMenu, Settings, Stage, Hub, SkillTree }; static IReadOnlyDictionary<Route,string> Routes; const string ParamStageDef = "stage_def"; string PathFor(Route); bool Go(Route, Dictionary<string, GodotObject>? params = null); Dictionary<string, GodotObject> TakeParams(); protected virtual Error ChangeScene(string path) }`. `Autoloads.Router(Node from)` / `Autoloads.Save(Node from)` read `/root/SceneRouter` and `/root/SaveService` (the only place). Probe overrides `ChangeScene` and records paths, optionally failing. Tests port `test_scene_router.gd` (8; "every route loads" checks each path with `ResourceLoader.Exists` once the screens exist — added in Task 10).

### Task 7: Logo, main menu, settings screen

**Files:** Create `port/src/App/Logo.cs` + `logo.tscn`, `port/src/Meta/Menu/{MainMenu,SettingsScreen}.cs` + `.tscn` (layouts copied from the GDScript scenes, node references as `[Export]` with `node_paths`); Tests `port/tests/App/LogoTest.cs`, `port/tests/Meta/{MainMenuTest,SettingsScreenTest}.cs` (3 + 3 + 3).

### Task 8: Hub

**Files:** Create `port/src/Meta/Hub/{Hub,StageCard}.cs` + `hub.tscn`, `stage_card.tscn`; data `port/data/stages/{stage_02,stage_catalog}.tres`, `stage_01.tres` gets `GemReward = 10`; Test `port/tests/Meta/HubTest.cs` (14).

### Task 9: Skill tree screen and data

**Files:** Create `port/src/Meta/SkillTree/{SkillTreeScreen,SkillNodeRow}.cs` + scenes; data `port/data/skill_tree/{vitality,swiftness,might,extra_slot,skill_tree}.tres` (PassiveDef curves with `max_level` points, values from the GDScript files); Tests `port/tests/Meta/SkillTreeScreenTest.cs` (11), `port/tests/Data/SkillTreeDataTest.cs` (5).

### Task 10: Main (the stage screen) replaces DevStage

**Files:** Create `port/src/App/Main.cs` + `main.tscn`; delete `port/src/App/DevStage.cs` + `dev_stage.tscn` (its tests move to `MainTest`); Modify `Core/InputActions` (`Confirm = "continue_run"`), `Ui/Hud/Hud.ShowResult` (gem + "Enter: continue   R: restart"); Tests `port/tests/App/MainTest.cs` (14 GDScript + the DevStage tests), `port/tests/App/BootFlowTest.cs` (2), SceneRouter "every route loads"; `DataFilesTest` numbers.

## Part B — cutover (spec 7.2)

### Task 11: Promote the C# tree

1. Delete `src/ tests/ data/ godot_gas/ addons/GodotGAS tools/sync_tags.gd*`, the `GameplayCueManager` autoload, `port/tests/Architecture/GlobalClassCollisionTest.cs`.
2. `git mv port/src src`, `port/data data`, `port/tests tests`, `port/tools/* tools/` (case-only clashes go through a temp name).
3. Replace `res://port/` with `res://` in every `.tscn`/`.tres`/`.cs`/`project.godot`; autoloads `SceneRouter="*res://src/App/SceneRouter.cs"`, `SaveService="*res://src/Meta/SaveService.cs"`; `main_scene = "res://src/App/logo.tscn"`.
4. `tools/run_tests.sh`: single C# runner (`res://tests/run_tests.tscn`); `RUNNER` removed.
5. Re-add `[GlobalClass]` to Defs left off for collisions; `SourceTree` scans `res://src`.
6. `IMPORT=1 tools/run_tests.sh` green.

### Task 12: Docs

`CLAUDE.md` rewritten for the C# tree (commands, layers, rules, how-to sections); spec section 7 status, section 9 App/Meta row; status note: port complete; manual play-through listed as the owner's check.
