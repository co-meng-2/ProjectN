using System.Collections.Generic;
using System.Linq;
using Godot;
using VampireSurvivor.Core;

namespace VampireSurvivor.Defs;

/// <summary>
/// Prices of the ability shop. <see cref="PromoteGold"/> is indexed by the grade being left (None..Unique);
/// Legendary -> Revealed costs stone, and every reroll costs stone.
/// </summary>
[GlobalClass]
public partial class GradeTable : Resource
{
	public const int PromoteSteps = 5;

	[Export]
	public int[] PromoteGold { get; set; } = [];

	[Export]
	public int RevealStone { get; set; }

	[Export]
	public int RerollStone { get; set; }

	/// <summary>Null when the grade cannot be promoted (Revealed) or the table is too short.</summary>
	public Price? PromotePrice(Grade grade)
	{
		if (grade == Grade.Revealed)
		{
			return null;
		}

		if (grade == Grade.Legendary)
		{
			return new Price(CurrencyKind.Stone, RevealStone);
		}

		int step = (int)grade;
		return step < PromoteGold.Length ? new Price(CurrencyKind.Gold, PromoteGold[step]) : null;
	}

	/// <summary>Null when the grade cannot be rerolled (None, Revealed).</summary>
	public Price? RerollPrice(Grade grade) => grade.IsRollable() ? new Price(CurrencyKind.Stone, RerollStone) : null;

	public List<string> Validate()
	{
		var errors = new List<string>();
		if (PromoteGold.Length != PromoteSteps)
		{
			errors.Add($"GradeTable: promote_gold needs {PromoteSteps} entries (None..Unique), has {PromoteGold.Length}");
		}

		if (PromoteGold.Any(cost => cost < 0))
		{
			errors.Add($"GradeTable: negative promote cost {PromoteGold.First(cost => cost < 0)}");
		}

		if (RevealStone < 0 || RerollStone < 0)
		{
			errors.Add("GradeTable: reveal_stone and reroll_stone must be >= 0");
		}

		return errors;
	}
}
