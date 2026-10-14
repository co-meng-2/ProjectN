using VampireSurvivor.Defs;

namespace VampireSurvivor.Meta;

public enum StageStatus
{
	Locked,
	Available,
	Cleared,
}

/// <summary>Which stages are locked, available or cleared. Derived from the catalog order and the save; stores nothing.</summary>
public sealed class StageProgress(StageCatalog catalog, SaveData save)
{
	/// <summary>Cleared if saved as cleared; else Available if first or the previous stage is cleared; else Locked.</summary>
	public StageStatus Status(int index)
	{
		if (index < 0 || index >= catalog.Stages.Count)
		{
			return StageStatus.Locked;
		}

		if (save.ClearedStages.Contains(catalog.Stages[index].Id))
		{
			return StageStatus.Cleared;
		}

		return index == 0 || save.ClearedStages.Contains(catalog.Stages[index - 1].Id) ? StageStatus.Available : StageStatus.Locked;
	}

	/// <summary>A cleared stage stays playable.</summary>
	public bool IsUnlocked(int index) => Status(index) != StageStatus.Locked;
}
