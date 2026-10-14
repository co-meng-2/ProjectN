using System.Collections.Generic;
using Godot;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Architecture;

public class LayerRulesTest : TestCase
{
	private const string SourceRoot = "res://port/src";

	public void TestDownwardUsingsAreAllowed()
	{
		const string source = "using VampireSurvivor.Core;\nusing VampireSurvivor.Gas;\n";
		Assert.NoErrors(LayerRules.FindViolations("Components/Health.cs", source));
	}

	public void TestUpwardUsingsAreViolations()
	{
		const string source = "using VampireSurvivor.Actors;\n";
		IReadOnlyList<string> violations = LayerRules.FindViolations("Components/Health.cs", source);
		Assert.Eq(violations.Count, 1);
		Assert.True(violations[0].Contains("Components", System.StringComparison.Ordinal)
			&& violations[0].Contains("Actors", System.StringComparison.Ordinal));
	}

	public void TestOnlyStageFileMayUseAbilitiesAndBoss()
	{
		const string source = "using VampireSurvivor.Abilities;\nusing VampireSurvivor.Boss;\n";
		Assert.NoErrors(LayerRules.FindViolations("Stage/StageRoot.cs", source));
		Assert.Eq(LayerRules.FindViolations("Stage/WaveDirector.cs", source).Count, 2);
	}

	public void TestStageNeverUsesEvents()
	{
		Assert.Eq(LayerRules.FindViolations("Stage/StageRoot.cs", "using VampireSurvivor.Events;\n").Count, 1);
	}

	public void TestAppAndMetaMayUseEachOther()
	{
		Assert.NoErrors(LayerRules.FindViolations("App/Main.cs", "using VampireSurvivor.Meta;\n"));
		Assert.NoErrors(LayerRules.FindViolations("Meta/Hub/Hub.cs", "using VampireSurvivor.App;\n"));
	}

	public void TestFullyQualifiedProjectNamesAreViolations()
	{
		const string source = "namespace VampireSurvivor.Components;\nclass A { VampireSurvivor.Core.Wallet W = new(); }\n";
		Assert.Eq(LayerRules.FindViolations("Components/A.cs", source).Count, 1);
	}

	public void TestNonProjectUsingsAreIgnored()
	{
		const string source = "using System;\nusing Godot;\nusing Gamesmiths.Forge.Effects;\n";
		Assert.NoErrors(LayerRules.FindViolations("Core/X.cs", source));
	}

	public void TestSourceTreeHasNoViolations()
	{
		var all = new List<string>();
		foreach (string path in SourceTree.Files(SourceRoot, ".cs"))
		{
			string relative = path[(SourceRoot.Length + 1)..];
			all.AddRange(LayerRules.FindViolations(relative, FileAccess.GetFileAsString(path)));
		}

		Assert.NoErrors(all);
	}
}
