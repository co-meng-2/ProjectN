using Godot;
using VampireSurvivor.Defs;
using VampireSurvivor.Stage;

namespace VampireSurvivor.Tools;

/// <summary>
/// Measures the frame cost of N chasing enemies (spec 8.12): spawns them around the building on a stage that cannot end,
/// waits <see cref="WarmupSeconds"/>, then averages Godot's monitors over <see cref="SampleSeconds"/>. Run windowed for real
/// FPS, or headless for the physics/process cost alone.
/// </summary>
public partial class SwarmBench : Node
{
	private const float WarmupSeconds = 2f;
	private const float SampleSeconds = 5f;
	private static readonly int[] Counts = [100, 300];

	private int _countIndex;
	private StageRoot? _stage;
	private float _clock;
	private int _frames;
	private double _physicsMs;
	private double _processMs;
	private double _fps;

	public override void _Ready()
	{
		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
		StartRound();
	}

	public override void _Process(double delta)
	{
		_clock += (float)delta;
		if (_clock < WarmupSeconds)
		{
			return;
		}

		_frames++;
		_physicsMs += Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0;
		_processMs += Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0;
		_fps += Performance.GetMonitor(Performance.Monitor.TimeFps);
		if (_clock < WarmupSeconds + SampleSeconds)
		{
			return;
		}

		GD.Print($"[Bench] enemies={Counts[_countIndex]} alive={_stage!.Factory.AliveCount} physics_ms={_physicsMs / _frames:0.00} process_ms={_processMs / _frames:0.00} fps={_fps / _frames:0}");
		_countIndex++;
		if (_countIndex >= Counts.Length)
		{
			GetTree().Quit();
			return;
		}

		StartRound();
	}

	private void StartRound()
	{
		if (_stage is not null)
		{
			RemoveChild(_stage);
			_stage.QueueFree();
		}

		var def = ResourceLoader.Load<StageDef>("res://data/stages/stage_01.tres", null, ResourceLoader.CacheMode.Ignore);
		def.TrickleEnemies = [];
		def.Waves = null;
		def.Duration = 3600f;
		def.BuildingStats = new StatBlock { MaxHealth = 1_000_000f, MoveSpeed = 0f };
		var profile = ResourceLoader.Load<PlayerProfile>("res://data/characters/default_profile.tres", null, ResourceLoader.CacheMode.Ignore);
		profile.Stats = new StatBlock { MaxHealth = 1_000_000f, MoveSpeed = 140f };
		_stage = GD.Load<PackedScene>("res://src/Stage/stage.tscn").Instantiate<StageRoot>();
		AddChild(_stage);
		_stage.Start(def, profile, 1);
		_stage.Player.AutoCaster.Enabled = false;
		EnemyDef orc = GD.Load<EnemyDef>("res://data/enemies/orc.tres");
		int count = Counts[_countIndex];
		for (int i = 0; i < count; i++)
		{
			float angle = Mathf.Tau * i / count;
			_stage.Factory.Spawn(orc, _stage.Building.GlobalPosition + (Vector2.Right.Rotated(angle) * (200f + (i % 5 * 60f))));
		}

		_clock = 0f;
		_frames = 0;
		_physicsMs = 0.0;
		_processMs = 0.0;
		_fps = 0.0;
	}
}
