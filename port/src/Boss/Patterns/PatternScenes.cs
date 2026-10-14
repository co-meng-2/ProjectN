using Godot;

namespace VampireSurvivor.Boss;

/// <summary>Checks the root type of a scene a pattern instantiates, so a wrong scene fails validation instead of mid-fight.</summary>
public static class PatternScenes
{
	public static bool RootIs<T>(PackedScene scene)
		where T : Node
	{
		Node root = scene.Instantiate();
		bool matches = root is T;
		root.Free();
		return matches;
	}
}
