using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Stage;
using VampireSurvivor.Ui;

namespace VampireSurvivor.App;

/// <summary>
/// Temporary stage screen for the port: runs one stage with its HUD (ability shop included) and the building's health bar,
/// shows the result and restarts on R. The real Main (hub, save, gem) replaces it in slice 5.
/// </summary>
public partial class DevStage : Node
{
	public const string StageScene = "res://port/src/Stage/stage.tscn";
	public const string HudScene = "res://port/src/Ui/Hud/hud.tscn";

	private static readonly Vector2 BuildingBarOffset = new(0f, -60f);

	private WorldHealthBar? _buildingBar;

	[Export]
	public StageDef StageDef { get; set; } = null!;

	[Export]
	public PlayerProfile Profile { get; set; } = null!;

	public StageRoot? Stage { get; private set; }

	public Hud? Hud { get; private set; }

	public RunResult? Result { get; private set; }

	public override void _Ready() => StartRun();

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Result is not null && @event.IsActionPressed(InputActions.Restart))
		{
			StartRun();
		}
	}

	public void StartRun()
	{
		DiscardRun();
		Result = null;
		Stage = GD.Load<PackedScene>(StageScene).Instantiate<StageRoot>();
		Hud = GD.Load<PackedScene>(HudScene).Instantiate<Hud>();
		_buildingBar = new WorldHealthBar();
		AddChild(Stage);
		AddChild(_buildingBar);
		AddChild(Hud);
		if (!Stage.Start(StageDef, Profile))
		{
			GetTree().Quit(1);
			return;
		}

		Hud.Bind(Stage.Player.Health, Stage.Building.Health, Stage.RunState!.Wallet, Stage.Clock, Stage.Waves.Timeline);
		Hud.AbilityBar.Bind(Stage.Loadout!, Stage.RunState.Wallet, Stage.GradeService!);
		Hud.AbilityBar.PromoteRequested += OnPromoteRequested;
		Hud.AbilityBar.RerollRequested += OnRerollRequested;
		_buildingBar.Bind(Stage.Building.Health, Stage.Building, BuildingBarOffset);
		Stage.Finished += OnFinished;
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

	private void OnFinished(RunResult result)
	{
		if (Result is not null)
		{
			return;
		}

		Result = result;
		Hud!.ShowResult(result);
		GD.Print($"[DevStage] {result.Outcome} after {result.Elapsed:0.0}s - kills {result.Kills}, gold {result.Gold}, stone {result.Stone}");
	}
}
