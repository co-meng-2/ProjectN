using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.Defs;

/// <summary>What a successful stage event pays into the run's wallet. Failure pays nothing.</summary>
public partial class RewardDef : Resource
{
	[Export]
	public int Gold { get; set; }

	[Export]
	public int Stone { get; set; }

	[Export]
	public int Tickets { get; set; }

	public List<string> Validate()
	{
		var errors = new List<string>();
		if (Gold < 0 || Stone < 0 || Tickets < 0)
		{
			errors.Add("RewardDef: gold, stone and tickets must be >= 0");
		}
		else if (Gold == 0 && Stone == 0 && Tickets == 0)
		{
			errors.Add("RewardDef: pays nothing (gold, stone and tickets are all 0)");
		}

		return errors;
	}
}
