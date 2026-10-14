using System.Linq;
using Gamesmiths.Forge.Abilities;
using VampireSurvivor.Abilities;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Abilities;

public class AbilityInstallerTest : TestCase
{
	private Player _player = null!;
	private Loadout _loadout = null!;
	private AbilityInstaller _installer = null!;

	private void Rig(int slotCount = 3, bool bindNow = true)
	{
		_player = ActorFixture.Player(this);
		_player.Entity.SetPhysicsProcess(false);
		_player.AutoCaster.SetPhysicsProcess(false);
		_loadout = new Loadout(slotCount);
		_installer = new AbilityInstaller();
		if (bindNow)
		{
			_installer.Bind(_loadout, _player);
		}
	}

	private double Attr(string key) => _player.Entity.Attributes[key].DisplayValue;

	private static AbilityDef Swift() => DefFactory.Ability("swift", new PassiveDef
	{
		Stat = StatId.MoveSpeed,
		Operation = PassiveOperation.Percent,
		ByLevel = DefFactory.CurveOf(0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f),
	});

	private static ProbeAbilityDef Probe(bool stayActive = false) => new()
	{
		Tag = GameplayTags.AbilityProbe,
		Cooldown = DefFactory.CurveOf(1f, 1f, 1f, 1f, 1f, 1f),
		StayActive = stayActive,
	};

	private static AbilityDef Attack(ProbeAbilityDef probe) => DefFactory.Ability("probe", active: probe);

	private static AbilityDef Hybrid() => DefFactory.Ability("hybrid", DefFactory.Vitality(), Probe());

	private static bool Fire(AbilityGrant? grant) => grant!.Ability!.Activate(out AbilityActivationFailures _);

	private int ActiveEffects() => _player.Entity.EffectsManager.GetActiveEffects().Count();

	public void TestSettingASlotAppliesThePassiveAtTheGradeLevel()
	{
		Rig();
		_loadout.SetSlot(0, DefFactory.VitalityAbility(), Grade.Common);
		Assert.AlmostEq(Attr(Stats.MaxHealth), 120.0);
		Assert.NotNull(_installer.InstalledEffect(0));
		_loadout.SetSlot(0, DefFactory.VitalityAbility(), Grade.Rare);
		Assert.AlmostEq(Attr(Stats.MaxHealth), 140.0, 0.001, "replaced, not stacked");
		_loadout.SetSlot(0, DefFactory.VitalityAbility(), Grade.Revealed);
		Assert.AlmostEq(Attr(Stats.MaxHealth), 340.0);
	}

	public void TestBindingInstallsSlotsThatWereFilledBefore()
	{
		Rig(3, bindNow: false);
		_loadout.SetSlot(1, DefFactory.VitalityAbility(), Grade.Epic);
		Assert.AlmostEq(Attr(Stats.MaxHealth), 100.0, 0.001, "nothing until bound");
		_installer.Bind(_loadout, _player);
		Assert.AlmostEq(Attr(Stats.MaxHealth), 170.0);
	}

	public void TestChangingTheAbilityRemovesTheOldPassive()
	{
		Rig();
		_loadout.SetSlot(0, DefFactory.VitalityAbility(), Grade.Common);
		_loadout.SetSlot(0, Swift(), Grade.Common);
		Assert.AlmostEq(Attr(Stats.MaxHealth), 100.0);
		Assert.AlmostEq(Attr(Stats.MoveSpeed), 110.0);
	}

	public void TestTwoSlotsWithTheSamePassiveStack()
	{
		Rig();
		_loadout.SetSlot(0, DefFactory.VitalityAbility(), Grade.Common);
		_loadout.SetSlot(1, DefFactory.VitalityAbility(), Grade.Rare);
		Assert.AlmostEq(Attr(Stats.MaxHealth), 160.0, 0.001, "20 + 40");
		_loadout.SetSlot(1, null, Grade.None);
		Assert.AlmostEq(Attr(Stats.MaxHealth), 120.0);
	}

	public void TestAttacksAreGrantedAtTheGradeLevelAndReplacedByAFreshGrant()
	{
		Rig();
		ProbeAbilityDef probe = Probe();
		_loadout.SetSlot(0, Attack(probe), Grade.Common);
		AbilityGrant? first = _installer.InstalledGrant(0);
		Assert.True(Fire(first));
		Assert.Eq(probe.LastLevel, 1);
		_loadout.SetSlot(0, Attack(probe), Grade.Rare);
		AbilityGrant? second = _installer.InstalledGrant(0);
		Assert.True(second != first, "a fresh grant");
		Assert.False(first!.Effect.IsValid, "the old grant was revoked");
		Assert.True(Fire(second), "a promoted attack fires at once: fresh cooldown");
		Assert.Eq(probe.LastLevel, 2);
	}

	public void TestClearingASlotUninstallsEverything()
	{
		Rig();
		_loadout.SetSlot(0, Hybrid(), Grade.Epic);
		Assert.AlmostEq(Attr(Stats.MaxHealth), 170.0);
		AbilityGrant? grant = _installer.InstalledGrant(0);
		Assert.NotNull(grant);
		_loadout.SetSlot(0, null, Grade.None);
		Assert.AlmostEq(Attr(Stats.MaxHealth), 100.0);
		Assert.Null(_installer.InstalledGrant(0));
		Assert.Null(_installer.InstalledEffect(0));
		Assert.False(grant!.Effect.IsValid);
	}

