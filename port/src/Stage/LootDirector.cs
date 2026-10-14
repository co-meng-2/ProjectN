using System;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Stage;

/// <summary>Turns enemy drops into pickups and collected pickups into wallet credit.</summary>
public partial class LootDirector : Node
{
	public const string PickupScene = "res://port/src/Actors/Pickup/pickup.tscn";

	private Node2D _pickupLayer = null!;
	private Player _player = null!;
	private Wallet _wallet = null!;
	private float _dropMultiplier = 1f;

	public void Bind(EnemyFactory factory, Node2D pickupLayer, Player player, Wallet wallet, float dropMultiplier = 1f)
	{
		_pickupLayer = pickupLayer;
		_player = player;
		_wallet = wallet;
		_dropMultiplier = dropMultiplier;
		factory.Spawned += OnEnemySpawned;
		player.LootCollected += OnLootCollected;
	}

	/// <summary>The player's loot event is a plain C# event; let go of it so a freed director is never called.</summary>
	public override void _ExitTree()
	{
		if (_player is not null && IsInstanceValid(_player))
		{
			_player.LootCollected -= OnLootCollected;
		}
	}

	private static int RoundHalfAway(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

	private void OnEnemySpawned(Enemy enemy)
	{
		enemy.LootDropped += OnLootDropped;
	}

	/// <summary>Deferred: pickups are collision objects and must not be added while physics signals are flushing.</summary>
	private void OnLootDropped(DropRoll roll, Vector2 position)
	{
		Callable.From(() => SpawnPickup(roll, position)).CallDeferred();
	}

	private void SpawnPickup(DropRoll roll, Vector2 position)
	{
		if (!IsInstanceValid(_pickupLayer))
		{
			return;
		}

		// The stage's drop multiplier is applied to the drop itself, so the pickup shows what it pays.
		var scaled = new DropRoll
		{
			Kind = roll.Kind,
			Amount = Math.Max(1, RoundHalfAway(roll.Amount * _dropMultiplier)),
			Icon = roll.Icon,
		};
		var pickup = GD.Load<PackedScene>(PickupScene).Instantiate<Pickup>();
		_pickupLayer.AddChild(pickup);
		pickup.GlobalPosition = position;
		pickup.Setup(scaled);
	}

	private void OnLootCollected(CurrencyKind kind, int amount)
	{
		_wallet.Add(kind, RoundHalfAway(amount * _player.GainMultiplier(kind)));
	}
}
