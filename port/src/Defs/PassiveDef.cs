using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.Defs;

/// <summary>
/// A permanent stat modifier scaled by a curve over the level (grade 1..6 for abilities, purchased level for skills).
/// Forge's EffectData is built from this at load time (Gas/PassiveEffects) and never saved.
/// </summary>
[GlobalClass]
public partial class PassiveDef : Resource
{
	public const int LevelCount = 6;

	[Export]
	public StatId Stat { get; set; }

	[Export]
	public PassiveOperation Operation { get; set; }

	/// <summary>Six points at x = 1..6; y is the display-unit amount (Flat) or fraction (Percent).</summary>
	[Export]
	public Curve? ByLevel { get; set; }

	public List<string> Validate()
	{
		var errors = new List<string>();
		if (ByLevel is null)
		{
			errors.Add($"PassiveDef {Stat}: ByLevel curve is null");
			return errors;
		}

		if (ByLevel.PointCount != LevelCount)
		{
			errors.Add($"PassiveDef {Stat}: ByLevel needs {LevelCount} points, has {ByLevel.PointCount}");
			return errors;
		}

		bool anyNonZero = false;
		for (int i = 0; i < LevelCount; i++)
		{
			if (!Mathf.IsEqualApprox(ByLevel.GetPointPosition(i).X, i + 1))
			{
				errors.Add($"PassiveDef {Stat}: point {i} must be at x = {i + 1}");
			}

			anyNonZero |= !Mathf.IsZeroApprox(ByLevel.GetPointPosition(i).Y);
		}

		if (!anyNonZero)
		{
			errors.Add($"PassiveDef {Stat}: every level is 0 (the passive would do nothing)");
		}

		return errors;
	}
}
