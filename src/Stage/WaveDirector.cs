using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Stage;

/// <summary>Fires the stage's scheduled surges and exposes their times for the timeline bar.</summary>
public partial class WaveDirector : Node
{
	public const float EdgeMargin = 24f;

	private List<WaveEntry> _entries = [];

	// Starts below 0 so a wave at time 0 fires on the first tick (marks are crossed when from < mark <= to).
	private float _previousTime = -1f;
	private EnemyFactory _factory = null!;
	private Node2D? _anchor;
	private RunRng _rng = null!;
	private Rect2 _bounds;
	private float _spawnRadius;

	[Signal]
	public delegate void WaveStartedEventHandler(WaveEntry entry);

	public Timeline Timeline { get; private set; } = new(0f);

	public void Bind(StageDef stageDef, StageClock clock, EnemyFactory factory, Node2D anchor, RunRng rng, Rect2 arenaBounds)
	{
		_factory = factory;
		_anchor = anchor;
		_rng = rng;
		_bounds = arenaBounds;
		_spawnRadius = stageDef.SpawnRadius;
		float[] times = [];
		if (stageDef.Waves is not null)
		{
			_entries = stageDef.Waves.SortedEntries();
			times = stageDef.Waves.Times();
		}

		Timeline = new Timeline(stageDef.Duration, times);
		clock.Ticked += OnClockTicked;
	}

	public float[] MarkTimes() => Timeline.Marks;

	private void OnClockTicked(float elapsed)
	{
		foreach (int index in Timeline.Crossed(_previousTime, elapsed))
		{
			Launch(_entries[index]);
		}

		_previousTime = elapsed;
	}

	private void Launch(WaveEntry entry)
	{
		EmitSignal(SignalName.WaveStarted, entry);
		if (!IsInstanceValid(_anchor))
		{
			return;
		}

		float baseAngle = _rng.Float() * Mathf.Tau;
		for (int i = 0; i < entry.Count; i++)
		{
			float angle = baseAngle + (Mathf.Tau * i / entry.Count);
			Vector2 point = SpawnGeometry.RingPoint(_anchor!.GlobalPosition, _spawnRadius, angle);
			_factory.Spawn(entry.Enemy!, SpawnGeometry.ClampToRect(point, _bounds, EdgeMargin));
		}
	}
}
