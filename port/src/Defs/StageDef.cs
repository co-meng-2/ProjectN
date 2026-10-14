using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.Defs;

/// <summary>
/// Static description of one stage. Events and boss (slice 4) and the gem reward (slice 5) are added by later slices.
/// </summary>
public partial class StageDef : Resource
{
	[Export]
	public StringName Id { get; set; } = string.Empty;

	[Export]
	public string DisplayName { get; set; } = string.Empty;

	[Export]
	public float Duration { get; set; } = 180f;

	[Export]
	public Vector2 ArenaSize { get; set; } = new(2400f, 1600f);

	[Export]
	public Texture2D? GroundTexture { get; set; }

	[Export]
	public Texture2D? BuildingTexture { get; set; }

	[Export]
	public StatBlock? BuildingStats { get; set; }

	[Export]
	public Godot.Collections.Array<EnemyDef> TrickleEnemies { get; set; } = [];

	[Export]
	public float TrickleIntervalStart { get; set; } = 2f;

	[Export]
	public float TrickleIntervalEnd { get; set; } = 0.8f;

	[Export]
	public int TrickleCap { get; set; } = 60;

	[Export]
	public float SpawnRadius { get; set; } = 520f;

	[Export]
	public WaveSchedule? Waves { get; set; }

	[Export]
	public int StartGold { get; set; }

	[Export]
	public int StartStone { get; set; }

	/// <summary>Scales every enemy drop; 1.0 for balanced play, very large for prototype tuning.</summary>
	[Export]
	public float DropMultiplier { get; set; } = 1f;

	/// <summary>Shop prices of this stage.</summary>
	[Export]
	public GradeTable? GradeTable { get; set; }

	public List<string> Validate()
	{
		var errors = new List<string>();
		string id = Id.ToString();
		if (Duration <= 0f)
		{
			errors.Add($"StageDef '{id}': duration must be > 0");
		}

		if (ArenaSize.X <= 0f || ArenaSize.Y <= 0f || SpawnRadius <= 0f)
		{
			errors.Add($"StageDef '{id}': arena_size and spawn_radius must be > 0");
		}

		if (TrickleIntervalStart <= 0f || TrickleIntervalEnd <= 0f || TrickleCap < 0)
		{
			errors.Add($"StageDef '{id}': trickle intervals must be > 0 and trickle_cap >= 0");
		}

		if (GroundTexture is null || BuildingTexture is null)
		{
			errors.Add($"StageDef '{id}': ground_texture and building_texture are required");
		}

		if (BuildingStats is null)
		{
			errors.Add($"StageDef '{id}': building_stats is null");
		}
		else
		{
			errors.AddRange(BuildingStats.Validate());
		}

		foreach (EnemyDef? enemy in TrickleEnemies)
		{
			if (enemy is null)
			{
				errors.Add($"StageDef '{id}': null trickle enemy");
			}
			else
			{
				errors.AddRange(enemy.Validate());
			}
		}

		if (Waves is not null)
		{
			errors.AddRange(Waves.ValidateAgainst(Duration));
		}

		if (StartGold < 0 || StartStone < 0)
		{
			errors.Add($"StageDef '{id}': start_gold and start_stone must be >= 0");
		}

		if (DropMultiplier <= 0f)
		{
			errors.Add($"StageDef '{id}': drop_multiplier must be > 0");
		}

		if (GradeTable is null)
		{
			errors.Add($"StageDef '{id}': grade_table is null");
		}
		else
		{
			errors.AddRange(GradeTable.Validate());
		}

		return errors;
	}
}
