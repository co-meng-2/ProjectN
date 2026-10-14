using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;
using VampireSurvivor.Ui;

namespace VampireSurvivor.Tests.Ui;

public class BossBarTest : TestCase
{
	public void TestHiddenUntilTheBossAppearsThenFollowsItsHealth()
	{
		BossBar bar = Adopt(new BossBar());
		Assert.False(bar.Visible);
		ActorEntity boss = ActorFixture.BareActor(this, new StatBlock { MaxHealth = 2500f });
		var health = new Health();
		boss.AddChild(health);
		health.Bind(boss);
		bar.ShowBoss(health, "Orc Warlord");
		Assert.True(bar.Visible);
		Assert.Eq(bar.BossName, "Orc Warlord");
		Assert.AlmostEq(bar.Maximum, 2500.0);
		Assert.AlmostEq(bar.Value, 2500.0);
		DamageEffects.Hit(ActorFixture.BareActor(this, new StatBlock()), boss, 400f);
		Assert.AlmostEq(bar.Value, 2100.0);
	}

	public void TestAFreedBossDoesNotBreakTheBar()
	{
		BossBar bar = Adopt(new BossBar());
		ActorEntity boss = ActorFixture.BareActor(this, new StatBlock { MaxHealth = 100f });
		var health = new Health();
		boss.AddChild(health);
		health.Bind(boss);
		bar.ShowBoss(health, "Gone");
		boss.Free();
		ActorEntity next = ActorFixture.BareActor(this, new StatBlock { MaxHealth = 300f });
		var nextHealth = new Health();
		next.AddChild(nextHealth);
		nextHealth.Bind(next);
		bar.ShowBoss(nextHealth, "Next");
		Assert.AlmostEq(bar.Maximum, 300.0, 0.001, "a second boss after the first was freed");
	}
}
