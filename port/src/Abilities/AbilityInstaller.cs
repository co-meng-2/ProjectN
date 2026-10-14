using System.Collections.Generic;
using Gamesmiths.Forge.Effects;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Abilities;

/// <summary>
/// Mirrors a <see cref="Loadout"/> onto the player's entity: a slot's passive is held as an effect handle, its attack as an
/// <see cref="AbilityGrant"/> with the slot's own cooldown tag (so two slots holding the same attack stack on purpose).
/// A changed slot installs the new passive before removing the old one (no stat dip), but revokes the old attack before
/// granting the new one (a promoted or rerolled attack starts with a fresh cooldown).
/// </summary>
public sealed class AbilityInstaller
{
	private readonly Dictionary<int, ActiveEffectHandle> _effects = [];
	private readonly Dictionary<int, AbilityGrant> _grants = [];
	private Loadout? _loadout;
	private Player? _player;

	public void Bind(Loadout loadout, Player player)
	{
		Unbind();
		_loadout = loadout;
		_player = player;
		loadout.SlotChanged += OnSlotChanged;
		for (int index = 0; index < loadout.Size; index++)
		{
			Install(index, loadout.Slot(index));
		}
	}

	/// <summary>Stops listening and removes everything this installer put on the player.</summary>
	public void Unbind()
	{
		if (_loadout is not null)
		{
			_loadout.SlotChanged -= OnSlotChanged;
			_loadout = null;
		}

		UninstallAll();
	}

	public ActiveEffectHandle? InstalledEffect(int index) => _effects.GetValueOrDefault(index);

	public AbilityGrant? InstalledGrant(int index) => _grants.GetValueOrDefault(index);

	public void UninstallAll()
	{
		foreach (int index in new List<int>(_grants.Keys))
		{
			RevokeGrant(index);
		}

		foreach (int index in new List<int>(_effects.Keys))
		{
			RemoveEffect(index);
		}
	}

	private bool PlayerAlive => GodotObject.IsInstanceValid(_player);

	private void OnSlotChanged(int index, AbilitySlot slot)
	{
		ActiveEffectHandle? oldEffect = _effects.GetValueOrDefault(index);
		_effects.Remove(index);
		RevokeGrant(index);
		Install(index, slot);
		if (oldEffect is not null && PlayerAlive)
		{
			_player!.Entity.RemovePassive(oldEffect);
		}
	}

	private void Install(int index, AbilitySlot? slot)
	{
		if (slot?.Def is not { } def || !PlayerAlive)
		{
			return;
		}

		int level = (int)slot.Grade;
		if (def.Passive is not null && _player!.Entity.ApplyPassive(def.Passive, level) is { } handle)
		{
			_effects[index] = handle;
		}

		if (def.Active is not null)
		{
			_grants[index] = _player!.Grant(def.Active, level, GameplayTags.SlotCooldowns[index]);
		}
	}

	private void RevokeGrant(int index)
	{
		if (_grants.Remove(index, out AbilityGrant? grant) && PlayerAlive)
		{
			_player!.Revoke(grant);
		}
	}

	private void RemoveEffect(int index)
	{
		if (_effects.Remove(index, out ActiveEffectHandle? handle) && PlayerAlive)
		{
			_player!.Entity.RemovePassive(handle);
		}
	}
}
