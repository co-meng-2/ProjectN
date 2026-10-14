using System.Collections.Generic;
using VampireSurvivor.Abilities;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Abilities;

public class LoadoutTest : TestCase
{
	public void TestLoadoutStartsWithEmptySlots()
	{
		var loadout = new Loadout(4);
		Assert.Eq(loadout.Size, 4);
		for (int i = 0; i < 4; i++)
		{
			Assert.True(loadout.Slot(i)!.IsEmpty, $"slot {i}");
			Assert.Eq(loadout.Slot(i)!.Grade, Grade.None);
		}

		Assert.Null(loadout.Slot(4));
		Assert.Null(loadout.Slot(-1));
	}

	public void TestSetSlotWritesAndEmitsOnce()
	{
		var loadout = new Loadout(2);
		var changes = new List<(int Index, AbilitySlot Slot)>();
		loadout.SlotChanged += (index, slot) => changes.Add((index, slot));
		AbilityDef def = DefFactory.VitalityAbility();
		Assert.True(loadout.SetSlot(1, def, Grade.Rare));
		Assert.Eq(changes.Count, 1);
		Assert.Eq(changes[0].Index, 1);
		Assert.True(changes[0].Slot.Def == def);
		Assert.Eq(loadout.Slot(1)!.Grade, Grade.Rare);
		Assert.True(loadout.Slot(0)!.IsEmpty, "other slot untouched");
	}

	public void TestRewritingTheSameContentStillEmits()
	{
		var loadout = new Loadout(1);
		AbilityDef def = DefFactory.VitalityAbility();
		loadout.SetSlot(0, def, Grade.Rare);
		var changes = new List<int>();
		loadout.SlotChanged += (index, _) => changes.Add(index);
		loadout.SetSlot(0, def, Grade.Rare);
		Assert.SequenceEq(changes, new[] { 0 }, "a reroll onto the same ability must reinstall it");
	}

	public void TestClearingASlotForcesGradeNone()
	{
		var loadout = new Loadout(1);
		loadout.SetSlot(0, DefFactory.VitalityAbility(), Grade.Epic);
		loadout.SetSlot(0, null, Grade.Epic);
		Assert.True(loadout.Slot(0)!.IsEmpty);
		Assert.Eq(loadout.Slot(0)!.Grade, Grade.None);
	}

	public void TestOutOfRangeSetIsRefusedWithoutEmitting()
	{
		var loadout = new Loadout(1);
		int changes = 0;
		loadout.SlotChanged += (_, _) => changes++;
		Assert.False(loadout.SetSlot(1, DefFactory.VitalityAbility(), Grade.Common));
		Assert.False(loadout.SetSlot(-1, DefFactory.VitalityAbility(), Grade.Common));
		Assert.Eq(changes, 0);
	}
}
