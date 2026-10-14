using System;
using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Stage;

/// <summary>The gentle background trickle of enemies; its pace ramps across the stage.</summary>
public partial class SpawnDirector : Node
{
	public const float EdgeMargin = 24f;

	private StageDef _stageDef = null!;
	private EnemyFactory _factory = null!;
	private Node2D? _anchor;
	private RunRng _rng = null!;
	private Rect2 _bounds;
	private float _nextSpawnAt;

	public void Bind(StageDef stageDef, StageClock clock, EnemyFactory factory, Node2D anchor, RunRng rng, Rect2 arenaBounds)
	{
		_stageDef = stageDef;
		_factory = factory;
		_anchor = anchor;
		_rng = rng;
		_bounds = arenaBounds;
		clock.Ticked += OnClockTicked;
	}

	/// <summary>Seconds between trickle spawns at <paramref name="elapsed"/>, interpolated across the stage.</summary>
	public float CurrentInterval(float elapsed)
	{
		float progress = Math.Clamp(elapsed / _stageDef.Duration, 0f, 1f);
		return Mathf.Lerp(_stageDef.TrickleIntervalStart, _stageDef.TrickleIntervalEnd, progress);
	}

	private void OnClockTicked(float elapsed)
	{
		if (elapsed < _nextSpawnAt)
		{
			return;
		}

		_nextSpawnAt = elapsed + CurrentInterval(elapsed);
		if (_stageDef.TrickleEnemies.Count == 0 || _factory.AliveCount >= _stageDef.TrickleCap)
		{
			return;
		}

		if (!IsInstanceValid(_anchor))
		{
			return;
		}

		EnemyDef def = _rng.Pick(_stageDef.TrickleEnemies)!;
		Vector2 point = SpawnGeometry.RingPoint(_anchor!.GlobalPosition, _stageDef.SpawnRadius, _rng.Float() * Mathf.Tau);
		_factory.Spawn(def, SpawnGeometry.ClampToRect(point, _bounds, EdgeMargin));
	}
}
