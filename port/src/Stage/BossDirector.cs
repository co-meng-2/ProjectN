using System;
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Stage;

/// <summary>
/// Brings the boss in when the stage time is up: switches the flow to Boss, aborts a running event, spawns the boss on the
/// spawn ring around the anchor (the building) and announces it. The boss's death is reported as
/// <see cref="BossDefeated"/>; the stage turns that into Cleared. The boss is a scene from <see cref="BossDef.Scene"/>.
/// </summary>
public partial class BossDirector : Node
{
	public const float EdgeMargin = 24f;

	private BossDef? _def;
	private StageFlow _flow = null!;
	private EventDirector _events = null!;
	private Node2D _layer = null!;
	private Node2D? _anchor;
	private float _spawnRadius;
	private Rect2 _bounds;
	private RunRng _rng = null!;
	private TargetProvider _targets = null!;
	private Node _projectileLayer = null!;
	private Action<EnemyDef, Vector2>? _summon;

	[Signal]
	public delegate void BossSpawnedEventHandler(BossActor boss);

	[Signal]
	public delegate void BossAppearedEventHandler(Health bossHealth, string displayName);

	[Signal]
	public delegate void BossDefeatedEventHandler();

	public BossActor? Boss { get; private set; }

	public bool HasBoss => _def is not null;

	/// <summary>Stage-layer check run by the stage: the boss scene root must be a BossActor.</summary>
	public static List<string> ValidateDef(BossDef? def)
	{
		var errors = new List<string>();
		if (def?.Scene is null)
		{
			return errors; // BossDef.Validate reports a missing scene
		}

		Node root = def.Scene.Instantiate();
		if (root is not BossActor)
		{
			errors.Add($"boss '{def.Id}': scene root is not a BossActor");
		}

		root.Free();
		return errors;
	}

	public void Bind(
		BossDef? def,
		StageFlow flow,
		EventDirector events,
		Node2D layer,
		Node2D anchor,
		float spawnRadius,
		Rect2 bounds,
		RunRng rng,
		TargetProvider targets,
		Node projectileLayer,
		Action<EnemyDef, Vector2>? summon)
	{
		_def = def;
		_flow = flow;
		_events = events;
		_layer = layer;
		_anchor = anchor;
		_spawnRadius = spawnRadius;
		_bounds = bounds;
		_rng = rng;
		_targets = targets;
		_projectileLayer = projectileLayer;
		_summon = summon;
	}

	/// <summary>Spawns the boss once, from Running only. Returns it, or null when there is none or the run is over.</summary>
	public BossActor? Appear()
	{
		if (Boss is not null)
		{
			return Boss;
		}

		if (_def is null || !_flow.TransitionTo(StageState.Boss))
		{
			return null;
		}

		_events.AbortRunning();
		Boss = _def.Scene!.Instantiate<BossActor>();
		_layer.AddChild(Boss);
		Vector2 center = IsInstanceValid(_anchor) ? _anchor!.GlobalPosition : Vector2.Zero;
		Vector2 point = SpawnGeometry.RingPoint(center, _spawnRadius, _rng.Float() * Mathf.Tau);
		Boss.GlobalPosition = SpawnGeometry.ClampToRect(point, _bounds, EdgeMargin);
		Boss.Setup(_def, _targets.Player, _targets, _projectileLayer, _summon);
		Boss.Died += OnBossDied;
		EmitSignal(SignalName.BossSpawned, Boss);
		EmitSignal(SignalName.BossAppeared, Boss.Health, _def.DisplayName);
		return Boss;
	}

	private void OnBossDied(BossActor boss) => EmitSignal(SignalName.BossDefeated);
}
