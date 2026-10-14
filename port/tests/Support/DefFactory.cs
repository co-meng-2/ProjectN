using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Tests.Support;

/// <summary>Small, valid definitions for tests (the real ones are .tres files).</summary>
public static class DefFactory
{
	public static Texture2D Texture(int width = 60, int height = 10)
	{
		return ImageTexture.CreateFromImage(Image.CreateEmpty(width, height, false, Image.Format.Rgba8));
	}

	public static VisualDef Visual()
	{
		var strip = new AnimationStrip
		{
			Animation = AnimNames.Idle,
			Texture = Texture(60, 10),
			FrameCount = 6,
		};
		var def = new VisualDef();
		def.Strips.Add(strip);
		return def;
	}

	public static EnemyDef Enemy(PackedScene? scene = null) => new()
	{
		Id = "test_enemy",
		Scene = scene ?? new PackedScene(),
		Visual = Visual(),
		Stats = new StatBlock(),
	};

	public static StageDef Stage(float duration = 10f) => new()
	{
		Id = "test_stage",
		Duration = duration,
		GroundTexture = Texture(32, 32),
		BuildingTexture = Texture(64, 64),
		BuildingStats = new StatBlock(),
		Waves = new WaveSchedule(),
		GradeTable = GradeTable(),
	};

	public static GradeTable GradeTable() => new()
	{
		PromoteGold = [100, 150, 250, 400, 600],
		RevealStone = 10,
		RerollStone = 2,
	};

	/// <summary>A valid shop entry; give it a passive and/or an active ability.</summary>
	public static AbilityDef Ability(
		string id, PassiveDef? passive = null, ActiveAbilityDef? active = null, Grade minGrade = Grade.Common) => new()
	{
		Id = id,
		DisplayName = id,
		Icon = Texture(16, 16),
		MinGrade = minGrade,
		Passive = passive,
		Active = active,
	};

	/// <summary>Packs <paramref name="node"/> into a scene and frees it (the scene keeps its own copy).</summary>
	public static PackedScene Packed(Node node)
	{
		var scene = new PackedScene();
		scene.Pack(node);
		node.Free();
		return scene;
	}

	public static RewardDef Reward(int gold = 150, int stone = 2, int tickets = 1) =>
		new() { Gold = gold, Stone = stone, Tickets = tickets };

	public static EventDef Event(PackedScene scene, float timeLimit = 20f, string id = "probe") => new()
	{
		Id = id,
		DisplayName = id.Capitalize(),
		ControllerScene = scene,
		TimeLimit = timeLimit,
		Reward = Reward(),
	};

	public static EventSchedule Schedule(
		IEnumerable<EventDef?> pool,
		int count = 1,
		float windowStart = 10f,
		float windowEnd = 50f,
		float minGap = 20f,
		float waveClearance = 0f)
	{
		var schedule = new EventSchedule
		{
			Count = count,
			WindowStart = windowStart,
			WindowEnd = windowEnd,
			MinGap = minGap,
			WaveClearance = waveClearance,
		};
		foreach (EventDef? def in pool)
		{
			schedule.Pool.Add(def!);
		}

		return schedule;
	}

	/// <summary>
	/// A valid boss def with contact damage 0 (tests then see only pattern damage), Sequence order, gap 2 s and one
	/// weight-1 entry per pattern. <paramref name="scene"/> defaults to an empty PackedScene (enough for validation).
	/// </summary>
	public static BossDef Boss(PackedScene? scene = null, params ActiveAbilityDef[] patterns)
	{
		var def = new BossDef
		{
			Id = "test_boss",
			DisplayName = "Test Boss",
			Scene = scene ?? new PackedScene(),
			Visual = Visual(),
			Stats = new StatBlock { MaxHealth = 500f },
			ContactDamage = 0f,
			ContactInterval = 1f,
			BodyRadius = 20f,
			PatternGap = 2f,
			Order = BossOrder.Sequence,
		};
		foreach (ActiveAbilityDef pattern in patterns)
		{
			def.Patterns.Add(new BossPatternEntry { Pattern = pattern, Weight = 1f });
		}

		return def;
	}

	/// <summary>Max health +20/40/70/110/160/240 over the six grade levels.</summary>
	public static AbilityDef VitalityAbility() => Ability("vitality", Vitality());

	public static PlayerProfile Profile() => new()
	{
		Stats = new StatBlock(),
		Visual = Visual(),
	};

	/// <summary>A curve with points at x = 1..n.</summary>
	public static Curve CurveOf(params float[] ys)
	{
		var curve = new Curve { MinDomain = 1f, MaxDomain = ys.Length, MaxValue = 1000f };
		for (int i = 0; i < ys.Length; i++)
		{
			curve.AddPoint(new Vector2(i + 1, ys[i]));
		}

		return curve;
	}

	public static PassiveDef Might() => new()
	{
		Stat = StatId.Attack,
		Operation = PassiveOperation.Percent,
		ByLevel = CurveOf(0.1f, 0.2f, 0.35f, 0.5f, 0.7f, 1.0f),
	};

	public static PassiveDef Vitality() => new()
	{
		Stat = StatId.MaxHealth,
		Operation = PassiveOperation.Flat,
		ByLevel = CurveOf(20f, 40f, 70f, 110f, 160f, 240f),
	};

	/// <summary>
	/// A private copy of a shipped resource, loaded past the cache so a test can change it. Used instead of
	/// <c>Duplicate()</c>, which on resources with C# scripts could crash the .NET finalizer.
	/// </summary>
	public static T Fresh<T>(string path)
		where T : Resource => ResourceLoader.Load<T>(path, null, ResourceLoader.CacheMode.Ignore);
}
