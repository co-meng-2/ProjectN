using System.Collections.Generic;
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;

namespace VampireSurvivor.Boss;

/// <summary>
/// Ground slam: a warning circle where the player stands when the slam starts; <see cref="Warning"/> seconds later an
/// AreaStrike there hits the player (only). Stepping out of the circle in time avoids it.
/// </summary>
public partial class GroundSlamDef : PatternDef
{
	[Export]
	public PackedScene? StrikeScene { get; set; }

	[Export]
	public float Warning { get; set; }

	[Export]
	public float Radius { get; set; }

	[Export]
	public float Damage { get; set; }

	/// <summary>How long the strike stays active; it must outlive a physics step so overlaps are reported.</summary>
	[Export]
	public float StrikeSeconds { get; set; }

	public override IAbilityBehavior CreateBehavior() => new GroundSlamBehavior(this);

	public override List<string> Validate()
	{
		List<string> errors = base.Validate();
		if (StrikeScene is null)
		{
			errors.Add("Ground slam: strike_scene is required");
		}
		else if (!PatternScenes.RootIs<AreaStrike>(StrikeScene))
		{
			errors.Add("Ground slam: strike_scene root is not an AreaStrike");
		}

		if (Warning <= 0f || Radius <= 0f || StrikeSeconds <= 0f)
		{
			errors.Add("Ground slam: warning, radius and strike_seconds must be > 0");
		}

		if (Damage < 0f)
		{
			errors.Add("Ground slam: damage must be >= 0");
		}

		return errors;
	}
}
