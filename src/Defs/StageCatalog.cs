using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.Defs;

/// <summary>The ordered list of stages. Order is unlock order: a stage opens once the one before it is cleared.</summary>
[GlobalClass]
public partial class StageCatalog : Resource
{
	[Export]
	public Godot.Collections.Array<StageDef> Stages { get; set; } = [];

	/// <summary>Index of the stage with this id, or -1.</summary>
	public int IndexOf(StringName id)
	{
		for (int i = 0; i < Stages.Count; i++)
		{
			if (Stages[i] is not null && Stages[i].Id == id)
			{
				return i;
			}
		}

		return -1;
	}

	public List<string> Validate()
	{
		var errors = new List<string>();
		if (Stages.Count == 0)
		{
			errors.Add("StageCatalog: no stages");
		}

		var seen = new HashSet<StringName>();
		foreach (StageDef? stage in Stages)
		{
			if (stage is null)
			{
				errors.Add("StageCatalog: null stage");
				continue;
			}

			if (!seen.Add(stage.Id))
			{
				errors.Add($"StageCatalog: duplicate stage id '{stage.Id}'");
			}

			errors.AddRange(stage.Validate());
		}

		return errors;
	}
}
