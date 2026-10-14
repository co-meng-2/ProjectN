using System.Collections.Generic;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class LootDropperTest : TestCase
{
	private static DropTable MakeTable(float chance)
	{
		var table = new DropTable();
		table.Entries.Add(new DropEntry { Kind = CurrencyKind.Gold, Chance = chance, MinAmount = 3, MaxAmount = 3 });
		return table;
	}

	private List<(DropRoll Roll, Vector2 Position)> Kill(DropTable? table, bool anchorFreedBeforeDeath = false)
	{
		var stats = new StatBlock { MaxHealth = 10f };
		ActorEntity killer = ActorFixture.BareActor(this, stats);
		ActorEntity victim = ActorFixture.BareActor(this, stats);
		var anchor = Adopt(new Node2D { Position = new Vector2(40, 60) });
		var health = new Health();
		victim.AddChild(health);
		health.Bind(victim);
		var dropper = new LootDropper();
		victim.AddChild(dropper);
		dropper.Bind(health, table, new RunRng(3), anchor);
		var drops = new List<(DropRoll, Vector2)>();
		dropper.DropRequested += (roll, position) => drops.Add((roll, position));
		if (anchorFreedBeforeDeath)
		{
			anchor.Free();
		}

		DamageEffects.Hit(killer, victim, 999f);
		return drops;
	}

	public void TestDeathRequestsTheRolledDropsAtTheAnchor()
	{
		var drops = Kill(MakeTable(1f));
		Assert.Eq(drops.Count, 1);
		Assert.Eq(drops[0].Roll.Amount, 3);
		Assert.True(drops[0].Position.DistanceTo(new Vector2(40, 60)) <= 12f, "scattered near the anchor");
	}

	public void TestNoTableMeansNoDrops()
	{
		Assert.Eq(Kill(null).Count, 0);
	}

	public void TestFailedChanceMeansNoDrops()
	{
		Assert.Eq(Kill(MakeTable(0f)).Count, 0);
	}

	public void TestAFreedAnchorMeansNoDropsAndNoException()
	{
		Assert.Eq(Kill(MakeTable(1f), anchorFreedBeforeDeath: true).Count, 0);
	}
}
