using System;
using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Components;

/// <summary>Rolls an actor's <see cref="DropTable"/> when it dies and asks the world to spawn the pickups.</summary>
public partial class LootDropper : Node
{
	public const float ScatterRadius = 10f;

	private DropTable? _table;
	private RunRng? _rng;
	private Node2D? _anchor;

	public event Action<DropRoll, Vector2>? DropRequested;

	public void Bind(Health health, DropTable? table, RunRng rng, Node2D anchor)
	{
		_table = table;
		_rng = rng;
		_anchor = anchor;
		health.Died += OnDied;
	}

	private void OnDied()
	{
		if (_table is null || _rng is null || !IsInstanceValid(_anchor))
		{
			return;
		}

		foreach (DropRoll roll in _table.Roll(_rng))
		{
			DropRequested?.Invoke(roll, _anchor!.GlobalPosition + _rng.OffsetInCircle(ScatterRadius));
		}
	}
}
