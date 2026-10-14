using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;

namespace VampireSurvivor.Defs;

/// <summary>One possible drop of a <see cref="DropTable"/>.</summary>
[GlobalClass]
public partial class DropEntry : Resource
{
	[Export]
	public CurrencyKind Kind { get; set; } = CurrencyKind.Gold;

	[Export]
	public Texture2D? Icon { get; set; }

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float Chance { get; set; } = 1f;

	[Export]
	public int MinAmount { get; set; } = 1;

	[Export]
	public int MaxAmount { get; set; } = 1;

	public List<string> Validate()
	{
		var errors = new List<string>();
		if (Chance < 0f || Chance > 1f)
		{
			errors.Add("DropEntry: chance must be within [0, 1]");
		}

		if (MinAmount < 1 || MaxAmount < MinAmount)
		{
			errors.Add("DropEntry: need 1 <= min_amount <= max_amount");
		}

		return errors;
	}
}
