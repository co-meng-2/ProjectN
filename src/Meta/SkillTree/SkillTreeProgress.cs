using VampireSurvivor.Defs;

namespace VampireSurvivor.Meta.SkillTree;

public enum SkillStatus
{
	Locked,
	Available,
	Maxed,
}

/// <summary>
/// Which skill nodes are locked, available or maxed, and the purchase rule. Derived from the tree and the save; it
/// stores nothing itself (levels and gem live in SaveData).
/// </summary>
public sealed class SkillTreeProgress(SkillTreeDef tree, SaveData save)
{
	public int Level(int index) => InRange(index) ? save.LevelOf(tree.Nodes[index].Id) : 0;

	/// <summary>Maxed at max level (a saved level above the max counts); else Available when every required node has level >= 1; else Locked.</summary>
	public SkillStatus Status(int index)
	{
		if (!InRange(index))
		{
			return SkillStatus.Locked;
		}

		SkillNodeDef node = tree.Nodes[index];
		if (save.LevelOf(node.Id) >= node.MaxLevel)
		{
			return SkillStatus.Maxed;
		}

		foreach (var required in node.Requires)
		{
			if (save.LevelOf(required) < 1)
			{
				return SkillStatus.Locked;
			}
		}

		return SkillStatus.Available;
	}

	/// <summary>Gem cost of the next level, or -1 when maxed, out of range, or the costs list is shorter than the level.</summary>
	public int NextCost(int index)
	{
		if (!InRange(index))
		{
			return -1;
		}

		SkillNodeDef node = tree.Nodes[index];
		int current = save.LevelOf(node.Id);
		return current >= node.MaxLevel || current >= node.Costs.Length ? -1 : node.Costs[current];
	}

	public bool CanBuy(int index) => Status(index) == SkillStatus.Available && NextCost(index) >= 0 && save.Gem >= NextCost(index);

	/// <summary>All-or-nothing: deducts the gem and raises the level together, or changes nothing.</summary>
	public bool Buy(int index)
	{
		if (!CanBuy(index))
		{
			return false;
		}

		SkillNodeDef node = tree.Nodes[index];
		int current = save.LevelOf(node.Id);
		if (!save.SpendGem(NextCost(index)))
		{
			return false;
		}

		save.SetLevel(node.Id, current + 1);
		return true;
	}

	private bool InRange(int index) => index >= 0 && index < tree.Nodes.Count && tree.Nodes[index] is not null;
}
