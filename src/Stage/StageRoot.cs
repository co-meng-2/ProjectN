using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VampireSurvivor.Abilities;
using VampireSurvivor.Actors;
using VampireSurvivor.Boss;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
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

	/// <summary>Keeps summoned enemies inside the arena walls.</summary>
	private const float SummonEdgeMargin = 24f;

	private readonly StageFlow _flow = new();
	private readonly AbilityInstaller _installer = new();

	public event Action<RunResult>? Finished;

	/// <summary>The boss arrived; relayed from the boss director for the HUD.</summary>
	[Signal]
	public delegate void BossAppearedEventHandler(Health bossHealth, string displayName);

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

	/// <summary>Where event controllers (and what they place) live.</summary>
	[Export]
	public Node2D EventLayer { get; set; } = null!;

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

	/// <summary>The stage's events: their times for the timeline and start/progress/end signals for the HUD.</summary>
	[Export]
	public EventDirector Events { get; set; } = null!;

	[Export]
	public BossDirector Bosses { get; set; } = null!;

	public RunState? RunState { get; private set; }

	public StageState FlowState => _flow.State;

	/// <summary>The ability slots; the HUD binds to them through whoever presents the stage.</summary>
	public Loadout? Loadout { get; private set; }

	/// <summary>The shop rules over the slots, the run's wallet and rng.</summary>
	public GradeService? GradeService { get; private set; }

	/// <summary>The boss once it has appeared; null before (or on a stage without one).</summary>
	public BossActor? CurrentBoss => Bosses.Boss;

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

		// Defs cannot see the Forge tag registry (layer order), so the registry checks happen here, before any purchase.
		foreach (ActiveAbilityDef active in profile.InnateAbilities.Concat(profile.Abilities.Select(a => a?.Active)).OfType<ActiveAbilityDef>())
		{
			errors.AddRange(AbilityFactory.Validate(active));
		}

		errors.AddRange(EventDirector.ValidateDefs(def.Events));
		errors.AddRange(BossDirector.ValidateDef(def.Boss));
		errors.AddRange(BossBrain.ValidateDef(def.Boss));
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
		Events.Bind(def, Clock, EventContextFor, RunState.Rng, Waves.MarkTimes());
		Bosses.Bind(def.Boss, _flow, Events, EnemyLayer, Building, def.SpawnRadius, Arena.Bounds, RunState.Rng, Targets, ProjectileLayer, Summon);

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
		Events.EventEnded += OnEventEnded;
		Bosses.BossSpawned += OnBossSpawned;
		Bosses.BossAppeared += (health, name) => EmitSignal(SignalName.BossAppeared, health, name);
		Bosses.BossDefeated += () => Finish(RunOutcome.Cleared);
	}

	/// <summary>Time up: the boss fight starts when the stage has a boss; otherwise the stage is cleared.</summary>
	private void OnClockEnded()
	{
		if (_flow.IsTerminal)
		{
			return;
		}

		if (Bosses.HasBoss)
		{
			Bosses.Appear();
		}
		else
		{
			Finish(RunOutcome.Cleared);
		}
	}

	private EventContext EventContextFor(EventDef def) => new(
		EventLayer, Factory, Targets, Player, Building, RunState!.Rng, Arena.Bounds, RunState.StageDef.SpawnRadius, def);

	/// <summary>Boss summons go through the factory like every enemy, so they count as kills and drop loot.</summary>
	private void Summon(EnemyDef def, Vector2 position)
	{
		Factory.Spawn(def, SpawnGeometry.ClampToRect(position, Arena.Bounds, SummonEdgeMargin));
	}

	private void OnBossSpawned(BossActor boss) => BossBrain.AttachTo(boss, RunState!.Rng);

	/// <summary>Only success pays; a failed or aborted event credits nothing.</summary>
	private void OnEventEnded(EventDef def, bool success)
	{
		if (!success || def.Reward is null)
		{
			return;
		}

		Wallet wallet = RunState!.Wallet;
		wallet.Add(CurrencyKind.Gold, def.Reward.Gold);
		wallet.Add(CurrencyKind.Stone, def.Reward.Stone);
		wallet.Add(CurrencyKind.Ticket, def.Reward.Tickets);
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
		Events.AbortRunning();
		Clock.Stop();
		World.ProcessMode = ProcessModeEnum.Disabled;
		Finished?.Invoke(RunState!.ToResult(outcome, Clock.Elapsed));
	}
}
