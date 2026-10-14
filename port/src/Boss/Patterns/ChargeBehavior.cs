using System.Collections.Generic;
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;

namespace VampireSurvivor.Boss;

/// <summary>One charge: warning, then the dash with a riding strike.</summary>
public sealed class ChargeBehavior(ChargeDef def) : PatternBehavior
{
	private bool _charging;
	private float _left;
	private Vector2 _direction = Vector2.Right;
	private Telegraph? _telegraph;
	private AreaStrike? _strike;

	protected override bool Begin()
	{
		Node2D? target = Ctx.Targets.Player;
		if (!GodotObject.IsInstanceValid(target))
		{
			return false;
		}

		Vector2 origin = Ctx.Actor.GlobalPosition;
		_direction = (target!.GlobalPosition - origin).Normalized();
		if (_direction == Vector2.Zero)
		{
			_direction = Vector2.Right;
		}

		_telegraph = Telegraph.SpawnLine(Ctx.ProjectileLayer, origin, origin + (_direction * def.Distance), def.LineWidth, def.Warning);
		_charging = false;
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

		if (_charging)
		{
			Complete();
			return;
		}

		if (!GodotObject.IsInstanceValid(Ctx.Targets.Player))
		{
			FreeTelegraph();
			Complete(); // the player is gone: nothing to hit (a null filter would hit every ally)
			return;
		}

		StartCharge();
	}

	protected override void Abort()
	{
		FreeTelegraph();
		if (_charging)
		{
			Ctx.Mover.CancelDash();
		}

		if (GodotObject.IsInstanceValid(_strike))
		{
			_strike!.QueueFree();
		}

		_strike = null;
	}

	private void StartCharge()
	{
		FreeTelegraph();
		_charging = true;
		_left = def.ChargeSeconds();
		Ctx.Mover.Dash(_direction, def.Speed, _left);
		_strike = def.StrikeScene!.Instantiate<AreaStrike>();
		Ctx.Actor.AddChild(_strike); // rides along with the boss
		_strike.Strike(Entity, def.Damage, def.HitRadius, _left, PhysicsLayers.AllyHurt, Ctx.Targets.Player);
	}

	private void FreeTelegraph()
	{
		if (GodotObject.IsInstanceValid(_telegraph))
		{
			_telegraph!.QueueFree();
		}

		_telegraph = null;
	}
}
