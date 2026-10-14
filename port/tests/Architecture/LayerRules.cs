using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VampireSurvivor.Tests.Architecture;

/// <summary>
/// The layer table as code. A file may only use layers with a lower rank; the few allowed exceptions are listed
/// explicitly. Reads <c>using VampireSurvivor.X;</c> lines, so it needs no extra dependency.
/// </summary>
public static partial class LayerRules
{
	private static readonly Dictionary<string, int> Rank = new()
	{
		["Core"] = 0,
		["Defs"] = 1,
		["Gas"] = 2,
		["Components"] = 3,
		["Actors"] = 4,
		["Stage"] = 5,
		["Abilities"] = 5,
		["Boss"] = 5,
		["Events"] = 6,
		["Ui"] = 7,
		["App"] = 8,
		["Meta"] = 8,
	};

	public static IReadOnlyList<string> FindViolations(string relativePath, string source)
	{
		var violations = new List<string>();
		string ownLayer = relativePath.Split('/')[0];
		if (!Rank.TryGetValue(ownLayer, out int ownRank))
		{
			return violations;
		}

		foreach (Match match in UsingPattern().Matches(source))
		{
			string target = match.Groups[1].Value;
			if (!Rank.TryGetValue(target, out int targetRank) || target == ownLayer)
			{
				continue;
			}

			bool allowed = targetRank < ownRank
				|| (relativePath == "Stage/Stage.cs" && (target == "Abilities" || target == "Boss"))
				|| (ownLayer == "App" && target == "Meta")
				|| (ownLayer == "Meta" && target == "App");
			if (!allowed)
			{
				violations.Add($"{relativePath}: layer {ownLayer} must not use {target}");
			}
		}

		foreach (string line in source.Split('\n'))
		{
			string trimmed = line.TrimStart();
			if (trimmed.StartsWith("using ", System.StringComparison.Ordinal)
				|| trimmed.StartsWith("namespace ", System.StringComparison.Ordinal))
			{
				continue;
			}

			if (QualifiedPattern().IsMatch(line))
			{
				violations.Add($"{relativePath}: write a using, not a fully qualified VampireSurvivor name: {trimmed}");
			}
		}

		return violations;
	}

	[GeneratedRegex(@"^\s*using\s+VampireSurvivor\.(\w+)", RegexOptions.Multiline)]
	private static partial Regex UsingPattern();

	[GeneratedRegex(@"\bVampireSurvivor\.\w+\.")]
	private static partial Regex QualifiedPattern();
}
