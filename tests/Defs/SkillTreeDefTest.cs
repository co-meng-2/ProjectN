using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class SkillTreeDefTest : TestCase
{
	public void TestAValidTreeHasNoErrors()
	{
		Assert.NoErrors(DefFactory.Tree(DefFactory.SkillNode("a"), DefFactory.SkillNode("b", ["a"])).Validate(), "a then b requiring a");
	}

	public void TestAnEmptyTreeIsAnError() => Assert.Eq(new SkillTreeDef().Validate().Count, 1);

	public void TestANullNodeIsAnError() => Assert.Eq(DefFactory.Tree(DefFactory.SkillNode("a"), null).Validate().Count, 1);

	public void TestDuplicateIdsAreAnError() => Assert.Eq(DefFactory.Tree(DefFactory.SkillNode("a"), DefFactory.SkillNode("a")).Validate().Count, 1);

	public void TestRequiringAnUnknownNodeIsAnError() => Assert.Eq(DefFactory.Tree(DefFactory.SkillNode("a", ["ghost"])).Validate().Count, 1);

	public void TestRequiringANodeListedLaterIsAnError()
	{
		Assert.Eq(DefFactory.Tree(DefFactory.SkillNode("a", ["b"]), DefFactory.SkillNode("b")).Validate().Count, 1);
	}

	public void TestABrokenNestedNodeIsReported()
	{
		SkillNodeDef broken = DefFactory.SkillNode("b", ["a"]);
		broken.MaxLevel = 0;
		Assert.True(DefFactory.Tree(DefFactory.SkillNode("a"), broken).Validate().Count >= 1);
	}

	public void TestIndexOfAndNodeById()
	{
		SkillTreeDef tree = DefFactory.Tree(DefFactory.SkillNode("a"), DefFactory.SkillNode("b"));
		Assert.Eq(tree.IndexOf("b"), 1);
		Assert.Eq(tree.IndexOf("missing"), -1);
		Assert.Eq(tree.NodeById("a")!.Id.ToString(), "a");
		Assert.Null(tree.NodeById("missing"));
	}
}
