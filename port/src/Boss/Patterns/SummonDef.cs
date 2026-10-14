using System.Collections.Generic;
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Boss;

/// <summary>
/// Summon: <see cref="Count"/> enemies of <see cref="Enemy"/> evenly on a ring of <see cref="Radius"/> around the boss,
/// through the stage's summon (it spawns with the enemy factory, so summoned enemies count as kills and drop loot).
/// </summary>
public partial class SummonDef : PatternDef
{
	[Export]
	public EnemyDef? Enemy { get; set; }

	[Export]
	public int Count { get; set; }

	[Export]
	public float Radius { get; set; }

	public override IAbilityBehavior CreateBehavior() => new SummonBehavior(this);

	public override List<string> Validate()
	{
		List<string> errors = base.Validate();
		if (Enemy is null)
		{
			errors.Add("Summon: enemy is null");
		}
		else
		{
			errors.AddRange(Enemy.Validate());
		}

		if (Count < 1 || Radius <= 0f)
		{
			errors.Add("Summon: count must be >= 1 and radius > 0");
		}

		return errors;
	}
}
