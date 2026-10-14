using System.Collections.Generic;
using System.Linq;
using Gamesmiths.Forge.Abilities;
using Gamesmiths.Forge.Effects;
using Gamesmiths.Forge.Effects.Components;
using Gamesmiths.Forge.Effects.Duration;
using Gamesmiths.Forge.Effects.Magnitudes;
using Gamesmiths.Forge.Godot.Nodes;
using Godot;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Components;

/// <summary>
/// The game-facing Forge seam: a ForgeEntity with the game's three attribute sets and a thin API. Game code talks to
/// this class, never to Forge types directly. Ticks in _PhysicsProcess (one game clock); tests turn physics
/// processing off and call <see cref="Tick"/> themselves.
/// </summary>
[GlobalClass]
public partial class ActorEntity : ForgeEntity
{
	/// <summary>Live grants and the cooldown effects they may have started, per ability tag.</summary>
	private readonly Dictionary<string, GrantBook> _books = [];

	public override void _Ready()
	{
		base._Ready();
		SetProcess(false); // ForgeEntity ticks in _Process; the game ticks in _PhysicsProcess.
	}

	public override void _PhysicsProcess(double delta) => Tick(delta);

	public void Tick(double delta)
	{
		EffectsManager.UpdateEffects(delta);
		Abilities.UpdateAbilities(delta);
	}

	/// <summary>Applies base stats. Call after the node is in the tree. MaxHealth first: Health clamps to it.</summary>
	public void ApplyStats(StatBlock stats)
	{
		SetFromDisplay(Stats.MaxHealth, stats.MaxHealth);
		SetFromDisplay(Stats.Health, stats.MaxHealth);
		SetFromDisplay(Stats.MoveSpeed, stats.MoveSpeed);
		SetFromDisplay(Stats.Attack, stats.Attack);
	}

	public void FillHealth()
	{
		Apply(StatEffects.SetBase(Stats.Health, Attributes[Stats.MaxHealth].CurrentValue));
	}

	/// <summary>Applies a prepared effect with this entity as owner and source.</summary>
	public void Apply(EffectData data)
	{
		EffectsManager.ApplyEffect(new Effect(data, new EffectOwnership(this, this)));
	}

	/// <summary>Applies a permanent passive at <paramref name="level"/>; keep the handle to remove it later.</summary>
	public ActiveEffectHandle? ApplyPassive(PassiveDef def, int level)
	{
		return EffectsManager.ApplyEffect(new Effect(PassiveEffects.Build(def), new EffectOwnership(this, this), level));
	}

	public void RemovePassive(ActiveEffectHandle? handle)
	{
		if (handle is { IsValid: true })
		{
			EffectsManager.RemoveEffect(handle);
		}
	}

	/// <summary>
	/// Grants an ability for as long as the returned grant lives. Forge 0.4.0 cannot revoke a permanent grant, so the
	/// grant is carried by an INFINITE effect: removing the effect (see <see cref="Revoke"/>) removes the ability.
	/// </summary>
	public AbilityGrant Grant(ActiveAbilityDef def, int level)
	{
		AbilityData data = AbilityFactory.Build(def);
		var config = new GrantAbilityConfig(
			data,
			new ScalableInt(level),
			AbilityDeactivationPolicy.CancelImmediately,
			AbilityDeactivationPolicy.CancelImmediately);
		var effectData = new EffectData(
			$"Grant:{def.Tag}",
			new DurationData(DurationType.Infinite),
			effectComponents: [new GrantAbilityEffectComponent([config])]);
		ActiveEffectHandle effect = EffectsManager.ApplyEffect(new Effect(effectData, new EffectOwnership(this, this)))!;
		Abilities.TryGetAbility(data, out AbilityHandle? ability);
		if (!_books.TryGetValue(def.Tag, out GrantBook? book))
		{
			_books[def.Tag] = book = new GrantBook();
		}

		book.Live++;
		book.Cooldowns.AddRange(data.CooldownEffects ?? []);
		return new AbilityGrant(effect, ability, data);
	}

	/// <summary>
	/// Ends one grant (running instances are cancelled). The cooldown belongs to the ability tag, not to a grant, so it is
	/// cleared only when the last grant of that tag is revoked; a later re-grant then starts fresh.
	/// </summary>
	public void Revoke(AbilityGrant? grant)
	{
		if (grant is null || !grant.Effect.IsValid)
		{
			return;
		}

		EffectsManager.RemoveEffect(grant.Effect, forceRemoval: true);
		string tag = grant.Data.Name;
		if (!_books.TryGetValue(tag, out GrantBook? book) || --book.Live > 0)
		{
			return;
		}

		_books.Remove(tag);
		foreach (EffectData cooldown in book.Cooldowns)
		{
			EffectsManager.RemoveEffectData(cooldown, forceRemoval: true);
		}
	}

	/// <summary>Ends every running ability and removes every effect. Call before freeing the actor.</summary>
	public void Cleanup()
	{
		Abilities.CancelAbilities(null, null);
		foreach (ActiveEffectHandle handle in EffectsManager.GetActiveEffects().ToArray())
		{
			if (handle.IsValid)
			{
				EffectsManager.RemoveEffect(handle, forceRemoval: true);
			}
		}

		_books.Clear();
	}

	/// <summary>Cleanup after the current physics callback finishes (death happens inside physics callbacks).</summary>
	public void CleanupDeferred() => CallDeferred(MethodName.Cleanup);

	private void SetFromDisplay(string key, float displayValue)
	{
		Apply(StatEffects.SetBase(key, Attributes[key].ToRawValue(displayValue)));
	}

	private sealed class GrantBook
	{
		public int Live { get; set; }

		public List<EffectData> Cooldowns { get; } = [];
	}
}
