namespace VampireSurvivor.Defs;

/// <summary>The stats a passive can modify. The mapping to Forge attribute keys lives in Gas/Stats.</summary>
public enum StatId
{
	MaxHealth,
	MoveSpeed,
	Attack,
	GoldGain,
	StoneGain,
}

public enum PassiveOperation
{
	/// <summary>Adds the curve value in display units (+20 health).</summary>
	Flat,

	/// <summary>Adds the curve value as a fraction (0.1 = +10%).</summary>
	Percent,
}
