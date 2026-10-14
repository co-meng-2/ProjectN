using System.Diagnostics.CodeAnalysis;
using Gamesmiths.Forge.Attributes;
using Gamesmiths.Forge.Core;
using Gamesmiths.Forge.Effects.Magnitudes;
using VampireSurvivor.Defs;

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

	/// <summary>
	/// The entity's attribute for <paramref name="key"/>, or false when it has none. Forge 0.4.0 keeps
	/// EntityAttributes.ContainsAttribute internal; the capture definition's TryGetAttribute is the public route.
	/// </summary>
	public static bool TryGet(IForgeEntity? entity, string key, [NotNullWhen(true)] out EntityAttribute? attribute)
	{
		return new AttributeCaptureDefinition(key, AttributeCaptureSource.Target).TryGetAttribute(entity, out attribute);
	}

	public static string KeyOf(StatId stat) => stat switch
	{
		StatId.MaxHealth => MaxHealth,
		StatId.MoveSpeed => MoveSpeed,
		StatId.Attack => Attack,
		StatId.GoldGain => GoldGain,
		_ => StoneGain,
	};

	public static int DecimalsOf(StatId stat) => stat switch
	{
		StatId.MaxHealth => HealthDecimals,
		StatId.MoveSpeed => SpeedDecimals,
		StatId.Attack => AttackDecimals,
		_ => GainDecimals,
	};

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
