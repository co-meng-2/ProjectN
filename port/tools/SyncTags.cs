using Gamesmiths.Forge.Godot.Core;
using Godot;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Tools;

/// <summary>
/// Rebuilds addons/forge/core/forge_data.tres from GameplayTags.All (with parents).
/// Run: Godot --headless --path . res://port/tools/sync_tags.tscn
/// </summary>
public partial class SyncTags : Node
{
	public override void _Ready()
	{
		var tags = new Godot.Collections.Array<string>(GameplayTags.WithParents(GameplayTags.All));
		var data = new ForgeData { RegisteredTags = tags };
		Error error = ResourceSaver.Save(data, ForgeData.ForgeDataResourcePath);
		GD.Print($"sync_tags: {tags.Count} tags -> {error}");
		GetTree().Quit(error == Error.Ok ? 0 : 1);
	}
}
