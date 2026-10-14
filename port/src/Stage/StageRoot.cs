using System;
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Abilities;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Stage;

/// <summary>
/// Composition root of one stage run. Builds everything from (StageDef, PlayerProfile), wires the signals between modules,
/// and reports a <see cref="RunResult"/>. Modules never look each other up, and the stage knows nothing about UI: whoever
/// presents it binds to the public models below. (Named StageRoot because a class Stage inside namespace
/// VampireSurvivor.Stage could not be referenced from other namespaces.)
/// </summary>
public partial class StageRoot : Node2D
{
	private static readonly Vector2 PlayerStart = new(0f, 150f);

	private readonly StageFlow _flow = new();
	private readonly AbilityInstaller _installer = new();

	public event Action<RunResult>? Finished;

	[Export]
	public Arena Arena { get; set; } = null!;

	[Export]
	public Node2D World { get; set; } = null!;

	[Export]
	public Player Player { get; set; } = null!;

	[Export]
	public Building Building { get; set; } = null!;

	[Export]
	public Node2D EnemyLayer { get; set; } = null!;

	[Export]
	public Node2D ProjectileLayer { get; set; } = null!;

	[Export]
	public Node2D PickupLayer { get; set; } = null!;

	[Export]
	public StageClock Clock { get; set; } = null!;

	[Export]
	public TargetProvider Targets { get; set; } = null!;

	[Export]
	public EnemyFactory Factory { get; set; } = null!;

	[Export]
	public SpawnDirector Spawner { get; set; } = null!;

	[Export]
	public WaveDirector Waves { get; set; } = null!;

	[Export]
	public LootDirector Loot { get; set; } = null!;

	public RunState? RunState { get; private set; }

	public StageState FlowState => _flow.State;

	/// <summary>The ability slots; the HUD binds to them through whoever presents the stage.</summary>
	public Loadout? Loadout { get; private set; }

	/// <summary>The shop rules over the slots, the run's wallet and rng.</summary>
	public GradeService? GradeService { get; private set; }

	/// <summary>Returns false, after logging every problem, when a def is invalid; nothing is started then.</summary>
	public bool Start(StageDef def, PlayerProfile profile, long runSeed = 0)
	{
		if (RunState is not null)
		{
			GD.PushError("StageRoot.Start: already started; a stage runs once");
			return false;
		}

		List<string> errors = def.Validate();
		errors.AddRange(profile.Validate());
		if (errors.Count > 0)
		{
			foreach (string error in errors)
			{
				GD.PushError($"StageRoot.Start: {error}");
			}

			return false;
		}

		RunState = new RunState(def, profile, runSeed);
		Arena.Build(def.ArenaSize, def.GroundTexture!);
		Building.Setup(def.BuildingTexture!, def.BuildingStats!);
		Player.GlobalPosition = PlayerStart;
		Player.Setup(profile, Targets, ProjectileLayer, RunState.Wallet);
		Player.LimitCamera(Arena.Bounds);
		Targets.Building = Building;
		Targets.Player = Player;
		Factory.Bind(EnemyLayer, Targets, RunState.Rng);

		// Enemies appear on a ring around the defended building, not around the roaming player.
		Spawner.Bind(def, Clock, Factory, Building, RunState.Rng, Arena.Bounds);
		Waves.Bind(def, Clock, Factory, Building, RunState.Rng, Arena.Bounds);
		Loot.Bind(Factory, PickupLayer, Player, RunState.Wallet, def.DropMultiplier);

		// The shop: pure rules over the run's wallet and rng; the installer mirrors the slots onto the player.
		Loadout = new Loadout(profile.SlotCount);
		GradeService = new GradeService(Loadout, new AbilityPool(profile.Abilities), def.GradeTable!, RunState.Wallet, RunState.Rng);
		_installer.Bind(Loadout, Player);
		Wire();
		_flow.TransitionTo(StageState.Running);
		Clock.Start(def.Duration);
		return true;
	}

	private void Wire()
	{
		Building.Destroyed += () => Finish(RunOutcome.Failed);
		Player.Died += () => Finish(RunOutcome.Failed);
		Clock.Ended += OnClockEnded;
		Factory.Spawned += OnEnemySpawned;
	}

	/// <summary>Time up clears the stage (slice 4 starts the boss fight here instead when the stage has one).</summary>
	private void OnClockEnded()
	{
		Finish(RunOutcome.Cleared);
	}

	private void OnEnemySpawned(Enemy enemy)
	{
		enemy.Died += OnEnemyDied;
	}

	/// <summary>Every kill is also a gameplay event on the player's entity, so on-kill abilities react through Forge.</summary>
	private void OnEnemyDied(Enemy enemy)
	{
		RunState!.Kills++;
		Player.Entity.Raise<Node>(GameplayTags.EventKill, enemy);
	}

	private void Finish(RunOutcome outcome)
	{
		if (_flow.IsTerminal)
		{
			return;
		}

		_flow.TransitionTo(outcome == RunOutcome.Cleared ? StageState.Cleared : StageState.Failed);
		Clock.Stop();
		World.ProcessMode = ProcessModeEnum.Disabled;
		Finished?.Invoke(RunState!.ToResult(outcome, Clock.Elapsed));
	}
}
