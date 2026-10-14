using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Stage;

namespace VampireSurvivor.Ui;

/// <summary>
/// Read-only view of the stage. It binds to models and never mutates them; whoever owns the stage (DevStage now, Main in
/// slice 5) binds it, because the stage layer must not know the HUD.
/// </summary>
public partial class Hud : CanvasLayer
{
	private Wallet? _wallet;

	[Export]
	public ProgressBar PlayerBar { get; set; } = null!;

	[Export]
	public ProgressBar BuildingBar { get; set; } = null!;

	[Export]
	public Label WalletLabel { get; set; } = null!;

	[Export]
	public TimelineBar Timeline { get; set; } = null!;

	[Export]
	public Label ResultLabel { get; set; } = null!;

	/// <summary>The shop's slots; bound by whoever owns the stage, which also routes its requests.</summary>
	[Export]
	public AbilityBar AbilityBar { get; set; } = null!;

	[Export]
	public EventBanner EventBanner { get; set; } = null!;

	[Export]
	public ObjectiveArrows ObjectiveArrows { get; set; } = null!;

	[Export]
	public BossBar BossBar { get; set; } = null!;

	public void Bind(Health playerHealth, Health buildingHealth, Wallet wallet, StageClock clock, Timeline timeline, float[]? eventTimes = null)
	{
		playerHealth.Changed += OnPlayerHealthChanged;
		buildingHealth.Changed += OnBuildingHealthChanged;
		SetBar(PlayerBar, playerHealth.Current(), playerHealth.Maximum());
		SetBar(BuildingBar, buildingHealth.Current(), buildingHealth.Maximum());
		_wallet = wallet;
		wallet.Changed += OnWalletChanged;
		OnWalletChanged(wallet.Gold, wallet.Stone, wallet.Tickets);
		Timeline.Configure(timeline.Duration, timeline.Marks, eventTimes);
		clock.Ticked += Timeline.SetElapsed;
	}

	/// <summary>EventDirector.EventStarted.</summary>
	public void ShowEvent(EventDef def, StageEvent stageEvent)
	{
		EventBanner.ShowEvent(def, stageEvent);
		ObjectiveArrows.Track(stageEvent);
	}

	/// <summary>EventDirector.EventProgressed.</summary>
	public void ShowEventProgress(EventDef def, string text) => EventBanner.ShowProgress(text);

	/// <summary>EventDirector.EventEnded.</summary>
	public void ShowEventResult(EventDef def, bool success)
	{
		EventBanner.ShowResult(def, success);
		ObjectiveArrows.Clear();
	}

	/// <summary>EventDirector.EventAborted.</summary>
	public void ShowEventAborted(EventDef def)
	{
		EventBanner.ShowAborted(def);
		ObjectiveArrows.Clear();
	}

	/// <summary>StageRoot.BossAppeared.</summary>
	public void ShowBoss(Health bossHealth, string displayName)
	{
		BossBar.ShowBoss(bossHealth, displayName);
		Timeline.ShowBoss();
	}

	public void ShowResult(RunResult result)
	{
		string title = result.Outcome == RunOutcome.Cleared ? "STAGE CLEARED" : "GAME OVER";
		ResultLabel.Text = $"{title}\nKills {result.Kills}   Gold {result.Gold}   Stone {result.Stone}\nR: restart";
		ResultLabel.Visible = true;
	}

	/// <summary>The wallet is a plain C# object that can outlive this node; Godot signals disconnect themselves.</summary>
	public override void _ExitTree()
	{
		if (_wallet is not null)
		{
			_wallet.Changed -= OnWalletChanged;
			_wallet = null;
		}
	}

	private static void SetBar(ProgressBar bar, float current, float maximum)
	{
		bar.MaxValue = Mathf.Max(maximum, 1f);
		bar.Value = current;
	}

	private void OnPlayerHealthChanged(float current, float maximum) => SetBar(PlayerBar, current, maximum);

	private void OnBuildingHealthChanged(float current, float maximum) => SetBar(BuildingBar, current, maximum);

	private void OnWalletChanged(int gold, int stone, int tickets)
	{
		WalletLabel.Text = $"Gold {gold}   Stone {stone}   Ticket {tickets}";
	}
}
