using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.App;

/// <summary>
/// Autoload "SceneRouter". The only place that knows scene paths. Screens ask for a route and hand the next screen
/// one-shot params.
/// </summary>
public partial class SceneRouter : Node
{
	public enum Route
	{
		Logo,
		MainMenu,
		Settings,
		Stage,
		Hub,
		SkillTree,
	}

	/// <summary>Param keys screens agree on, so no screen hard-codes another's strings.</summary>
	public const string ParamStageDef = "stage_def";

	public static readonly IReadOnlyDictionary<Route, string> Routes = new Dictionary<Route, string>
	{
		[Route.Logo] = "res://port/src/App/logo.tscn",
		[Route.MainMenu] = "res://port/src/Meta/Menu/main_menu.tscn",
		[Route.Settings] = "res://port/src/Meta/Menu/settings_screen.tscn",
		[Route.Stage] = "res://port/src/App/main.tscn",
		[Route.Hub] = "res://port/src/Meta/Hub/hub.tscn",
		[Route.SkillTree] = "res://port/src/Meta/SkillTree/skill_tree_screen.tscn",
	};

	private Dictionary<string, object> _params = [];

	public string PathFor(Route route) => Routes.GetValueOrDefault(route, string.Empty);

	/// <summary>
	/// Changes to the route's scene; <paramref name="parameters"/> become readable once through <see cref="TakeParams"/>
	/// by the next screen. False (and the current screen and params left alone) for an unknown route or a failed change.
	/// </summary>
	public bool Go(Route route, Dictionary<string, object>? parameters = null)
	{
		string path = PathFor(route);
		if (path.Length == 0)
		{
			GD.PushError($"SceneRouter: unknown route {route}");
			return false;
		}

		Dictionary<string, object> previous = _params;
		_params = parameters ?? [];
		Error error = ChangeScene(path);
		if (error != Error.Ok)
		{
			_params = previous;
			GD.PushError($"SceneRouter: cannot change to '{path}' (error {error})");
			return false;
		}

		return true;
	}

	public Dictionary<string, object> TakeParams()
	{
		Dictionary<string, object> taken = _params;
		_params = [];
		return taken;
	}

	protected virtual Error ChangeScene(string path) => GetTree().ChangeSceneToFile(path);
}
