using System.Collections.Generic;
using System.Linq;
using Gamesmiths.Forge.Core;
using Gamesmiths.Forge.Abilities;
using Gamesmiths.Forge.Effects;
using Gamesmiths.Forge.Effects.Components;
using Gamesmiths.Forge.Effects.Duration;
using Gamesmiths.Forge.Effects.Magnitudes;
using Gamesmiths.Forge.Events;
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
	/// <summary>Live grants and the cooldown effects they may have started, per cooldown tag.</summary>
	private readonly Dictionary<string, GrantBook> _books = [];

	public override void _Ready()
	{
		base._Ready();
		SetProcess(false); // ForgeEntity ticks in _Process; the game ticks in _PhysicsProcess.
	}

	/// <summary>What this actor offers its abilities; set by the actor's Setup.</summary>
	public AbilityContext? Context { get; set; }

	public override void _PhysicsProcess(double delta) => Tick(delta);

	public void Tick(double delta)
	{
		EffectsManager.UpdateEffects(delta);
		Abilities.UpdateAbilities(delta);
	}

	/// <summary>Activates the granted abilities carrying <paramref name="abilityTag"/>. True when at least one started.</summary>
	public bool TryActivate(string abilityTag, IForgeEntity? target = null)
	{
		return Abilities.TryActivateAbilitiesByTag(GameplayTags.ContainerOf(abilityTag), target, out _);
	}

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
	/// <param name="cooldownTag">A slot's own cooldown tag (GameplayTags.SlotCooldowns); null uses "cooldown." + the tag tail.</param>
	public AbilityGrant Grant(ActiveAbilityDef def, int level, string? cooldownTag = null)
	{
		string effectiveCooldown = cooldownTag ?? (def.Cooldown is null ? def.Tag : GameplayTags.CooldownFor(def.Tag));
		AbilityData data = AbilityFactory.Build(def, effectiveCooldown);
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
		if (!_books.TryGetValue(effectiveCooldown, out GrantBook? book))
		{
			_books[effectiveCooldown] = book = new GrantBook();
		}

		book.Live++;
		book.Cooldowns.AddRange(data.CooldownEffects ?? []);
		return new AbilityGrant(effect, ability, data, effectiveCooldown);
	}

	/// <summary>
	/// Ends one grant (running instances are cancelled). A cooldown belongs to its cooldown tag, not to a grant, so it is
	/// cleared only when the last grant using that tag is revoked; a later re-grant then starts fresh. Slot grants have a
	/// tag of their own, so revoking a slot always clears its cooldown.
	/// </summary>
	public void Revoke(AbilityGrant? grant)
	{
		if (grant is null || !grant.Effect.IsValid)
		{
			return;
		}

		EffectsManager.RemoveEffect(grant.Effect, forceRemoval: true);
		string tag = grant.CooldownTag;
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
