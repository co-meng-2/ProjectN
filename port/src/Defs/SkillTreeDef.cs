using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.Defs;

/// <summary>
/// The ordered skill-tree nodes. Order is display order and dependency order: a node may only require nodes listed
/// before it, so the tree can never contain a cycle.
/// </summary>
public partial class SkillTreeDef : Resource
{
	[Export]
	public Godot.Collections.Array<SkillNodeDef> Nodes { get; set; } = [];

	/// <summary>Index of the node with this id, or -1.</summary>
	public int IndexOf(StringName id)
	{
		for (int i = 0; i < Nodes.Count; i++)
		{
			if (Nodes[i] is not null && Nodes[i].Id == id)
			{
				return i;
			}
		}

		return -1;
	}

	public SkillNodeDef? NodeById(StringName id)
	{
		int index = IndexOf(id);
		return index >= 0 ? Nodes[index] : null;
	}

	public List<string> Validate()
	{
		var errors = new List<string>();
		if (Nodes.Count == 0)
		{
			errors.Add("SkillTreeDef: no nodes");
		}

		var earlier = new HashSet<StringName>();
		foreach (SkillNodeDef? node in Nodes)
		{
			if (node is null)
			{
				errors.Add("SkillTreeDef: null node");
				continue;
			}

			if (earlier.Contains(node.Id))
			{
				errors.Add($"SkillTreeDef: duplicate node id '{node.Id}'");
			}

			foreach (StringName required in node.Requires)
			{
				if (!earlier.Contains(required))
				{
					errors.Add($"SkillTreeDef: node '{node.Id}' requires '{required}', which is not listed before it");
				}
			}

			errors.AddRange(node.Validate());
			earlier.Add(node.Id);
		}

		return errors;
	}
}
