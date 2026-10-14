using System.Collections.Generic;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Stage;

namespace VampireSurvivor.Events;

/// <summary>
/// "Elite hunt": <see cref="Count"/> elite enemies arrive on the spawn ring around the building through the enemy factory,
/// so kills, event.kill and drops work as for any enemy. All dead in time -> success; time up -> failure, and the
/// survivors stay in the fight (also on abort: once spawned they are ordinary enemies).
/// </summary>
public partial class EliteEvent : StageEvent
{
	private readonly List<Enemy> _alive = [];
	private int _killed;

	[Export]
	public EnemyDef? EliteEnemy { get; set; }

	[Export]
	public int Count { get; set; }

	/// <summary>Keeps the spawn points off the arena walls.</summary>
	[Export]
	public float EdgeMargin { get; set; }

	public override List<string> Validate()
	{
		var errors = new List<string>();
		if (EliteEnemy is null)
		{
			errors.Add("EliteEvent: elite_enemy is null");
		}
		else
		{
			errors.AddRange(EliteEnemy.Validate());
		}

		if (Count < 1)
		{
			errors.Add("EliteEvent: count must be >= 1");
		}

		if (EdgeMargin < 0f)
		{
			errors.Add("EliteEvent: edge_margin must be >= 0");
		}

		return errors;
	}

	public override List<Node2D> Objectives() => [.. _alive.FindAll(IsInstanceValid)];

	protected override void OnBegin()
	{
		Vector2 center = IsInstanceValid(Context!.Building) ? Context.Building!.GlobalPosition : Vector2.Zero;
		float baseAngle = Context.Rng.Float() * Mathf.Tau;
		for (int i = 0; i < Count; i++)
		{
			float angle = baseAngle + (Mathf.Tau * i / Count);
			Vector2 point = SpawnGeometry.RingPoint(center, Context.SpawnRadius, angle);
			Enemy elite = Context.Factory.Spawn(EliteEnemy!, SpawnGeometry.ClampToRect(point, Context.Bounds, EdgeMargin));
			elite.Died += OnEliteDied;
			_alive.Add(elite);
		}

		Report();
	}

	/// <summary>The survivors are not removed: they keep fighting as ordinary enemies.</summary>
	protected override void Cleanup()
	{
		foreach (Enemy elite in _alive)
		{
			if (IsInstanceValid(elite))
			{
				elite.Died -= OnEliteDied;
			}
		}

		_alive.Clear();
	}

	private void OnEliteDied(Enemy elite)
	{
		if (!IsRunning)
		{
			return;
		}

		_alive.Remove(elite);
		_killed++;
		Report();
		if (_killed >= Count)
		{
			Succeed();
		}
	}

	private void Report() => ReportProgress($"Elites {_killed}/{Count}");
}
