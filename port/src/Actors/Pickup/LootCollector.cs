using System;
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;

namespace VampireSurvivor.Actors;

/// <summary>
/// Magnet area. Pickups entering it are attracted to the anchor; <see cref="Collected"/> fires when they arrive.
/// Configure in the scene: collision_layer = 0, collision_mask = Pickup, monitorable = false.
/// </summary>
public partial class LootCollector : Area2D
{
	private readonly HashSet<ulong> _subscribed = [];
	private Node2D? _anchor;

	public event Action<CurrencyKind, int>? Collected;

	public void Bind(Node2D anchor)
	{
		_anchor = anchor;
		AreaEntered += OnAreaEntered;
	}

	private void OnAreaEntered(Area2D area)
	{
		if (area is not Pickup pickup)
		{
			return;
		}

		// A pickup can leave and re-enter the magnet (dash), so subscribe only once.
		if (_subscribed.Add(pickup.GetInstanceId()))
		{
			pickup.Reached += OnPickupReached;
		}

		pickup.AttractTo(_anchor);
	}

	private void OnPickupReached(Pickup pickup)
	{
		_subscribed.Remove(pickup.GetInstanceId());
		Collected?.Invoke(pickup.Kind, pickup.Amount);
	}
}
