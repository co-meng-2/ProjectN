using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Stage;

/// <summary>What a <see cref="StageEvent"/> may use. The stage builds one per event; event code never imports the stage root.</summary>
public sealed class EventContext(
	Node2D layer,
	EnemyFactory factory,
	TargetProvider targets,
	Node2D? player,
	Node2D? building,
	RunRng rng,
	Rect2 bounds,
	float spawnRadius,
	EventDef def)
{
	/// <summary>Where the event controller lives; the objects it places (statues, warnings, strikes) are its children.</summary>
	public Node2D Layer { get; } = layer;

	public EnemyFactory Factory { get; } = factory;

	public TargetProvider Targets { get; } = targets;

	public Node2D? Player { get; } = player;

	public Node2D? Building { get; } = building;

	public RunRng Rng { get; } = rng;

	public Rect2 Bounds { get; } = bounds;

	public float SpawnRadius { get; } = spawnRadius;

	public EventDef Def { get; } = def;
}
