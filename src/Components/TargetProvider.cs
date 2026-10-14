using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Components;

/// <summary>Answers targeting questions: who an enemy should chase, who an ability should shoot.</summary>
public partial class TargetProvider : Node
{
	public Node2D? Building { get; set; }

	public Node2D? Player { get; set; }

	public Node2D? TargetFor(TargetPolicy policy)
	{
		Node2D? preferred = policy == TargetPolicy.Player ? Player : Building;
		Node2D? fallback = policy == TargetPolicy.Player ? Building : Player;
		if (IsInstanceValid(preferred))
		{
			return preferred;
		}

		return IsInstanceValid(fallback) ? fallback : null;
	}

	public Node2D? NearestEnemy(Vector2 origin, float maxRange)
	{
		List<Node2D> nearest = NearestEnemies(origin, maxRange, 1);
		return nearest.Count > 0 ? nearest[0] : null;
	}

	/// <summary>Up to <paramref name="count"/> living enemies within <paramref name="maxRange"/>, nearest first.</summary>
	public List<Node2D> NearestEnemies(Vector2 origin, float maxRange, int count)
	{
		var found = new List<Node2D>();
		if (count <= 0 || maxRange < 0f)
		{
			return found;
		}

		float limit = maxRange * maxRange;
		foreach (Node node in GetTree().GetNodesInGroup(Groups.Enemies))
		{
			if (node is Node2D enemy && !enemy.IsQueuedForDeletion()
				&& origin.DistanceSquaredTo(enemy.GlobalPosition) <= limit)
			{
				found.Add(enemy);
			}
		}

		found.Sort((a, b) => origin.DistanceSquaredTo(a.GlobalPosition).CompareTo(origin.DistanceSquaredTo(b.GlobalPosition)));
		if (found.Count > count)
		{
			found.RemoveRange(count, found.Count - count);
		}

		return found;
	}
}
