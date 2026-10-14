using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class StageCatalogTest : TestCase
{
	public void TestAValidCatalogHasNoErrors() => Assert.NoErrors(DefFactory.Catalog("a", "b").Validate(), "two distinct valid stages");

	public void TestAnEmptyCatalogIsAnError() => Assert.Eq(new StageCatalog().Validate().Count, 1);

	public void TestANullEntryIsAnError()
	{
		StageCatalog catalog = DefFactory.Catalog("a");
		catalog.Stages.Add(null!);
		Assert.Eq(catalog.Validate().Count, 1);
	}

	public void TestDuplicateIdsAreAnError() => Assert.Eq(DefFactory.Catalog("a", "a").Validate().Count, 1);

	public void TestABrokenNestedStageIsReported()
	{
		StageCatalog catalog = DefFactory.Catalog("a", "b");
		catalog.Stages[1].Duration = 0f;
		Assert.True(catalog.Validate().Count >= 1);
	}

	public void TestIndexOfFindsAStageOrReturnsMinusOne()
	{
		StageCatalog catalog = DefFactory.Catalog("a", "b");
		Assert.Eq(catalog.IndexOf("b"), 1);
		Assert.Eq(catalog.IndexOf("missing"), -1);
	}
}
