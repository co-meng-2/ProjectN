# Slice 1 — Forge go/no-go

Date: 2026-10-14
Forge: addon `v0.4.0` (forge-godot tag, commit `b72c741`), NuGet `Gamesmiths.Forge` 0.4.0.
Suites: C# 95 tests green; GDScript 648 tests green (unchanged by the port work).

| # | Question | Test(s) | Result | Notes / fallback used |
|---|---|---|---|---|
| a | Lowering MaxHealth pulls Health down; raising never heals | `ActorEntityTest::TestLoweringMaxHealthPullsHealthDown`, `::TestRaisingMaxHealthNeverHeals`, `PassiveTest::TestRemovingVitalityPullsHealthBackToTheBaseMax` | pass | `VitalsAttributes.AttributeOnValueChanged` calling `SetAttributeMaxValue(Health, MaxHealth.CurrentValue)` is enough, including when the change comes from a modifier being removed. |
| b | `DamageExecution` reads the source's Attack in raw units | `DamageTest::TestDamageScalesWithSourceAttack`, `::TestFractionalAttackKeepsTwoDecimalsOfDamage`, `::TestDamageIsTruncatedOnceBelowTheHundredth` | pass | The custom execution captures `Attack` from `AttributeCaptureSource.Source`; no fallback needed. 8.0 x 1.35 = 10.80 -> health 89.20 exactly (integer arithmetic, one truncation). |
| c | Passive apply -> remove -> re-apply at another level; two slots stack | `PassiveTest::TestReapplyAtAnotherLevelUsesTheNewLevel`, `::TestTwoSlotsWithTheSamePassiveStack` | pass | Effects built without `StackingData` apply independently, so two slots holding the same `PassiveDef` add up and unstack separately. |
| d | Actor with active effects/abilities cleaned up and freed safely | `CleanupTest::*` | pass | `Abilities.CancelAbilities(null, null)` plus force-removing every active effect ends running instances (their `OnEnded` runs). Freeing the actor afterwards leaves other entities ticking. |
| e | (new) 0.4.0 has no public revoke: abilities are granted through an INFINITE effect | `AbilityGrantTest::TestRevoke*`, `::TestSameDefGrantedTwiceYieldsTwoIndependentGrants` | pass | `ActorEntity.Grant` applies an INFINITE `GrantAbilityEffectComponent` effect; `Revoke` removes it (running instances are cancelled) and force-removes the ability's cooldown effect so a re-grant starts fresh. |

## Decision
**GO.** All four unverified items from the spec passed without a fallback, and the one thing 0.4.0 lacks (a public revoke) has a clean workaround that fits the "everything is an effect" model. The cooldown scaling by ability level, the typed event trigger and the integer attributes were already shown by the spike. Remaining Forge risk is the one the spec already lists: 300 enemies each carrying a `ForgeEntity` tick has not been measured (slice 2 measures it) and Forge is marked WIP.

## Surprises
- `EntityAttributes.ContainsAttribute` is `internal` in 0.4.0; the public route is `AttributeCaptureDefinition.TryGetAttribute` (wrapped as `Stats.TryGet`).
- `ForgeAbilityBehavior` lives in `Gamesmiths.Forge.Godot.Resources.Abilities`, not `...Resources`.
- Godot's import migrated the csproj to `Godot.NET.Sdk/4.7.2` on its own; the project now pins 4.7.2.
- The `forge-godot` clone used for the spike was not the `v0.4.0` tag; the vendored addon is the tag and compiles against NuGet 0.4.0.
- `ResourceSaver` rewrote `forge_data.tres` without the `uid`/`load_steps` header; Forge loads it by path, so this is harmless.
- `Health` also emits `Damaged` when a lowered `MaxHealth` pulls `Health` down, the same as the GDScript version.
- One Godot segfault on `--import` right after a `dotnet build`; two later imports exit 0 (not reproducible).
