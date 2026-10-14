using System.Threading.Tasks;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Stage;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Stage;

public class LootDirectorTest : TestCase
{
	private const string EnemyScene = "res://port/src/Actors/Enemy/enemy.tscn";
	private const string PickupScene = "res://port/src/Actors/Pickup/pickup.tscn";

	private sealed record Rig(Player Player, EnemyFactory Factory, Node2D Pickups, Wallet Wallet);

	private Rig MakeRig(float dropMultiplier = 1f)
	{
		var wallet = new Wallet();
		Player player = ActorFixture.Player(this, wallet: wallet);
		TargetProvider targets = Adopt(new TargetProvider { Player = player, Building = player });
		EnemyFactory factory = Adopt(new EnemyFactory());
		factory.Bind(Adopt(new Node2D()), targets, new RunRng(6));
		Node2D pickups = Adopt(new Node2D());
		LootDirector loot = Adopt(new LootDirector());
		loot.Bind(factory, pickups, player, wallet, dropMultiplier);
		return new Rig(player, factory, pickups, wallet);
	}

	private static EnemyDef DroppingDef(int amount)
	{
		EnemyDef def = DefFactory.Enemy(GD.Load<PackedScene>(EnemyScene));
		def.Drops = new DropTable();
		def.Drops.Entries.Add(new DropEntry
		{
			Chance = 1f,
			MinAmount = amount,
			MaxAmount = amount,
			Icon = DefFactory.Texture(8, 8),
		});
		return def;
	}

	private static Pickup DropAt(Node2D layer, Vector2 position, CurrencyKind kind, int amount)
	{
		var pickup = GD.Load<PackedScene>(PickupScene).Instantiate<Pickup>();
		layer.AddChild(pickup);
		pickup.GlobalPosition = position;
		pickup.Setup(new DropRoll { Kind = kind, Amount = amount, Icon = DefFactory.Texture(8, 8) });
		return pickup;
	}

	public async Task TestCollectedLootIsCreditedWithTheGainMultiplier()
	{
		Rig rig = MakeRig();
		ActorEntity entity = rig.Player.Entity;
		entity.Apply(StatEffects.SetBase(Stats.GoldGain, entity.Attributes[Stats.GoldGain].ToRawValue(2f)));
		DropAt(rig.Pickups, rig.Player.GlobalPosition, CurrencyKind.Gold, 5);
		DropAt(rig.Pickups, rig.Player.GlobalPosition, CurrencyKind.Stone, 3);
		await WaitSeconds(0.3);
		Assert.Eq(rig.Wallet.Gold, 10);
		Assert.Eq(rig.Wallet.Stone, 3);
	}

	public async Task TestEnemyDropsBecomePickupsOnThePickupLayer()
	{
		Rig rig = MakeRig();
		Enemy enemy = rig.Factory.Spawn(DroppingDef(1), new Vector2(300f, 300f));
		ActorFixture.Kill(this, enemy.Entity);
		Assert.Eq(rig.Pickups.GetChildCount(), 0, "spawned deferred, not inside the death callback");
		await NextFrame();
		await NextFrame();
		Assert.Eq(rig.Pickups.GetChildCount(), 1);
		var pickup = rig.Pickups.GetChild(0) as Pickup;
		Assert.NotNull(pickup);
		Assert.True(pickup!.GlobalPosition.DistanceTo(new Vector2(300f, 300f)) <= 12f, "dropped where the enemy died");
	}

	public async Task TestDropMultiplierScalesPickupAmounts()
	{
		Rig rig = MakeRig(3f);
		Enemy enemy = rig.Factory.Spawn(DroppingDef(2), new Vector2(300f, 300f));
		ActorFixture.Kill(this, enemy.Entity);
		await NextFrame();
		await NextFrame();
		Assert.Eq(((Pickup)rig.Pickups.GetChild(0)).Amount, 6, "2 x 3.0");
	}

	public async Task TestASmallMultiplierStillPaysAtLeastOneAndHalvesRoundUp()
	{
		Rig small = MakeRig(0.1f);
		ActorFixture.Kill(this, small.Factory.Spawn(DroppingDef(2), new Vector2(300f, 300f)).Entity);
		Rig half = MakeRig(0.5f);
		ActorFixture.Kill(this, half.Factory.Spawn(DroppingDef(5), new Vector2(-300f, 300f)).Entity);
		await NextFrame();
		await NextFrame();
		Assert.Eq(((Pickup)small.Pickups.GetChild(0)).Amount, 1, "0.2 is still worth one coin");
		Assert.Eq(((Pickup)half.Pickups.GetChild(0)).Amount, 3, "2.5 rounds away from zero, as GDScript roundi did");
	}
}
