using VampireSurvivor.Defs;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Boss;

/// <summary>
/// A boss attack: an on-demand ability (the brain activates it) that owns state.casting while it runs, which stops the
/// boss chasing. Concrete patterns add their numbers as exports and a <see cref="PatternBehavior"/>.
/// </summary>
public abstract partial class PatternDef : ActiveAbilityDef
{
	protected PatternDef()
	{
		OwnedTags = [GameplayTags.StateCasting];
		AutoFire = false;
	}
}
