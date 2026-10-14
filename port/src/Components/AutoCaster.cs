using System.Collections.Generic;
using Gamesmiths.Forge.Abilities;
using Godot;

namespace VampireSurvivor.Components;

/// <summary>
/// Keeps trying to activate the abilities added to it. A failed activation (cooling down, nothing to shoot) costs nothing:
/// the ability itself decides when it actually fires and commits its cooldown.
/// </summary>
public partial class AutoCaster : Node
{
	private readonly List<AbilityGrant> _grants = [];

	public bool Enabled { get; set; } = true;

	public int Count => _grants.Count;

	public void Add(AbilityGrant grant)
	{
		if (!_grants.Contains(grant))
		{
			_grants.Add(grant);
		}
	}

	public void Remove(AbilityGrant? grant)
	{
		if (grant is not null)
		{
			_grants.Remove(grant);
		}
	}

	public void Step()
	{
		if (!Enabled)
		{
			return;
		}

		for (int i = _grants.Count - 1; i >= 0; i--)
		{
			AbilityHandle? ability = _grants[i].Ability;
			if (ability is null || !ability.IsValid)
			{
				_grants.RemoveAt(i);
				continue;
			}

			if (!ability.IsActive)
			{
				ability.Activate(out _);
			}
		}
	}

	public override void _PhysicsProcess(double delta) => Step();
}
