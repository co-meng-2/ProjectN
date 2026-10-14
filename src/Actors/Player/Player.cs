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
		ApplySkillBonuses(profile);
		Health.Bind(Entity);
		Hurtbox.Bind(Entity, this);
		Mover.Bind(this, Entity);
		Visual.Apply(profile.Visual!);
		Collector.Bind(this);
		var context = new AbilityContext(this, Mover, targets, projectileLayer, wallet);
		context.AnimationRequested += OnAnimationRequested;
		Entity.Context = context;
		foreach (ActiveAbilityDef ability in profile.InnateAbilities)
		{
			Grant(ability, 1);
		}

		Collector.Collected += (kind, amount) => LootCollected?.Invoke(kind, amount);
		Health.Damaged += _ => Visual.FlashHit();
		Health.Died += OnDied;
		AddToGroup(Groups.Player);
	}

	/// <summary>
	/// Skill-tree bonuses are permanent for the run (the handles are never removed). ApplyStats filled health to the
	/// base max and a max-health bonus does not heal, so health is refilled to the modified max afterwards.
	/// </summary>
	private void ApplySkillBonuses(PlayerProfile profile)
	{
		if (profile.SkillBonuses.Count == 0)
		{
			return;
		}

		foreach (SkillBonus bonus in profile.SkillBonuses)
		{
			Entity.ApplyPassive(bonus.Passive, bonus.Level);
		}

		Entity.FillHealth();
	}

	public float GainMultiplier(CurrencyKind kind)
	{
		return Entity.Attributes[kind == CurrencyKind.Gold ? Stats.GoldGain : Stats.StoneGain].DisplayValue;
	}

	/// <summary>Grants an ability; attacks (<see cref="ActiveAbilityDef.AutoFire"/>) are also handed to the auto-caster.</summary>
	public AbilityGrant Grant(ActiveAbilityDef def, int level, string? cooldownTag = null)
	{
		AbilityGrant grant = Entity.Grant(def, level, cooldownTag);
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
