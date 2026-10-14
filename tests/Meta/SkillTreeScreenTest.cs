using Godot;
using VampireSurvivor.App;
using VampireSurvivor.Defs;
using VampireSurvivor.Meta;
using VampireSurvivor.Meta.SkillTree;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Meta;

/// <summary>a: 2 levels costs 10/20. b: 1 level cost 5, requires a.</summary>
public class SkillTreeScreenTest : TestCase
{
	private const string Dir = "user://test_skill_tree_screen";
	private const string Path = Dir + "/save.json";

	public override void BeforeEach() => SaveFiles.Reset(Dir);

	public override void AfterEach() => SaveFiles.Reset(Dir);

	private SaveService Service(long gem = 0)
	{
		SaveService service = SaveServiceTest.Service(this, Path);
		service.Data.Gem = gem;
		return service;
	}

	private static SkillTreeDef Tree() => DefFactory.Tree(DefFactory.SkillNode("a"), DefFactory.SkillNode("b", ["a"], 1, [5]));

	private SkillTreeScreen Screen(SkillTreeDef? tree, SaveService service, SceneRouterProbe? router = null)
	{
		var screen = GD.Load<PackedScene>(SceneRouter.Routes[SceneRouter.Route.SkillTree]).Instantiate<SkillTreeScreen>();
		screen.Tree = tree;
		screen.SaveService = service;
		screen.Router = router ?? Adopt(new SceneRouterProbe());
		return Adopt(screen);
	}

	private static SkillNodeRow Row(SkillTreeScreen screen, int index) => screen.NodeList.GetChild<SkillNodeRow>(index);

	private static Button Buy(SkillTreeScreen screen, int index) => Row(screen, index).BuyButton;

	private static Control? FocusOwner(Control screen) => screen.GetViewport().GuiGetFocusOwner();

	public void TestOneRowPerNodeAndTheGemBalance()
	{
		SkillTreeScreen screen = Screen(Tree(), Service(25));
		Assert.Eq(screen.NodeList.GetChildCount(), 2);
		Assert.True(screen.GemLabel.Text.Contains("25"));
	}

	public void TestRowStatesForAvailableLockedAndUnaffordable()
	{
		SkillTreeScreen screen = Screen(Tree(), Service(10));
		Assert.False(Buy(screen, 0).Disabled, "a is affordable at 10");
		Assert.True(Buy(screen, 1).Disabled, "b is locked");
		Assert.True(Row(screen, 1).DetailLabel.Text.Contains("Requires"));
		SkillTreeScreen poor = Screen(Tree(), Service(9));
		Assert.True(Buy(poor, 0).Disabled, "a is not affordable at 9");
	}

	public void TestBuyingUpdatesGemLevelChildrenAndSaves()
	{
		SaveService service = Service(25);
		SkillTreeScreen screen = Screen(Tree(), service);
		Buy(screen, 0).EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Eq(service.Data.Gem, 15L);
		Assert.Eq(service.Data.LevelOf("a"), 1);
		Assert.True(screen.GemLabel.Text.Contains("15"));
		Assert.False(Buy(screen, 1).Disabled, "b is now open and affordable");
		Assert.Eq(SaveStore.LoadOrDefault(Path).LevelOf("a"), 1, "persisted to disk");
	}

	public void TestAMaxedNodeCannotBeBoughtAgain()
	{
		SaveService service = Service(100);
		service.Data.SetLevel("a", 2);
		SkillTreeScreen screen = Screen(Tree(), service);
		Assert.True(Buy(screen, 0).Disabled);
		Assert.True(Row(screen, 0).DetailLabel.Text.Contains("Max"));
	}

	public void TestTheHandlerRefusesLockedUnaffordableAndMaxedNodes()
	{
		SaveService service = Service(1000);
		Screen(Tree(), service).OnBuyPressed(1);
		Assert.Eq(service.Data.Gem, 1000L, "locked b");
		Assert.Eq(service.Data.LevelOf("b"), 0);
		SaveService poor = Service(3);
		Screen(Tree(), poor).OnBuyPressed(0);
		Assert.Eq(poor.Data.Gem, 3L, "short on gem");
		Assert.Eq(poor.Data.LevelOf("a"), 0);
	}

	public void TestBackReturnsToTheHub()
	{
		SceneRouterProbe router = Adopt(new SceneRouterProbe());
		Screen(Tree(), Service(), router).BackButton.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Eq(router.LastRoute, SceneRouter.Route.Hub);
	}

	public void TestAnInvalidTreeShowsAnEmptyStateAndBackStillWorks()
	{
		SceneRouterProbe router = Adopt(new SceneRouterProbe());
		SkillTreeScreen screen = Screen(new SkillTreeDef(), Service(), router);
		Assert.Eq(screen.NodeList.GetChildCount(), 0);
		Assert.True(screen.EmptyLabel.Visible);
		screen.BackButton.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Eq(router.LastRoute, SceneRouter.Route.Hub);
	}

	public void TestFocusStartsOnTheFirstBuyableRowElseBack()
	{
		SkillTreeScreen screen = Screen(Tree(), Service(10));
		Assert.True(FocusOwner(screen) == Buy(screen, 0));
		SkillTreeScreen poor = Screen(Tree(), Service(0));
		Assert.True(FocusOwner(poor) == poor.BackButton);
	}

	public void TestTheShippedTreeIsTheDefault() => Assert.Eq(Screen(null, Service()).NodeList.GetChildCount(), 4);

	public void TestFocusStaysOnTheRowJustBoughtSoEnterLevelsItAgain()
	{
		SkillTreeScreen screen = Screen(DefFactory.Tree(DefFactory.SkillNode("first"), DefFactory.SkillNode("second")), Service(100));
		Assert.True(FocusOwner(screen) == Buy(screen, 0), "starts on the first buyable row");
		Buy(screen, 1).EmitSignal(BaseButton.SignalName.Pressed);
		Assert.True(FocusOwner(screen) == Buy(screen, 1), "stays on the bought row, not row 0");
	}

	public void TestFocusFallsBackWhenTheBoughtRowCanNoLongerBeBought()
	{
		SkillTreeScreen screen = Screen(DefFactory.Tree(DefFactory.SkillNode("first"), DefFactory.SkillNode("second", null, 1, [10])), Service(100));
		Buy(screen, 1).EmitSignal(BaseButton.SignalName.Pressed);
		Assert.True(Buy(screen, 1).Disabled, "second is maxed at one level");
		Assert.True(FocusOwner(screen) == Buy(screen, 0), "falls back to the first buyable row");
	}
}
