using System;
using Godot;
using VampireSurvivor.App;

namespace VampireSurvivor.Meta;

/// <summary>
/// Autoload "SaveService". Holds the session's SaveData and persists it. Screens request changes here and never keep
/// save state of their own.
/// </summary>
public partial class SaveService : Node
{
	public const string SavePath = "user://save.json";

	public event Action? Changed;

	public SaveData Data { get; private set; } = new();

	/// <summary>Overridable so tests can point at a scratch file; set before the node enters the tree.</summary>
	public string Path { get; set; } = SavePath;

	public override void _Ready() => Reload();

	public void Reload()
	{
		Data = SaveStore.LoadOrDefault(Path);
		AudioSettings.Apply(Data.Settings);
		Changed?.Invoke();
	}

	public void AddGem(long amount)
	{
		if (amount < 0)
		{
			GD.PushError($"SaveService: AddGem({amount}) ignored, amount must be >= 0");
			return;
		}

		Data.Gem += amount;
		Changed?.Invoke();
	}

	/// <summary>Marks a stage as cleared. The caller saves (Main does so once, together with the gem credit).</summary>
	public void RecordClear(string stageId)
	{
		Data.MarkCleared(stageId);
		Changed?.Invoke();
	}

	public bool Save() => SaveStore.Write(Path, Data);
}
