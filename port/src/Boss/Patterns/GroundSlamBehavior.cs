using System.Collections.Generic;
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;

namespace VampireSurvivor.Boss;

/// <summary>One slam: warn at the player's spot, strike there when the warning ends.</summary>
public sealed class GroundSlamBehavior(GroundSlamDef def) : PatternBehavior
{
	private float _left;
	private Vector2 _point;
	private Telegraph? _telegraph;

	protected override bool Begin()
	{
		Node2D? target = Ctx.Targets.Player;
		if (!GodotObject.IsInstanceValid(target))
		{
			return false;
		}

		_point = target!.GlobalPosition;
		_telegraph = Telegraph.SpawnCircle(Ctx.ProjectileLayer, _point, def.Radius, def.Warning);
		_left = def.Warning;
		return true;
	}

	protected override void Advance(float delta)
	{
		_left -= delta;
		if (_left > 0f)
		{
			return;
		}

		FreeTelegraph();
		if (GodotObject.IsInstanceValid(Ctx.Targets.Player))
		{
			var strike = def.StrikeScene!.Instantiate<AreaStrike>();
			Ctx.ProjectileLayer.AddChild(strike);
			strike.GlobalPosition = _point;
			strike.Strike(Entity, def.Damage, def.Radius, def.StrikeSeconds, PhysicsLayers.AllyHurt, Ctx.Targets.Player);
		}

		Complete(); // without a player there is nothing to hit (a null filter would hit every ally)
	}

	protected override void Abort() => FreeTelegraph();

	private void FreeTelegraph()
	{
		if (GodotObject.IsInstanceValid(_telegraph))
		{
			_telegraph!.QueueFree();
		}

		_telegraph = null;
	}
}
