using VampireSurvivor.Meta;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Meta;

public class SaveServiceTest : TestCase
{
	public const string Dir = "user://test_save_service";
	public const string Path = Dir + "/save.json";

	public override void BeforeEach() => SaveFiles.Reset(Dir);

	public override void AfterEach() => SaveFiles.Reset(Dir);

	/// <summary>A service on a scratch file; it loads when it enters the tree.</summary>
	public static SaveService Service(TestCase test, string path = Path) => test.Adopt(new SaveService { Path = path });

	public void TestStartsFromDefaultsWithoutAFile() => Assert.Eq(Service(this).Data.Gem, 0L);

	public void TestAddGemAccumulatesAndRaisesChanged()
	{
		SaveService service = Service(this);
		int raised = 0;
		service.Changed += () => raised++;
		service.AddGem(3);
		service.AddGem(4);
		Assert.Eq(service.Data.Gem, 7L);
		Assert.Eq(raised, 2);
	}

	public void TestNegativeGemIsIgnored()
	{
		SaveService service = Service(this);
		service.AddGem(5);
		service.AddGem(-3);
		Assert.Eq(service.Data.Gem, 5L);
	}

	public void TestSaveThenReloadKeepsGemAndVolume()
	{
		SaveService service = Service(this);
		service.AddGem(9);
		service.Data.Settings.MasterVolume = 0.3f;
		Assert.True(service.Save());
		SaveService fresh = Service(this);
		Assert.Eq(fresh.Data.Gem, 9L);
		Assert.AlmostEq(fresh.Data.Settings.MasterVolume, 0.3);
	}

	public void TestRecordClearMarksTheStageAndRaisesChanged()
	{
		SaveService service = Service(this);
		int raised = 0;
		service.Changed += () => raised++;
		service.RecordClear("stage_01");
		Assert.SequenceEq(service.Data.ClearedStages, new[] { "stage_01" });
		Assert.Eq(raised, 1);
	}

	public void TestRecordClearTwiceKeepsOneEntry()
	{
		SaveService service = Service(this);
		service.RecordClear("stage_01");
		service.RecordClear("stage_01");
		Assert.Eq(service.Data.ClearedStages.Count, 1);
	}

	public void TestARecordedClearSurvivesSaveAndReload()
	{
		SaveService service = Service(this);
		service.RecordClear("stage_01");
		Assert.True(service.Save());
		Assert.SequenceEq(Service(this).Data.ClearedStages, new[] { "stage_01" });
	}
}
