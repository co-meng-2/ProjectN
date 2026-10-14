using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Meta;
using VampireSurvivor.Meta.SkillTree;
using VampireSurvivor.Stage;
using VampireSurvivor.Ui;

namespace VampireSurvivor.App;

/// <summary>
/// The stage screen: runs a stage with its HUD (and the profile built from the purchased skills), routes HUD requests
/// to the stage, pays the run's gem into the save, and leaves for the hub on request. Reached through SceneRouter
/// (param: the StageDef).
/// </summary>
public partial class Main : Node
{
	public const string StageScene = "res://port/src/Stage/stage.tscn";
	public const string HudScene = "res://port/src/Ui/Hud/hud.tscn";

	/// <summary>Used when the scene runs standalone (F6) with no params.</summary>
	public const string DefaultStageDef = "res://port/data/stages/stage_01.tres";
	public const string BaseProfile = "res://port/data/characters/default_profile.tres";
	public const string DefaultSkillTree = "res://port/data/skill_tree/skill_tree.tres";

	private static readonly Vector2 BuildingBarOffset = new(0f, -60f);

	private WorldHealthBar? _buildingBar;

	/// <summary>Collaborators default to the autoloads and shipped data in _Ready; tests set them first.</summary>
	public SaveService? SaveService { get; set; }

	public SceneRouter? Router { get; set; }

	public StageDef? StageDef { get; set; }

	public SkillTreeDef? SkillTree { get; set; }

	public StageRoot? Stage { get; private set; }

	public Hud? Hud { get; private set; }

	public RunResult? Result { get; private set; }

	public override void _Ready()
	{
		SaveService ??= Autoloads.Save(this);
		Router ??= Autoloads.Router(this);
		StageDef ??= Router.TakeParams().GetValueOrDefault(SceneRouter.ParamStageDef) as StageDef ?? GD.Load<StageDef>(DefaultStageDef);
		SkillTree ??= GD.Load<SkillTreeDef>(DefaultSkillTree);
		StartRun();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Result is null)
		{
			return;
		}

		if (@event.IsActionPressed(InputActions.Restart))
		{
			StartRun();
		}
		else if (@event.IsActionPressed(InputActions.Confirm))
		{
			Router!.Go(SceneRouter.Route.Hub);
		}
	}

	/// <summary>StageRoot.Finished. Pays the gem and records a clear once per run, then saves once.</summary>
	public void OnStageFinished(RunResult result)
	{
		if (Result is not null)
		{
			return;
		}

		result.Gem = StageDef!.GemFor(result.Outcome);
		Result = result;
		SaveService!.AddGem(result.Gem);
		if (result.Outcome == RunOutcome.Cleared)
		{
			SaveService.RecordClear(StageDef.Id);
		}

		SaveService.Save();
		Hud!.ShowResult(result);
		GD.Print($"[Main] {result.Outcome} after {result.Elapsed:0.0}s - kills {result.Kills}, gold {result.Gold}, stone {result.Stone}, gem {result.Gem}");
	}

	private void StartRun()
	{
		DiscardRun();
		Result = null;
		Stage = GD.Load<PackedScene>(StageScene).Instantiate<StageRoot>();
		Hud = GD.Load<PackedScene>(HudScene).Instantiate<Hud>();
		_buildingBar = new WorldHealthBar();
		AddChild(Stage);
		AddChild(_buildingBar);
		AddChild(Hud);
		if (!Stage.Start(StageDef!, RunProfile()))
		{
			GetTree().Quit(1);
			return;
		}

		Hud.Bind(Stage.Player.Health, Stage.Building.Health, Stage.RunState!.Wallet, Stage.Clock, Stage.Waves.Timeline, Stage.Events.EventTimes());
		Stage.Events.EventStarted += Hud.ShowEvent;
		Stage.Events.EventProgressed += Hud.ShowEventProgress;
		Stage.Events.EventEnded += Hud.ShowEventResult;
		Stage.Events.EventAborted += Hud.ShowEventAborted;
		Stage.BossAppeared += Hud.ShowBoss;
		Hud.AbilityBar.Bind(Stage.Loadout!, Stage.RunState.Wallet, Stage.GradeService!);
		Hud.AbilityBar.PromoteRequested += OnPromoteRequested;
		Hud.AbilityBar.RerollRequested += OnRerollRequested;
		_buildingBar.Bind(Stage.Building.Health, Stage.Building, BuildingBarOffset);
		Stage.Finished += OnStageFinished;
	}

	/// <summary>Base profile plus the purchased skill levels. A broken skill tree must not stop a run: fall back to the base profile.</summary>
	private PlayerProfile RunProfile()
	{
		var baseProfile = GD.Load<PlayerProfile>(BaseProfile);
		List<string> errors = SkillTree!.Validate();
		if (errors.Count > 0)
		{
			GD.PushError($"Main: unusable skill tree, starting with the base profile: {string.Join(", ", errors)}");
			return baseProfile;
		}

		return ProfileBuilder.Build(baseProfile, SkillTree, SaveService!.Data.SkillLevels);
	}

	/// <summary>Removed from the tree at once (so the old enemies leave their group), freed at the end of the frame.</summary>
	private void DiscardRun()
	{
		foreach (Node? node in new Node?[] { Stage, _buildingBar, Hud })
		{
			if (node is not null && IsInstanceValid(node))
			{
				RemoveChild(node);
				node.QueueFree();
			}
		}
	}

	/// <summary>The HUD only asks; the app decides. The shop closes with the run.</summary>
	private void OnPromoteRequested(int index)
	{
		if (Result is null)
		{
			Stage?.GradeService?.Promote(index);
		}
	}

	private void OnRerollRequested(int index)
	{
		if (Result is null)
		{
			Stage?.GradeService?.Reroll(index);
		}
	}
}
