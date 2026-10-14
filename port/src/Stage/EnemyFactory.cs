using System.Collections.Generic;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Stage;

/// <summary>Creates enemies from defs. The single place to add pooling later.</summary>
public partial class EnemyFactory : Node
{
	private readonly List<Enemy> _alive = [];
	private Node2D _layer = null!;
	private TargetProvider _targets = null!;
	private RunRng _rng = null!;

	[Signal]
	public delegate void SpawnedEventHandler(Enemy enemy);

	/// <summary>Enemies this factory created that are still alive (dying enemies no longer count toward spawn caps).</summary>
	public int AliveCount => _alive.Count;

	public void Bind(Node2D layer, TargetProvider targets, RunRng rng)
	{
		_layer = layer;
		_targets = targets;
		_rng = rng;
	}

	public Enemy Spawn(EnemyDef def, Vector2 position)
	{
		var enemy = def.Scene!.Instantiate<Enemy>();
		_layer.AddChild(enemy);
		enemy.GlobalPosition = position;
		enemy.Setup(def, _targets.TargetFor(def.TargetPolicy), _rng);
		_alive.Add(enemy);
		enemy.Died += Forget;
		enemy.TreeExited += () => Forget(enemy);
		EmitSignal(SignalName.Spawned, enemy);
		return enemy;
	}

	private void Forget(Enemy enemy) => _alive.Remove(enemy);
}
