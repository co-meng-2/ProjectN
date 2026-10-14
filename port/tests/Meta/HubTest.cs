using Godot;
using VampireSurvivor.App;
using VampireSurvivor.Defs;
using VampireSurvivor.Meta;
using VampireSurvivor.Meta.Hub;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Meta;

public class HubTest : TestCase
{
	private const string Dir = "user://test_hub";
	private const string Path = Dir + "/save.json";

	public override void BeforeEach() => SaveFiles.Reset(Dir);

	public override void AfterEach() => SaveFiles.Reset(Dir);

	private SaveService Service() => SaveServiceTest.Service(this, Path);

	private static StageCatalog Catalog(int count)
	{
		var catalog = new StageCatalog();
		for (int i = 1; i <= count; i++)
		{
			StageDef stage = DefFactory.Stage();
			stage.Id = $"s{i}";
			stage.DisplayName = $"Stage {i}";
			catalog.Stages.Add(stage);
		}

		return catalog;
	}

	private HubScreen Hub(StageCatalog? catalog, SaveService service, SceneRouterProbe router)
	{
		var hub = GD.Load<PackedScene>(SceneRouter.Routes[SceneRouter.Route.Hub]).Instantiate<HubScreen>();
		hub.Catalog = catalog;
		hub.SaveService = service;
		hub.Router = router;
		return Adopt(hub);
	}

	private static StageCard Card(HubScreen hub, int index) => hub.StageList.GetChild<StageCard>(index);

	private static Control? FocusOwner(HubScreen hub) => hub.GetViewport().GuiGetFocusOwner();

	public void TestOneCardPerStageWithItsName()
	{
		HubScreen hub = Hub(Catalog(3), Service(), Adopt(new SceneRouterProbe()));
		Assert.Eq(hub.StageList.GetChildCount(), 3);
		Assert.Eq(Card(hub, 1).NameLabel.Text, "Stage 2");
	}

	public void TestOnlyTheFirstStageIsPlayableOnAFreshSave()
	{
		HubScreen hub = Hub(Catalog(3), Service(), Adopt(new SceneRouterProbe()));
		Assert.False(Card(hub, 0).PlayButton.Disabled);
		Assert.True(Card(hub, 1).PlayButton.Disabled);
		Assert.True(Card(hub, 2).PlayButton.Disabled);
		Assert.Eq(Card(hub, 1).StatusLabel.Text, "Locked");
	}

	public void TestAClearedStageOpensTheNextAndShowsCleared()
	{
		SaveService service = Service();
		service.RecordClear("s1");
		HubScreen hub = Hub(Catalog(3), service, Adopt(new SceneRouterProbe()));
		Assert.Eq(Card(hub, 0).StatusLabel.Text, "Cleared");
		Assert.False(Card(hub, 0).PlayButton.Disabled, "a cleared stage can be replayed");
		Assert.False(Card(hub, 1).PlayButton.Disabled);
		Assert.True(Card(hub, 2).PlayButton.Disabled);
	}

	public void TestPlayGoesToTheStageWithThatStageDef()
	{
		StageCatalog catalog = Catalog(2);
		SceneRouterProbe router = Adopt(new SceneRouterProbe());
		HubScreen hub = Hub(catalog, Service(), router);
		Card(hub, 0).PlayButton.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Eq(router.LastRoute, SceneRouter.Route.Stage);
		Assert.True(router.TakeParams()[SceneRouter.ParamStageDef] == catalog.Stages[0]);
	}

	public void TestALockedStageDoesNotStartEvenWhenTheHandlerIsCalled()
	{
		SceneRouterProbe router = Adopt(new SceneRouterProbe());
		Hub(Catalog(2), Service(), router).OnPlayPressed(1);
		Assert.Eq(router.ChangedTo.Count, 0);
	}

	public void TestBackGoesToTheMainMenu()
	{
		SceneRouterProbe router = Adopt(new SceneRouterProbe());
		Hub(Catalog(1), Service(), router).BackButton.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Eq(router.LastRoute, SceneRouter.Route.MainMenu);
	}

	public void TestTheSkillTreeButtonOpensTheSkillTree()
	{
		SceneRouterProbe router = Adopt(new SceneRouterProbe());
		HubScreen hub = Hub(Catalog(1), Service(), router);
		Assert.False(hub.SkillTreeButton.Disabled);
		hub.SkillTreeButton.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Eq(router.LastRoute, SceneRouter.Route.SkillTree);
	}

	public void TestAnEmptyCatalogShowsAnEmptyStateAndBackStillWorks()
	{
		SceneRouterProbe router = Adopt(new SceneRouterProbe());
		HubScreen hub = Hub(new StageCatalog(), Service(), router);
		Assert.Eq(hub.StageList.GetChildCount(), 0);
		Assert.True(hub.EmptyLabel.Visible);
		hub.BackButton.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Eq(router.LastRoute, SceneRouter.Route.MainMenu);
	}

	public void TestAnInvalidCatalogDoesNotCrashTheHub()
	{
		StageCatalog catalog = Catalog(2);
		catalog.Stages[1].Duration = 0f;
		HubScreen hub = Hub(catalog, Service(), Adopt(new SceneRouterProbe()));
		Assert.True(hub.EmptyLabel.Visible);
		Assert.Eq(hub.StageList.GetChildCount(), 0);
	}

	public void TestTheShippedCatalogIsTheDefault()
	{
		HubScreen hub = Hub(null, Service(), Adopt(new SceneRouterProbe()));
		Assert.Eq(hub.StageList.GetChildCount(), 2);
		Assert.Eq(Card(hub, 1).NameLabel.Text, "Orc Frontier");
	}

	public void TestFocusStartsOnTheFirstStageSoEnterPlaysIt()
	{
		HubScreen hub = Hub(Catalog(2), Service(), Adopt(new SceneRouterProbe()));
		Assert.True(FocusOwner(hub) == Card(hub, 0).PlayButton);
	}

	public void TestFocusMovesToTheNextAvailableStageAfterAClear()
	{
		SaveService service = Service();
		service.RecordClear("s1");
		HubScreen hub = Hub(Catalog(3), service, Adopt(new SceneRouterProbe()));
		Assert.True(FocusOwner(hub) == Card(hub, 1).PlayButton);
	}

	public void TestFocusFallsBackToTheFirstStageWhenEverythingIsCleared()
	{
		SaveService service = Service();
		service.RecordClear("s1");
		service.RecordClear("s2");
		HubScreen hub = Hub(Catalog(2), service, Adopt(new SceneRouterProbe()));
		Assert.True(FocusOwner(hub) == Card(hub, 0).PlayButton);
	}

	public void TestFocusIsOnBackWhenThereAreNoStages()
	{
		HubScreen hub = Hub(new StageCatalog(), Service(), Adopt(new SceneRouterProbe()));
		Assert.True(FocusOwner(hub) == hub.BackButton);
	}
}
