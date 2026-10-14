using System.Collections.Generic;
using System.Linq;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Boss;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Actors;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Boss;

public class BossBrainTest : TestCase
{
	private sealed record Rig(BossBrain Brain, ActorEntity Entity, List<ProbePatternDef> Patterns, List<int> Started);

	/// <summary><paramref name="count"/> probe patterns on a bare actor, gap 2 s, RunRng(9). Started records each started index.</summary>
	private Rig MakeRig(int count, BossOrder order, float[]? weights = null, bool hold = false)
	{
		ActorEntity entity = ActorFixture.BareActor(this, new StatBlock());
		ActorFixture.Context(this, entity, Vector2.Zero);
		var patterns = new List<ProbePatternDef>();
		var grants = new List<AbilityGrant>();
		var typedWeights = new List<float>();
		for (int i = 0; i < count; i++)
		{
			var pattern = new ProbePatternDef { Hold = hold };
			patterns.Add(pattern);
			grants.Add(entity.Grant(pattern, 1));
			typedWeights.Add(weights is not null && i < weights.Length ? weights[i] : 1f);
		}

		BossBrain brain = Adopt(new BossBrain());
		brain.SetPhysicsProcess(false);
		var started = new List<int>();
		brain.PatternStarted += started.Add;
		brain.Bind(entity, grants, typedWeights, order, 2f, new RunRng(9));
		return new Rig(brain, entity, patterns, started);
	}

	private BossActor MakeBoss(PatternDef pattern, float gap)
	{
		Player player = ActorFixture.Player(this);
		BossDef def = DefFactory.Boss(GD.Load<PackedScene>(BossActorTest.BossScene), pattern);
		def.PatternGap = gap;
		return ActorFixture.Boss(this, player, def, new Vector2(-250f, 120f));
	}

	public void TestWaitsOneGapBeforeTheFirstPattern()
	{
		Rig rig = MakeRig(2, BossOrder.Sequence);
		rig.Brain.Step(1.9f);
		Assert.Eq(rig.Started.Count, 0);
		rig.Brain.Step(0.2f);
		Assert.SequenceEq(rig.Started, new[] { 0 });
		Assert.Eq(rig.Patterns[0].Starts, 1, "activated through its grant");
	}

	public void TestSequenceOrderWrapsAround()
	{
		Rig rig = MakeRig(3, BossOrder.Sequence);
		for (int i = 0; i < 5; i++)
		{
			rig.Brain.Step(2f);
		}

		Assert.SequenceEq(rig.Started, new[] { 0, 1, 2, 0, 1 });
	}

	public void TestWaitsForTheRunningPatternBeforeTheNextGap()
	{
		Rig rig = MakeRig(2, BossOrder.Sequence, hold: true);
		rig.Brain.Step(2f);
		Assert.True(rig.Entity.Tags.AllTags.HasTag(GameplayTags.Request(GameplayTags.StateCasting)));
		Assert.NotNull(rig.Brain.Casting);
		rig.Brain.Step(10f);
		Assert.SequenceEq(rig.Started, new[] { 0 }, "nothing new while a pattern runs");
		rig.Patterns[0].Release();
		Assert.Null(rig.Brain.Casting);
		rig.Brain.Step(1.9f);
		Assert.SequenceEq(rig.Started, new[] { 0 }, "the gap starts when the pattern ends");
		rig.Brain.Step(0.2f);
		Assert.SequenceEq(rig.Started, new[] { 0, 1 });
	}

	public void TestRandomOrderNeverRepeatsAndFollowsTheWeights()
	{
		Rig rig = MakeRig(3, BossOrder.Random, [6f, 1f, 1f]);
		for (int i = 0; i < 60; i++)
		{
			rig.Brain.Step(2f);
		}

		Assert.Eq(rig.Started.Count, 60);
		int[] counts = [0, 0, 0];
		for (int i = 0; i < rig.Started.Count; i++)
		{
			counts[rig.Started[i]]++;
			if (i > 0)
			{
				Assert.False(rig.Started[i] == rig.Started[i - 1], $"pick {i} repeats pattern {rig.Started[i]}");
			}
		}

		Assert.True(counts[0] > counts[1] && counts[0] > counts[2], $"weight 6 beats weight 1: {string.Join(",", counts)}");
		Assert.True(counts[1] > 0 && counts[2] > 0, "every pattern gets a turn");
	}

