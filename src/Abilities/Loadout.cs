using System;
using System.Collections.Generic;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Abilities;

/// <summary>The player's N ability slots. Writing a slot announces it; the installer and the HUD react to the event.</summary>
public sealed class Loadout
{
	private readonly List<AbilitySlot> _slots = [];

	public Loadout(int slotCount)
	{
		for (int i = 0; i < Math.Max(slotCount, 0); i++)
		{
			_slots.Add(new AbilitySlot());
		}
	}

	public event Action<int, AbilitySlot>? SlotChanged;

	public int Size => _slots.Count;

	/// <summary>Null when <paramref name="index"/> is out of range.</summary>
	public AbilitySlot? Slot(int index) => index >= 0 && index < _slots.Count ? _slots[index] : null;

	/// <summary>
	/// False (and nothing changes) for an out-of-range index. A null def empties the slot. Always announces on success, even
	/// when the content is identical: a reroll onto the same ability reinstalls it.
	/// </summary>
	public bool SetSlot(int index, AbilityDef? def, Grade grade)
	{
		AbilitySlot? target = Slot(index);
		if (target is null)
		{
			return false;
		}

		target.Def = def;
		target.Grade = def is null ? Grade.None : grade;
		SlotChanged?.Invoke(index, target);
		return true;
	}
}
