using System.Collections.Generic;
using Godot;
using VampireSurvivor.App;

namespace VampireSurvivor.Tests.Support;

/// <summary>Test double: records scene changes instead of performing them.</summary>
public partial class SceneRouterProbe : SceneRouter
{
	public List<string> ChangedTo { get; } = [];

	public Error NextError { get; set; } = Error.Ok;

	/// <summary>The route of the last change, by path.</summary>
	public Route? LastRoute => ChangedTo.Count == 0 ? null : RouteOf(ChangedTo[^1]);

	public static Route? RouteOf(string path)
	{
		foreach (KeyValuePair<Route, string> entry in Routes)
		{
			if (entry.Value == path)
			{
				return entry.Key;
			}
		}

		return null;
	}

	protected override Error ChangeScene(string path)
	{
		ChangedTo.Add(path);
		return NextError;
	}
}
