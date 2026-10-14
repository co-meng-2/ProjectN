using System;
using System.Collections.Generic;
using Godot;
using VampireSurvivor.App;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.App;

public class SceneRouterTest : TestCase
{
	private SceneRouterProbe Probe() => Adopt(new SceneRouterProbe());

	public void TestGoChangesToTheRouteScene()
	{
		SceneRouterProbe router = Probe();
		Assert.True(router.Go(SceneRouter.Route.MainMenu));
		Assert.SequenceEq(router.ChangedTo, new[] { SceneRouter.Routes[SceneRouter.Route.MainMenu] });
	}

	public void TestParamsAreHandedOverExactlyOnce()
	{
		SceneRouterProbe router = Probe();
		router.Go(SceneRouter.Route.Stage, new() { ["a"] = 1 });
		Assert.Eq(router.TakeParams()["a"], (object)1);
		Assert.Eq(router.TakeParams().Count, 0, "second read is empty");
	}

	public void TestANewGoReplacesUnreadParams()
	{
		SceneRouterProbe router = Probe();
		router.Go(SceneRouter.Route.Stage, new() { ["a"] = 1 });
		router.Go(SceneRouter.Route.MainMenu);
		Assert.Eq(router.TakeParams().Count, 0);
	}

	public void TestUnknownRouteChangesNothingAndKeepsParams()
	{
		SceneRouterProbe router = Probe();
		router.Go(SceneRouter.Route.Stage, new() { ["keep"] = true });
		Assert.False(router.Go((SceneRouter.Route)999, new() { ["x"] = 1 }));
		Assert.Eq(router.ChangedTo.Count, 1, "no second scene change");
		Assert.Eq(router.TakeParams()["keep"], (object)true);
	}

	public void TestFailedSceneChangeReportsFalseAndKeepsPreviousParams()
	{
		SceneRouterProbe router = Probe();
		router.Go(SceneRouter.Route.Stage, new() { ["keep"] = true });
		router.NextError = Error.CantOpen;
		Assert.False(router.Go(SceneRouter.Route.MainMenu, new() { ["lost"] = true }));
		Dictionary<string, object> taken = router.TakeParams();
		Assert.Eq(taken.Count, 1);
		Assert.Eq(taken["keep"], (object)true);
	}

	public void TestPathForAnUnknownRouteIsEmpty() => Assert.Eq(Probe().PathFor((SceneRouter.Route)999), string.Empty);

	public void TestTheStageParamKeyIsAStableName() => Assert.Eq(SceneRouter.ParamStageDef, "stage_def");

	public void TestEveryRouteHasAPath()
	{
		Assert.Eq(SceneRouter.Routes.Count, Enum.GetValues<SceneRouter.Route>().Length, "every Route value has a path");
	}
}
