using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Architecture;

/// <summary>
/// Godot has ONE global class namespace. While the GDScript tree (src/) and the C# tree (port/) coexist, a C#
/// [GlobalClass] with the same name as a GDScript class_name replaces it in the global class cache and breaks the
/// GDScript regression suite. At cutover the GDScript tree is deleted and this test goes with it.
/// </summary>
public partial class GlobalClassCollisionTest : TestCase
{
	public void TestFindsGdScriptClassNames()
	{
		const string source = "class_name Wallet\nextends RefCounted\n# class_name Commented\nvar x := 1\n";
		Assert.SequenceEq(GdScriptClassNames(source), new[] { "Wallet" });
	}

	public void TestFindsCSharpGlobalClassNames()
	{
		const string source = "[Tool]\n[GlobalClass]\npublic abstract partial class A : Resource {}\n"
			+ "[GlobalClass]\npublic sealed partial class B : Resource {}\npublic partial class NotGlobal : Node {}\n";
		Assert.SequenceEq(CSharpGlobalClassNames(source), new[] { "A", "B" });
	}

	public void TestPortGlobalClassesDoNotShadowGdScriptClassNames()
	{
		var legacy = new HashSet<string>();
		foreach (string path in SourceTree.Files("res://src", ".gd"))
		{
			legacy.UnionWith(GdScriptClassNames(FileAccess.GetFileAsString(path)));
		}

		var collisions = new List<string>();
		foreach (string path in SourceTree.Files("res://port/src", ".cs"))
		{
			foreach (string name in CSharpGlobalClassNames(FileAccess.GetFileAsString(path)))
			{
				if (legacy.Contains(name))
				{
					collisions.Add($"{path}: [GlobalClass] {name} shadows the GDScript class_name {name}");
				}
			}
		}

		Assert.NoErrors(collisions);
	}

	private static IEnumerable<string> GdScriptClassNames(string source)
	{
		return GdClassPattern().Matches(source).Select(m => m.Groups[1].Value);
	}

	private static IEnumerable<string> CSharpGlobalClassNames(string source)
	{
		return GlobalClassPattern().Matches(source).Select(m => m.Groups[1].Value);
	}

	[GeneratedRegex(@"^class_name\s+(\w+)", RegexOptions.Multiline)]
	private static partial Regex GdClassPattern();

	[GeneratedRegex(@"\[GlobalClass\]\s*(?:\[[^\]]+\]\s*)*(?:public\s+|internal\s+)?(?:sealed\s+|abstract\s+|static\s+)*partial\s+class\s+(\w+)")]
	private static partial Regex GlobalClassPattern();
}
