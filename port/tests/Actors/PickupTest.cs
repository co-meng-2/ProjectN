using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Actors;

public class PickupTest : TestCase
{
	private const string ScenePath = "res://port/src/Actors/Pickup/pickup.tscn";

	private static DropRoll MakeRoll(CurrencyKind kind, int amount) => new()
	{
		Kind = kind,
		Amount = amount,
		Icon = DefFactory.Texture(32, 32),
	};

	private Pickup MakePickup(Vector2 position)
	{
		var pickup = Adopt(GD.Load<PackedScene>(ScenePath).Instantiate<Pickup>());
		pickup.GlobalPosition = position;
		pickup.Setup(MakeRoll(CurrencyKind.Stone, 4));
		return pickup;
	}

	private (Node2D Anchor, LootCollector Collector, List<(CurrencyKind, int)> Collected) MakeCollector()
	{
		var anchor = Adopt(new Node2D());
		var collector = new LootCollector
		{
			CollisionLayer = 0,
			CollisionMask = (uint)PhysicsLayers.Pickup,
			Monitorable = false,
		};
		collector.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 60f } });
		anchor.AddChild(collector);
		collector.Bind(anchor);
		var collected = new List<(CurrencyKind, int)>();
		collector.Collected += (kind, amount) => collected.Add((kind, amount));
		return (anchor, collector, collected);
	}

	public void TestSetupCopiesKindAmountAndFitsTheIcon()
	{
		Pickup pickup = MakePickup(Vector2.Zero);
		Assert.Eq(pickup.Kind, CurrencyKind.Stone);
		Assert.Eq(pickup.Amount, 4);
		Assert.AlmostEq(pickup.Sprite.Scale.X, 0.5);
	}

	public void TestPickupWithoutATargetStaysPut()
	{
		Pickup pickup = MakePickup(new Vector2(30, 0));
		pickup.SetPhysicsProcess(false);
		pickup.Step(1.0);
		Assert.Eq(pickup.GlobalPosition, new Vector2(30, 0));
	}

	public void TestAttractedPickupFliesToTheTargetAndReportsOnce()
	{
		var target = Adopt(new Node2D());
		Pickup pickup = MakePickup(new Vector2(100, 0));
		pickup.SetPhysicsProcess(false);
		int reached = 0;
		pickup.Reached += _ => reached++;
		pickup.AttractTo(target);
		for (int i = 0; i < 40; i++)
		{
			pickup.Step(0.05);
		}

		Assert.Eq(reached, 1);
		Assert.True(pickup.IsQueuedForDeletion());
	}

	public async Task TestCollectorAttractsAndReportsCollectedAmounts()
	{
		var (_, _, collected) = MakeCollector();
		MakePickup(new Vector2(30, 0));
		await WaitSeconds(0.6);
		Assert.SequenceEq(collected, new[] { (CurrencyKind.Stone, 4) });
	}

	public async Task TestPickupLeavingAndReenteringTheMagnetIsCollectedOnce()
	{
		var (_, _, collected) = MakeCollector();
		Pickup pickup = MakePickup(new Vector2(50, 0));
		pickup.SetPhysicsProcess(false);
		await Settle();
		pickup.GlobalPosition = new Vector2(400, 0);
		await Settle();
		pickup.GlobalPosition = new Vector2(50, 0);
		pickup.SetPhysicsProcess(true);
		await WaitSeconds(0.6);
		Assert.SequenceEq(collected, new[] { (CurrencyKind.Stone, 4) });
	}
}
