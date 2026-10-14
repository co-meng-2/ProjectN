using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VampireSurvivor.Meta;

/// <summary>Everything that persists between sessions. Serialised by SaveStore; migrated by SaveMigrations.</summary>
public sealed class SaveData
{
	/// <summary>The only persistent currency.</summary>
	public long Gem { get; set; }

	/// <summary>
	/// Ids of stages the player has cleared. Unlock state is derived from this (StageProgress); ids no longer in the
	/// catalog are kept so progress survives content changes.
	/// </summary>
	public List<string> ClearedStages { get; } = [];

	/// <summary>Purchased skill levels by node id. Ids no longer in the tree are kept so investments survive content changes.</summary>
	public Dictionary<string, int> SkillLevels { get; } = [];

	public SettingsData Settings { get; set; } = new();

	public JsonObject ToJson()
	{
		var cleared = new JsonArray();
		foreach (string id in ClearedStages)
		{
			cleared.Add(id);
		}

		var levels = new JsonObject();
		foreach (KeyValuePair<string, int> entry in SkillLevels)
		{
			levels[entry.Key] = entry.Value;
		}

		return new JsonObject
		{
			["version"] = SaveMigrations.CurrentVersion,
			["gem"] = Gem,
			["cleared_stages"] = cleared,
			["skill_levels"] = levels,
			["settings"] = Settings.ToJson(),
		};
	}

	public void MarkCleared(string id)
	{
		if (!ClearedStages.Contains(id))
		{
			ClearedStages.Add(id);
		}
	}

	public int LevelOf(string id) => SkillLevels.GetValueOrDefault(id);

	/// <summary>A level &lt;= 0 removes the entry.</summary>
	public void SetLevel(string id, int level)
	{
		if (level <= 0)
		{
			SkillLevels.Remove(id);
		}
		else
		{
			SkillLevels[id] = level;
		}
	}

	/// <summary>Deducts gem all-or-nothing; false (and no change) when the amount is negative or exceeds the balance.</summary>
	public bool SpendGem(long amount)
	{
		if (amount < 0 || Gem < amount)
		{
			return false;
		}

		Gem -= amount;
		return true;
	}

	/// <summary><paramref name="source"/> must already be migrated. Missing fields keep defaults; null when a present field is invalid.</summary>
	public static SaveData? FromJson(JsonObject source)
	{
		var result = new SaveData();
		if (source.TryGetPropertyValue("gem", out JsonNode? gem))
		{
			long? value = SaveMigrations.ReadInt(gem);
			if (value is null or < 0)
			{
				return null;
			}

			result.Gem = value.Value;
		}

		if (source.TryGetPropertyValue("cleared_stages", out JsonNode? cleared))
		{
			if (cleared is not JsonArray ids)
			{
				return null;
			}

			foreach (JsonNode? id in ids)
			{
				if (id is not JsonValue text || text.GetValueKind() != JsonValueKind.String)
				{
					return null;
				}

				result.MarkCleared(text.GetValue<string>());
			}
		}

		if (source.TryGetPropertyValue("skill_levels", out JsonNode? levels))
		{
			if (levels is not JsonObject entries)
			{
				return null;
			}

			foreach (KeyValuePair<string, JsonNode?> entry in entries)
			{
				long? level = SaveMigrations.ReadInt(entry.Value);
				if (level is null or < 0 or > int.MaxValue)
				{
					return null;
				}

				result.SetLevel(entry.Key, (int)level.Value);
			}
		}

		if (source.TryGetPropertyValue("settings", out JsonNode? settings))
		{
			SettingsData? parsed = settings is JsonObject settingsObject ? SettingsData.FromJson(settingsObject) : null;
			if (parsed is null)
			{
				return null;
			}

			result.Settings = parsed;
		}

		return result;
	}
}
