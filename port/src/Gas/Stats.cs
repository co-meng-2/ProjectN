namespace VampireSurvivor.Gas;

/// <summary>
/// Attribute keys ("{AttributeSetClass}.{Property}") and the fixed-point scale of each stat. Data and UI use floats;
/// Forge stores integers, so the conversion happens only at the edges (ApplyStats, DamageExecution, display).
/// </summary>
public static class Stats
{
	public const string Health = "VitalsAttributes.Health";
	public const string MaxHealth = "VitalsAttributes.MaxHealth";
	public const string MoveSpeed = "CombatAttributes.MoveSpeed";
	public const string Attack = "CombatAttributes.Attack";
	public const string GoldGain = "EconomyAttributes.GoldGain";
	public const string StoneGain = "EconomyAttributes.StoneGain";

	public const int HealthDecimals = 2;
	public const int SpeedDecimals = 2;
	public const int AttackDecimals = 3;
	public const int GainDecimals = 3;

	/// <summary>10^decimals: the raw value that stands for 1.0.</summary>
	public static int Scale(int decimals)
	{
		int scale = 1;
		for (int i = 0; i < decimals; i++)
		{
			scale *= 10;
		}

		return scale;
	}
}
