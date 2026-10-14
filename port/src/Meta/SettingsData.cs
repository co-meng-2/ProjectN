using System;
using System.Text.Json.Nodes;

namespace VampireSurvivor.Meta;

/// <summary>Player-adjustable options that persist across runs.</summary>
public sealed class SettingsData
{
	public const float DefaultMasterVolume = 1f;

	private float _masterVolume = DefaultMasterVolume;

	/// <summary>Linear 0..1; the setter clamps.</summary>
	public float MasterVolume
	{
		get => _masterVolume;
		set => _masterVolume = Math.Clamp(value, 0f, 1f);
	}

	public JsonObject ToJson() => new() { ["master_volume"] = MasterVolume };

	/// <summary>Missing keys keep their defaults; null when a present key has the wrong type.</summary>
	public static SettingsData? FromJson(JsonObject source)
	{
		var result = new SettingsData();
		if (source.TryGetPropertyValue("master_volume", out JsonNode? volume))
		{
			if (SaveMigrations.ReadNumber(volume) is not double value)
			{
				return null;
			}

			result.MasterVolume = (float)value;
		}

		return result;
	}
}
