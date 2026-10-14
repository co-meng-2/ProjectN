using Gamesmiths.Forge.Attributes;

namespace VampireSurvivor.Gas.Attributes;

/// <summary>Offensive and mobility stats.</summary>
public class CombatAttributes : AttributeSet
{
	public CombatAttributes()
	{
		MoveSpeed = InitializeAttribute(nameof(MoveSpeed), 10_000, 0, 1_000_000, decimalPlaces: Stats.SpeedDecimals);
		Attack = InitializeAttribute(nameof(Attack), 1_000, 0, 1_000_000, decimalPlaces: Stats.AttackDecimals);
	}

	/// <summary>Pixels per second (x100).</summary>
	public EntityAttribute MoveSpeed { get; }

	/// <summary>Damage multiplier for hits this actor lands (x1000).</summary>
	public EntityAttribute Attack { get; }
}
