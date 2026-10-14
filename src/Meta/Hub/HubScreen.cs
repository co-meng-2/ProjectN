using System.Collections.Generic;
using Godot;
using VampireSurvivor.App;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Meta.Hub;

/// <summary>
/// The hub: one card per catalog stage, a Skill Tree button and Back. It owns no state: it asks the router to move on.
/// A stage opens once the previous one is cleared (see StageProgress).
/// </summary>
public partial class HubScreen : Control
{
	public const string StageCardScene = "res://src/Meta/Hub/stage_card.tscn";
	public const string DefaultCatalog = "res://data/stages/stage_catalog.tres";

	private StageProgress? _progress;

	[Export]
	public VBoxContainer StageList { get; set; } = null!;

	[Export]
	public Label EmptyLabel { get; set; } = null!;

	[Export]
	public Button SkillTreeButton { get; set; } = null!;

	[Export]
	public Button BackButton { get; set; } = null!;

	/// <summary>Default to the shipped catalog and the autoloads in _Ready; tests set them first.</summary>
	public StageCatalog? Catalog { get; set; }

	public SaveService? SaveService { get; set; }

	public SceneRouter? Router { get; set; }

	public override void _Ready()
	{
		Catalog ??= GD.Load<StageCatalog>(DefaultCatalog);
		SaveService ??= Autoloads.Save(this);
		Router ??= Autoloads.Router(this);
		BackButton.Pressed += () => Router.Go(SceneRouter.Route.MainMenu);
		SkillTreeButton.Pressed += () => Router.Go(SceneRouter.Route.SkillTree);
		List<string> errors = Catalog.Validate();
		if (errors.Count > 0)
		{
			GD.PushError($"Hub: unusable stage catalog: {string.Join(", ", errors)}");
			EmptyLabel.Visible = true;
			BackButton.GrabFocus();
			return;
		}

		_progress = new StageProgress(Catalog, SaveService.Data);
		var cardScene = GD.Load<PackedScene>(StageCardScene);
		StageCard? focusCard = null;
		for (int index = 0; index < Catalog.Stages.Count; index++)
		{
			var card = cardScene.Instantiate<StageCard>();
			StageList.AddChild(card);
			StageStatus status = _progress.Status(index);
			card.Setup(index, Catalog.Stages[index].DisplayName, status);
			card.PlayPressed += OnPlayPressed;
			if (focusCard is null && status == StageStatus.Available)
			{
				focusCard = card;
			}
		}

		// Enter should play the next stage; with everything cleared, the first one.
		(focusCard ?? StageList.GetChild<StageCard>(0)).PlayButton.GrabFocus();
	}

	/// <summary>Public so tests can prove a locked stage never starts, even if its button fired.</summary>
	public void OnPlayPressed(int index)
	{
		if (_progress is null || !_progress.IsUnlocked(index))
		{
			return;
		}

		Router!.Go(SceneRouter.Route.Stage, new() { [SceneRouter.ParamStageDef] = Catalog!.Stages[index] });
	}
}
