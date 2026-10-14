using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

namespace VampireSurvivor.Meta;

/// <summary>
/// Reads and writes the save file. Never loses the player's data: writes are atomic and unreadable files are moved
/// aside rather than overwritten.
/// </summary>
public static class SaveStore
{
	private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

	/// <summary>Always returns a usable SaveData: defaults when the file is absent or unreadable.</summary>
	public static SaveData LoadOrDefault(string path)
	{
		if (!FileAccess.FileExists(path))
		{
			return new SaveData();
		}

		if (Parse(FileAccess.GetFileAsString(path)) is JsonObject source
			&& SaveMigrations.Migrate(source) is JsonObject migrated
			&& SaveData.FromJson(migrated) is SaveData data)
		{
			return data;
		}

		Quarantine(path);
		return new SaveData();
	}

	/// <summary>Writes <c>&lt;path&gt;.tmp</c> then renames it over <paramref name="path"/>, so a crash cannot leave a half-written save.</summary>
	public static bool Write(string path, SaveData data)
	{
		string tempPath = path + ".tmp";
		using (FileAccess? file = FileAccess.Open(tempPath, FileAccess.ModeFlags.Write))
		{
			if (file is null)
			{
				GD.PushError($"SaveStore: cannot open '{tempPath}' for writing (error {FileAccess.GetOpenError()})");
				return false;
			}

			file.StoreString(data.ToJson().ToJsonString(Indented));
		}

		Error error = DirAccess.RenameAbsolute(tempPath, path);
		if (error != Error.Ok)
		{
			GD.PushError($"SaveStore: cannot move '{tempPath}' over '{path}' (error {error})");
			return false;
		}

		return true;
	}

	/// <summary>The top-level object, or null for anything else (bad JSON, another JSON type, duplicate keys).</summary>
	private static JsonObject? Parse(string text)
	{
		try
		{
			if (JsonNode.Parse(text) is not JsonObject source)
			{
				return null;
			}

			_ = source.Count; // JsonObject reads its properties lazily; duplicate keys throw here, not later
			return source;
		}
		catch (Exception exception) when (exception is JsonException or ArgumentException)
		{
			return null;
		}
	}

	private static void Quarantine(string path)
	{
		string backup = path + ".bak";
		if (FileAccess.FileExists(backup))
		{
			DirAccess.RemoveAbsolute(backup);
		}

		Error error = DirAccess.RenameAbsolute(path, backup);
		GD.PushWarning($"SaveStore: '{path}' is unreadable; kept as '{backup}' and starting from defaults (rename error {error})");
	}
}
