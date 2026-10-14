using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VampireSurvivor.Meta;

/// <summary>
/// Brings a loaded save object up to the current schema one version at a time.
/// To change the schema: bump <see cref="CurrentVersion"/> and append one step to <see cref="Steps"/>.
/// </summary>
public static class SaveMigrations
{
	public const int CurrentVersion = 3;

	/// <summary>JSON numbers are doubles; anything beyond this is not a plausible whole count.</summary>
	public const double MaxSafeInt = 9e15;

	/// <summary>Steps()[i] upgrades a version (i + 1) object to version (i + 2).</summary>
	public static IReadOnlyList<Func<JsonObject, JsonObject>> Steps() => [V1ToV2, V2ToV3];

	/// <summary>The migrated copy, or null when the version is missing, invalid or from the future.</summary>
	public static JsonObject? Migrate(JsonObject source) => MigrateWith(source, Steps(), CurrentVersion);

	public static JsonObject? MigrateWith(JsonObject source, IReadOnlyList<Func<JsonObject, JsonObject>> steps, int current)
	{
		long? version = ReadInt(source["version"]);
		if (version is null || version < 1 || version > current)
		{
			return null;
		}

		JsonObject result = source.DeepClone().AsObject();
		for (int at = (int)version; at < current; at++)
		{
			result = steps[at - 1](result);
		}

		result["version"] = current;
		return result;
	}

	/// <summary>A whole JSON number as a long, else null. JSON has no int type, so 5 may come back as 5.0.</summary>
	public static long? ReadInt(JsonNode? raw)
	{
		if (ReadNumber(raw) is not double number || Math.Abs(number) >= MaxSafeInt || number != Math.Round(number))
		{
			return null;
		}

		return (long)number;
	}

	/// <summary>
	/// A JSON number as a double, else null. Read through its JSON text: a node built in code holds an int, long or float,
	/// which <c>GetValue&lt;double&gt;</c> refuses to convert.
	/// </summary>
	public static double? ReadNumber(JsonNode? raw)
	{
		if (raw is not JsonValue value || value.GetValueKind() != JsonValueKind.Number)
		{
			return null;
		}

		return double.Parse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture);
	}

	/// <summary>v2 adds the list of cleared stage ids.</summary>
	private static JsonObject V1ToV2(JsonObject source)
	{
		source["cleared_stages"] = new JsonArray();
		return source;
	}

	/// <summary>v3 adds the purchased skill levels.</summary>
	private static JsonObject V2ToV3(JsonObject source)
	{
		source["skill_levels"] = new JsonObject();
		return source;
	}
}
