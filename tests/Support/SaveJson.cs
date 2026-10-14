using System.Text.Json.Nodes;

namespace VampireSurvivor.Tests.Support;

/// <summary>JSON literals for save tests: parse a string, compare nodes by content.</summary>
public static class SaveJson
{
	public static JsonObject Object(string json) => JsonNode.Parse(json)!.AsObject();

	public static void Eq(JsonNode? actual, string expectedJson, string message = "")
	{
		JsonNode? expected = JsonNode.Parse(expectedJson);
		Assert.True(JsonNode.DeepEquals(actual, expected), $"{message} (expected {expected?.ToJsonString()}, got {actual?.ToJsonString() ?? "null"})");
	}
}
