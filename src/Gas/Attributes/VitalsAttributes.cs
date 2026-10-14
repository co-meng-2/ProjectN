using Gamesmiths.Forge.Attributes;

namespace VampireSurvivor.Gas.Attributes;

/// <summary>Hit points. Health is clamped to [0, MaxHealth]: a lowered MaxHealth pulls Health down, a raised one never heals.</summary>
public class VitalsAttributes : AttributeSet
{
	public VitalsAttributes()
	{
		MaxHealth = InitializeAttribute(nameof(MaxHealth), 10_000, 1, 10_000_000, decimalPlaces: Stats.HealthDecimals);
		Health = InitializeAttribute(nameof(Health), 10_000, 0, 10_000, decimalPlaces: Stats.HealthDecimals);
	}

	public EntityAttribute Health { get; }

	public EntityAttribute MaxHealth { get; }

	protected override void AttributeOnValueChanged(EntityAttribute attribute, int change)
	{
		if (attribute == MaxHealth)
		{
			SetAttributeMaxValue(Health, MaxHealth.CurrentValue);
		}
	}
}
