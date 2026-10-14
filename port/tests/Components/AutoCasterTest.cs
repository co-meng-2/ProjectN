using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class AutoCasterTest : TestCase
{
	private static ProbeAbilityDef MakeProbe(float cooldown = 1f, bool stayActive = false) => new()
	{
		Tag = GameplayTags.AbilityProbe,
		Cooldown = DefFactory.CurveOf(cooldown, cooldown, cooldown, cooldown, cooldown, cooldown),
		StayActive = stayActive,
	};

	private AutoCaster MakeCaster()
	{
		var caster = Adopt(new AutoCaster());
		caster.SetPhysicsProcess(false);
		return caster;
	}

	public void TestStepActivatesAddedAbilitiesAndRespectsTheCooldown()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ProbeAbilityDef probe = MakeProbe();
		AutoCaster caster = MakeCaster();
		caster.Add(actor.Grant(probe, 1));
		caster.Step();
		Assert.Eq(probe.Starts, 1);
		caster.Step();
		Assert.Eq(probe.Starts, 1, "still cooling down");
		actor.Tick(1.1);
		caster.Step();
		Assert.Eq(probe.Starts, 2);
	}

	public void TestAbilitiesThatWereNotAddedAreLeftAlone()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ProbeAbilityDef probe = MakeProbe();
		actor.Grant(probe, 1);
		MakeCaster().Step();
		Assert.Eq(probe.Starts, 0);
	}

	public void TestARunningAbilityIsNotActivatedAgain()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ProbeAbilityDef probe = MakeProbe(0.1f, stayActive: true);
		AutoCaster caster = MakeCaster();
		caster.Add(actor.Grant(probe, 1));
		caster.Step();
		actor.Tick(0.5);
		caster.Step();
		Assert.Eq(probe.Starts, 1);
	}

	public void TestRevokedGrantsAreDroppedWithoutThrowing()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		AutoCaster caster = MakeCaster();
		AbilityGrant grant = actor.Grant(MakeProbe(), 1);
		caster.Add(grant);
		actor.Revoke(grant);
		caster.Step();
		Assert.Eq(caster.Count, 0);
	}

	public void TestDisabledCasterDoesNothingAndAddingTwiceCountsOnce()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ProbeAbilityDef probe = MakeProbe();
		AutoCaster caster = MakeCaster();
		AbilityGrant grant = actor.Grant(probe, 1);
		caster.Add(grant);
		caster.Add(grant);
		Assert.Eq(caster.Count, 1);
		caster.Enabled = false;
		caster.Step();
		Assert.Eq(probe.Starts, 0);
	}
}
