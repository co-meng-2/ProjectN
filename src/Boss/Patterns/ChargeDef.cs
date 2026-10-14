using System.Collections.Generic;
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;

namespace VampireSurvivor.Boss;

/// <summary>
/// Charge: a warning line from the boss toward the player for <see cref="Warning"/> seconds, then the boss dashes
/// <see cref="Distance"/> at <see cref="Speed"/>. An AreaStrike riding on the boss hits the player (only) once per charge.
/// </summary>
[GlobalClass]
public partial class ChargeDef : PatternDef
{
	[Export]
	public PackedScene? StrikeScene { get; set; }

	[Export]
	public float Warning { get; set; }

	[Export]
	public float Speed { get; set; }

	[Export]
	public float Distance { get; set; }

	[Export]
	public float Damage { get; set; }

	/// <summary>Contact radius of the charging boss.</summary>
	[Export]
	public float HitRadius { get; set; }

	[Export]
	public float LineWidth { get; set; }

	public float ChargeSeconds() => Speed > 0f ? Distance / Speed : 0f;

	public override IAbilityBehavior CreateBehavior() => new ChargeBehavior(this);

	public override List<string> Validate()
	{
		List<string> errors = base.Validate();
		if (StrikeScene is null)
		{
			errors.Add("Charge: strike_scene is required");
		}
		else if (!PatternScenes.RootIs<AreaStrike>(StrikeScene))
		{
			errors.Add("Charge: strike_scene root is not an AreaStrike");
		}

		if (Warning <= 0f || Speed <= 0f || Distance <= 0f || HitRadius <= 0f || LineWidth <= 0f)
		{
			errors.Add("Charge: warning, speed, distance, hit_radius and line_width must be > 0");
		}

		if (Damage < 0f)
		{
			errors.Add("Charge: damage must be >= 0");
		}

		return errors;
	}
}
