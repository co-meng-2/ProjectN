using Godot;
using VampireSurvivor.Meta;

namespace VampireSurvivor.App;

/// <summary>
/// The only place that looks up autoloads by path. Screens call these from <c>_Ready</c> when no collaborator was
/// injected (tests inject their own).
/// </summary>
public static class Autoloads
{
	public static SceneRouter Router(Node from) => from.GetNode<SceneRouter>("/root/SceneRouter");

	public static SaveService Save(Node from) => from.GetNode<SaveService>("/root/SaveService");
}
