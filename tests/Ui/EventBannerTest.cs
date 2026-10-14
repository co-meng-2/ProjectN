using Godot;
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;
using VampireSurvivor.Ui;

namespace VampireSurvivor.Tests.Ui;

public class EventBannerTest : TestCase
{
	private EventBanner MakeBanner()
	{
		EventBanner banner = Adopt(new EventBanner());
		banner.SetProcess(false);
		return banner;
	}

	private static EventDef Def(string id) => DefFactory.Event(DefFactory.Packed(new ProbeEvent()), 25f, id);

	private ProbeEvent RunningProbe(EventDef def)
	{
		ProbeEvent probe = Adopt(new ProbeEvent());
		probe.SetPhysicsProcess(false);
		probe.Begin(ActorFixture.ContextFor(this, def));
		return probe;
	}

	public void TestHiddenUntilAnEventStarts()
	{
		Assert.False(MakeBanner().Visible);
	}

	public void TestShowsNameProgressAndRemainingTimeOfTheRunningEvent()
	{
		EventBanner banner = MakeBanner();
		EventDef def = Def("statues");
		ProbeEvent probe = RunningProbe(def);
		banner.ShowEvent(def, probe);
		Assert.True(banner.Visible);
		Assert.Eq(banner.TitleText, "Statues");
		Assert.Eq(banner.TimerText, "25s");
		banner.ShowProgress("Statues 1/4");
		Assert.Eq(banner.ProgressText, "Statues 1/4");
		probe.Step(5.2f);
		banner.Step(0.016f);
		Assert.Eq(banner.TimerText, "20s", "ceil of 19.8");
	}

	public void TestSuccessShowsTheRewardsThenHides()
	{
		EventBanner banner = MakeBanner();
		EventDef def = Def("meteors");
		banner.ShowEvent(def, RunningProbe(def));
		banner.ShowResult(def, true);
		Assert.Eq(banner.TitleText, "Meteors: Success +150 gold +2 stone +1 ticket");
		Assert.Eq(banner.TimerText, string.Empty);
		banner.Step(1f);
		Assert.True(banner.Visible);
		banner.Step(2f);
		Assert.False(banner.Visible, "hidden after the result seconds (2.5 s)");
	}

	public void TestFailureShowsFailedWithoutRewards()
	{
		EventBanner banner = MakeBanner();
		banner.ShowResult(Def("elites"), false);
		Assert.True(banner.Visible);
		Assert.Eq(banner.TitleText, "Elites: Failed");
	}

	public void TestAbortedShowsInterruptedWithoutRewards()
	{
		EventBanner banner = MakeBanner();
		EventDef def = Def("elites");
		banner.ShowEvent(def, RunningProbe(def));
		banner.ShowAborted(def);
		Assert.True(banner.Visible);
		Assert.Eq(banner.TitleText, "Elites: Interrupted");
		Assert.Eq(banner.TimerText, string.Empty);
		banner.Step(3f);
		Assert.False(banner.Visible, "hidden after the result seconds");
	}

	public void TestTheTimerShowsTheLimitBeforeTheEventHasBegun()
	{
		EventBanner banner = MakeBanner();
		EventDef def = Def("statues");
		ProbeEvent probe = Adopt(new ProbeEvent());
		probe.SetPhysicsProcess(false);
		banner.ShowEvent(def, probe);
		Assert.Eq(banner.TimerText, "25s", "not 0s while Begin is still to come");
	}

	public void TestRewardTextOmitsZeroAmountsAndPluralisesTickets()
	{
		Assert.Eq(EventBanner.RewardText(DefFactory.Reward(0, 3, 2)), "+3 stone +2 tickets");
		Assert.Eq(EventBanner.RewardText(DefFactory.Reward(200, 0, 0)), "+200 gold");
	}

	public void TestAFreedEventDoesNotBreakTheBanner()
	{
		EventBanner banner = MakeBanner();
		EventDef def = Def("probe");
		ProbeEvent probe = RunningProbe(def);
		banner.ShowEvent(def, probe);
		probe.Free();
		banner.Step(0.1f);
		Assert.Eq(banner.TimerText, "25s", "keeps the last value");
	}
}
