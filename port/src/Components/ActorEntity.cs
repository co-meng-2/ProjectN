using Gamesmiths.Forge.Effects;
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

	private void SetFromDisplay(string key, float displayValue)
	{
		Apply(StatEffects.SetBase(key, Attributes[key].ToRawValue(displayValue)));
	}
}