	public void TestRandomWithASinglePatternRepeatsIt()
	{
		Rig rig = MakeRig(1, BossOrder.Random);
		for (int i = 0; i < 3; i++)
		{
			rig.Brain.Step(2f);
		}

		Assert.SequenceEq(rig.Started, new[] { 0, 0, 0 });
	}

	public void TestAPatternThatCannotStartDoesNotStallTheBrain()
	{
		Rig rig = MakeRig(2, BossOrder.Sequence);
		rig.Patterns[0].Refuse = true;
		rig.Brain.Step(2f);
		Assert.Null(rig.Brain.Casting, "the refusal ended the attempt");
		rig.Brain.Step(1f);
		Assert.SequenceEq(rig.Started, new[] { 0 }, "a full gap follows a refusal");
		rig.Brain.Step(1f);
		Assert.SequenceEq(rig.Started, new[] { 0, 1 });
		Assert.Eq(rig.Patterns[1].Starts, 1);
	}

	public void TestStopHaltsTheBrain()
	{
		Rig rig = MakeRig(2, BossOrder.Sequence);
		rig.Brain.Step(2f);
		rig.Brain.Stop();
		for (int i = 0; i < 5; i++)
		{
			rig.Brain.Step(2f);
		}

		Assert.SequenceEq(rig.Started, new[] { 0 });
	}

	public void TestAttachToBindsABossAndRunsItsPatterns()
	{
		var pattern = new ProbePatternDef();
		BossActor boss = MakeBoss(pattern, 1.5f);
		BossBrain brain = BossBrain.AttachTo(boss, new RunRng(1));
		brain.SetPhysicsProcess(false);
		Assert.True(brain.GetParent() == boss);
		Assert.Eq(brain.Name.ToString(), "Brain");
		brain.Step(1.4f);
		Assert.Eq(pattern.Starts, 0, "the def's gap, not a default");
		brain.Step(0.2f);
		Assert.Eq(pattern.Starts, 1);
	}

	public void TestValidateDefRequiresPatternsWithRegisteredDistinctTags()
	{
		Assert.NoErrors(BossBrain.ValidateDef(DefFactory.Boss(null, new ProbePatternDef())), "one probe");
		var errors = BossBrain.ValidateDef(DefFactory.Boss(null, new ProbePatternDef(), new ProbePatternDef()));
		Assert.Eq(errors.Count, 1, "two patterns share ability.probe");
		Assert.True(errors[0].Contains("ability.probe"));
		Assert.Eq(BossBrain.ValidateDef(DefFactory.Boss(null, new ProbeAbilityDef { Tag = GameplayTags.AbilityProbe })).Count, 1, "not a boss pattern");
		Assert.Eq(BossBrain.ValidateDef(DefFactory.Boss(null, new ProbePatternDef { Tag = "ability.nonexistent" })).Count, 1, "an unregistered tag");
		Assert.NoErrors(BossBrain.ValidateDef(null), "no boss");
	}

	public void TestTheBossDyingMidGapStopsTheBrain()
	{
		var pattern = new ProbePatternDef();
		BossActor boss = MakeBoss(pattern, 1.5f);
		BossBrain brain = BossBrain.AttachTo(boss, new RunRng(1));
		brain.SetPhysicsProcess(false);
		brain.Step(1f);
		ActorFixture.Kill(this, boss.Entity);
		brain.Step(5f);
		Assert.Eq(pattern.Starts, 0, "no pattern activates after the boss died");
	}
}
