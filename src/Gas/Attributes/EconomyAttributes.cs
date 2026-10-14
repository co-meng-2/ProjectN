using Gamesmiths.Forge.Attributes;

namespace VampireSurvivor.Gas.Attributes;

/// <summary>Multipliers applied to currency the actor collects.</summary>
public class EconomyAttributes : AttributeSet
{
	public EconomyAttributes()
	{
		GoldGain = InitializeAttribute(nameof(GoldGain), 1_000, 0, 1_000_000, decimalPlaces: Stats.GainDecimals);
		StoneGain = InitializeAttribute(nameof(StoneGain), 1_000, 0, 1_000_000, decimalPlaces: Stats.GainDecimals);
	}

	public EntityAttribute GoldGain { get; }

	public EntityAttribute StoneGain { get; }
}
