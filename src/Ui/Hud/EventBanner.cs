using System.Collections.Generic;
using Godot;
using VampireSurvivor.Defs;
using VampireSurvivor.Stage;

namespace VampireSurvivor.Ui;

/// <summary>
/// The running event's name, progress and remaining time; after it ends, "&lt;name&gt;: Success +rewards" or
/// "&lt;name&gt;: Failed" (or "&lt;name&gt;: Interrupted" when aborted) for <see cref="ResultSeconds"/>. A view: it reads
/// the event and never changes it.
/// </summary>
public partial class EventBanner : VBoxContainer
{
	private readonly Label _title = new();
	private readonly Label _progress = new();
	private readonly Label _timer = new();
	private StageEvent? _event;
	private bool _tracking;
	private float _limit;
	private float _resultLeft;

	[Export]
	public float ResultSeconds { get; set; } = 2.5f;

	[Export]
	public Color SuccessColor { get; set; } = new(0.55f, 1f, 0.5f);

	[Export]
	public Color FailureColor { get; set; } = new(1f, 0.45f, 0.4f);

	public string TitleText => _title.Text;

	public string ProgressText => _progress.Text;

	public string TimerText => _timer.Text;

	public static string RewardText(RewardDef? reward)
	{
		if (reward is null)
		{
			return string.Empty;
		}

		var parts = new List<string>();
		if (reward.Gold > 0)
		{
			parts.Add($"+{reward.Gold} gold");
		}

		if (reward.Stone > 0)
		{
			parts.Add($"+{reward.Stone} stone");
		}

		if (reward.Tickets > 0)
		{
			parts.Add($"+{reward.Tickets} {(reward.Tickets == 1 ? "ticket" : "tickets")}");
		}

		return string.Join(" ", parts);
	}

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		foreach (Label label in new[] { _title, _progress, _timer })
		{
			label.HorizontalAlignment = HorizontalAlignment.Right;
			label.MouseFilter = MouseFilterEnum.Ignore;
			AddChild(label);
		}

		Visible = false;
	}

	public void ShowEvent(EventDef def, StageEvent stageEvent)
	{
		_event = stageEvent;
		_limit = def.TimeLimit;
		_tracking = true;
		_resultLeft = 0f;
		_title.Text = def.DisplayName;
		_title.Modulate = Colors.White;
		_progress.Text = string.Empty;
		_timer.Text = string.Empty;
		Visible = true;
		Step(0f);
	}

	public void ShowProgress(string text) => _progress.Text = text;

	public void ShowResult(EventDef def, bool success)
	{
		EndTracking(success
			? $"{def.DisplayName}: Success {RewardText(def.Reward)}"
			: $"{def.DisplayName}: Failed");
		_title.Modulate = success ? SuccessColor : FailureColor;
	}

	public void ShowAborted(EventDef def)
	{
		EndTracking($"{def.DisplayName}: Interrupted");
		_title.Modulate = Colors.White;
	}

	public void Step(float delta)
	{
		if (_tracking)
		{
			if (IsInstanceValid(_event))
			{
				float left = _event!.RemainingTime;
				if (!_event.IsRunning && left <= 0f)
				{
					left = _limit; // EventStarted fires before Begin sets the timer
				}

				_timer.Text = $"{Mathf.CeilToInt(left)}s";
			}

			return;
		}

		if (_resultLeft > 0f)
		{
			_resultLeft -= delta;
			if (_resultLeft <= 0f)
			{
				Visible = false;
			}
		}
	}

	public override void _Process(double delta) => Step((float)delta);

	private void EndTracking(string title)
	{
		_event = null;
		_tracking = false;
		_title.Text = title;
		_progress.Text = string.Empty;
		_timer.Text = string.Empty;
		_resultLeft = ResultSeconds;
		Visible = true;
	}
}
