using Godot;

namespace VampireSurvivor.Tests.Support;

/// <summary>Scratch save files under user:// for save tests.</summary>
public static class SaveFiles
{
	/// <summary>Deletes every file in <paramref name="dir"/> (not recursive) and makes sure the directory exists.</summary>
	public static void Reset(string dir)
	{
		DirAccess.MakeDirRecursiveAbsolute(dir);
		foreach (string file in DirAccess.GetFilesAt(dir))
		{
			DirAccess.RemoveAbsolute($"{dir}/{file}");
		}
	}

	public static void WriteText(string path, string text)
	{
		using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
		file.StoreString(text);
	}
}
