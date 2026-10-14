using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Abilities;

/// <summary>One HUD slot's content: which ability (null = empty) at which grade. Written only by <see cref="Loadout"/>.</summary>
public sealed class AbilitySlot
{
	public AbilityDef? Def { get; internal set; }

	public Grade Grade { get; internal set; } = Grade.None;

	public bool IsEmpty => Def is null;
}
