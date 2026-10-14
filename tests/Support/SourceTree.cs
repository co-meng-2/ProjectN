using System;
using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.Tests.Support;

/// <summary>Lists source files under a res:// directory (tests that read the source tree share this).</summary>
public static class SourceTree
{
	public static IEnumerable<string> Files(string directory, string extension)
	{
		DirAccess? access = DirAccess.Open(directory);
		if (access is null)
		{
			yield break;
		}

		foreach (string sub in access.GetDirectories())
		{
			foreach (string file in Files($"{directory}/{sub}", extension))
			{
				yield return file;
			}
		}

		foreach (string file in access.GetFiles())
		{
			if (file.EndsWith(extension, StringComparison.Ordinal))
			{
				yield return $"{directory}/{file}";
			}
		}
	}
}
