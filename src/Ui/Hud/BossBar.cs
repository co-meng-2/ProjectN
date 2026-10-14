using System;
using Godot;
using VampireSurvivor.Components;

namespace VampireSurvivor.Ui;

/// <summary>The boss's name and health bar, shown once the boss appears. A view over its Health.</summary>
public partial class BossBar : VBoxContainer
{
	private readonly Label _nameLabel = new();
	private readonly ProgressBar _bar = new();
	private Health? _health;

	[Export]
	public Color FillColor { get; set; } = new(0.85f, 0.2f, 0.2f);

	[Export]
	public Vector2 BarSize { get; set; } = new(400f, 14f);

	public string BossName => _nameLabel.Text;

	public double Value => _bar.Value;

	public double Maximum => _bar.MaxValue;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_bar.CustomMinimumSize = BarSize;
		_bar.ShowPercentage = false;
		_bar.SelfModulate = FillColor;
		AddChild(_nameLabel);
		AddChild(_bar);
		Visible = false;
	}

	public void ShowBoss(Health health, string displayName)
	{
		if (IsInstanceValid(_health))
		{
			_health!.Changed -= OnHealthChanged;
		}

		_health = health;
		_nameLabel.Text = displayName;
		health.Changed += OnHealthChanged;
		OnHealthChanged(health.Current(), health.Maximum());
		Visible = true;
	}

	private void OnHealthChanged(float current, float maximum)
	{
		_bar.MaxValue = Math.Max(maximum, 1f);
		_bar.Value = current;
	}
}