	public void TestManyChangesLeaveNoLeftovers()
	{
		Rig();
		int baseline = ActiveEffects();
		var rng = new RunRng(9);
		AbilityDef[] defs = [DefFactory.VitalityAbility(), Swift(), Hybrid(), Attack(Probe())];
		for (int i = 0; i < 40; i++)
		{
			_loadout.SetSlot(rng.Int(0, 2), rng.Pick(defs), (Grade)rng.Int(1, 6));
		}

		for (int i = 0; i < 3; i++)
		{
			_loadout.SetSlot(i, null, Grade.None);
		}

		Assert.AlmostEq(Attr(Stats.MaxHealth), 100.0);
		Assert.AlmostEq(Attr(Stats.MoveSpeed), 100.0);
		Assert.Eq(ActiveEffects(), baseline, "every passive and grant effect was removed");
	}

	public void TestChangingASlotMidActivationStripsItsTags()
	{
		Rig();
		ProbeAbilityDef probe = Probe(stayActive: true);
		probe.OwnedTags = [GameplayTags.StateInvulnerable];
		_loadout.SetSlot(0, Attack(probe), Grade.Common);
		Assert.True(Fire(_installer.InstalledGrant(0)));
		Assert.True(_player.Entity.Tags.AllTags.HasTag(GameplayTags.Request(GameplayTags.StateInvulnerable)));
		_loadout.SetSlot(0, DefFactory.VitalityAbility(), Grade.Common);
		Assert.False(_player.Entity.Tags.AllTags.HasTag(GameplayTags.Request(GameplayTags.StateInvulnerable)));
		Assert.AlmostEq(Attr(Stats.MaxHealth), 120.0);
	}

	public void TestRebindingDoesNotDoubleInstall()
	{
		Rig();
		_loadout.SetSlot(0, DefFactory.VitalityAbility(), Grade.Common);
		_installer.Bind(_loadout, _player);
		Assert.AlmostEq(Attr(Stats.MaxHealth), 120.0, 0.001, "reinstalled once, not stacked");
		_loadout.SetSlot(0, DefFactory.VitalityAbility(), Grade.Rare);
		Assert.AlmostEq(Attr(Stats.MaxHealth), 140.0, 0.001, "one listener");
	}

	public void TestUninstallSurvivesACleanedUpEntity()
	{
		Rig();
		_loadout.SetSlot(0, Hybrid(), Grade.Common);
		_player.Entity.Cleanup();
		_loadout.SetSlot(0, null, Grade.None);
		Assert.Null(_installer.InstalledEffect(0));
		Assert.Null(_installer.InstalledGrant(0));
	}

	public void TestClearingVitalityReclampsHealthToTheLoweredMax()
	{
		Rig();
		_loadout.SetSlot(0, DefFactory.VitalityAbility(), Grade.Common);
		_player.Entity.FillHealth();
		Assert.AlmostEq(Attr(Stats.Health), 120.0, 0.001, "precondition: health at the raised max");
		_loadout.SetSlot(0, null, Grade.None);
		Assert.AlmostEq(Attr(Stats.MaxHealth), 100.0);
		Assert.AlmostEq(Attr(Stats.Health), 100.0, 0.001, "clamped down with the max");
	}

	public void TestRaisingMaxHealthDoesNotHeal()
	{
		Rig();
		_loadout.SetSlot(0, DefFactory.VitalityAbility(), Grade.Common);
		Assert.AlmostEq(Attr(Stats.MaxHealth), 120.0);
		Assert.AlmostEq(Attr(Stats.Health), 100.0);
	}

	public void TestPromotingVitalityKeepsHealthAtTheFullMax()
	{
		Rig();
		_loadout.SetSlot(0, DefFactory.VitalityAbility(), Grade.Rare);
		_player.Entity.FillHealth();
		_loadout.SetSlot(0, DefFactory.VitalityAbility(), Grade.Epic);
		Assert.AlmostEq(Attr(Stats.Health), 140.0, 0.001, "install before remove: no dip to the base max");
	}

	public void TestTwoSlotsHoldingTheSameAttackBothFire()
	{
		Rig();
		ProbeAbilityDef probe = Probe();
		_loadout.SetSlot(0, Attack(probe), Grade.Common);
		_loadout.SetSlot(1, Attack(probe), Grade.Common);
		Assert.True(Fire(_installer.InstalledGrant(0)));
		Assert.True(Fire(_installer.InstalledGrant(1)), "each slot has its own cooldown");
		Assert.False(Fire(_installer.InstalledGrant(0)));
		Assert.Eq(probe.Starts, 2);
	}

	public void TestAPromotedAttackFiresAtOnceWhileAnotherSlotKeepsItsCooldown()
	{
		Rig();
		ProbeAbilityDef probe = Probe();
		_loadout.SetSlot(0, Attack(probe), Grade.Common);
		_loadout.SetSlot(1, Attack(probe), Grade.Common);
		Fire(_installer.InstalledGrant(0));
		Fire(_installer.InstalledGrant(1));
		_loadout.SetSlot(0, Attack(probe), Grade.Rare);
		Assert.True(Fire(_installer.InstalledGrant(0)), "promoted: fresh cooldown");
		Assert.False(Fire(_installer.InstalledGrant(1)), "untouched: still cooling down");
	}
}
