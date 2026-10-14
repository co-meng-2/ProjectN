using System;
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Defs;
using VampireSurvivor.Stage;

namespace VampireSurvivor.Events;

/// <summary>
/// "Destroy the statues": spawns statues at random arena points at least <see cref="MinDistance"/> from the building and
/// the player. All destroyed in time -> success; time up -> the rest are removed and the event fails.
/// </summary>
public partial class StatueEvent : StageEvent
{
	private readonly List<Statue> _statues = [];
	private int _destroyed;

	[Export]
	public PackedScene? StatueScene { get; set; }

	[Export]
	public int StatueCount { get; set; }

	[Export]
	public StatBlock? StatueStats { get; set; }

	[Export]
	public float MinDistance { get; set; }

	/// <summary>Minimum distance between two statues of the same event.</summary>
	[Export]
	public float StatueSpacing { get; set; }

	/// <summary>Keeps statues off the arena walls.</summary>
	[Export]
	public float EdgeMargin { get; set; }

	/// <summary>Random points tried per statue before settling for the best one found.</summary>
	[Export]
	public int PlacementTries { get; set; }

	public override List<string> Validate()
	{
		var errors = new List<string>();
		if (StatueScene is null)
		{
			errors.Add("StatueEvent: statue_scene is null");
		}
		else
		{
			Node probe = StatueScene.Instantiate();
			if (probe is not Statue)
			{
				errors.Add("StatueEvent: statue_scene root is not a Statue");
			}

			probe.Free();
		}

		if (StatueCount < 1)
		{
			errors.Add("StatueEvent: statue_count must be >= 1");
		}

		if (StatueStats is null)
		{
			errors.Add("StatueEvent: statue_stats is null");
		}
		else
		{
			errors.AddRange(StatueStats.Validate());
		}

		if (MinDistance < 0f || EdgeMargin < 0f || StatueSpacing < 0f)
		{
			errors.Add("StatueEvent: min_distance, statue_spacing and edge_margin must be >= 0");
		}

		if (PlacementTries < 1)
		{
			errors.Add("StatueEvent: placement_tries must be >= 1");
		}

		return errors;
	}

	public override List<Node2D> Objectives() => [.. _statues.FindAll(IsInstanceValid)];

	protected override void OnBegin()
	{
		for (int i = 0; i < StatueCount; i++)
		{
			var statue = StatueScene!.Instantiate<Statue>();
			AddChild(statue);
			statue.GlobalPosition = PickPoint();
			statue.Setup(StatueStats!);
			statue.Destroyed += OnStatueDestroyed;
			_statues.Add(statue);
		}

		Report();
	}

	protected override void Cleanup()
	{
		foreach (Statue statue in _statues)
		{
			if (IsInstanceValid(statue))
			{
				statue.Remove();
			}
		}

		_statues.Clear();
	}

	private Vector2 PickPoint()
	{
		Rect2 area = Context!.Bounds.Grow(-EdgeMargin);
		Vector2 best = area.GetCenter();
		float bestMargin = float.NegativeInfinity;
		for (int attempt = 0; attempt < PlacementTries; attempt++)
		{
			float x = Context.Rng.Range(area.Position.X, area.End.X);
			float y = Context.Rng.Range(area.Position.Y, area.End.Y);
			var point = new Vector2(x, y);
			float margin = Margin(point);
			if (margin >= 0f)
			{
				return point;
			}

			if (margin > bestMargin)
			{
				best = point;
				bestMargin = margin;
			}
		}

		return best;
	}

	/// <summary>
	/// How far <paramref name="point"/> is inside the placement rules: the smaller of (distance to the nearer of the building
	/// and the player - MinDistance) and (distance to the nearest placed statue - StatueSpacing). >= 0 is valid.
	/// </summary>
	private float Margin(Vector2 point)
	{
		float margin = float.PositiveInfinity;
		foreach (Node2D? anchor in new[] { Context!.Building, Context.Player })
		{
			if (IsInstanceValid(anchor))
			{
				margin = Math.Min(margin, point.DistanceTo(anchor!.GlobalPosition) - MinDistance);
			}
		}

		foreach (Statue statue in _statues)
		{
			if (IsInstanceValid(statue))
			{
				margin = Math.Min(margin, point.DistanceTo(statue.GlobalPosition) - StatueSpacing);
			}
		}

		return margin;
	}

	private void OnStatueDestroyed(Statue statue)
	{
		if (!IsRunning)
		{
			return;
		}

		_statues.Remove(statue);
		_destroyed++;
		Report();
		if (_destroyed >= StatueCount)
		{
			Succeed();
		}
	}

	private void Report() => ReportProgress($"Statues {_destroyed}/{StatueCount}");
}
