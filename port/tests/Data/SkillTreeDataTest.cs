using System.Linq;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Data;

public class SkillTreeDataTest : TestCase
{
	public const string TreePath = "res://port/data/skill_tree/skill_tree.tres";

	private static SkillTreeDef Tree() => GD.Load<SkillTreeDef>(TreePath);

	public void TestTheShippedTreeValidatesAndHasTheExpectedOrder()
	{
		SkillTreeDef tree = Tree();
		Assert.NoErrors(tree.Validate(), "skill_tree");
		Assert.SequenceEq(tree.Nodes.Select(n => n.Id.ToString()), new[] { "vitality", "swiftness", "might", "extra_slot" });
	}

	public void TestTheShippedRequirements()
	{
		SkillTreeDef tree = Tree();
		Assert.Eq(tree.NodeById("vitality")!.Requires.Count, 0);
		Assert.Eq(tree.NodeById("swiftness")!.Requires.Count, 0);
		Assert.SequenceEq(tree.NodeById("might")!.Requires.Select(r => r.ToString()), new[] { "vitality" });
		Assert.SequenceEq(tree.NodeById("extra_slot")!.Requires.Select(r => r.ToString()), new[] { "might" });
	}

	/// <summary>On a default StatBlock actor (max health 100, move speed 100, attack 1).</summary>
	public void TestPassiveNodesScaleWithThePurchasedLevel()
	{
		SkillTreeDef tree = Tree();
		foreach (var (id, stat, atOne, atMax) in new[]
		{
			("vitality", Stats.MaxHealth, 110.0, 135.0),
			("swiftness", Stats.MoveSpeed, 110.0, 130.0),
			("might", Stats.Attack, 1.1, 1.3),
		})
		{
			SkillNodeDef node = tree.NodeById(id)!;
			ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
			var handle = actor.ApplyPassive(node.Passive!, 1);
			Assert.AlmostEq(actor.Attributes[stat].DisplayValue, atOne, 0.001, $"{id} at level 1");
			actor.RemovePassive(handle);
			actor.ApplyPassive(node.Passive!, node.MaxLevel);
			Assert.AlmostEq(actor.Attributes[stat].DisplayValue, atMax, 0.001, $"{id} at max level");
		}
	}

	public void TestExtraSlotIsAPureSlotBonus()
	{
		SkillNodeDef node = Tree().NodeById("extra_slot")!;
		Assert.Null(node.Passive);
		Assert.Eq(node.SlotBonus, 1);
		Assert.Eq(node.MaxLevel, 1);
	}

	public void TestCostsGrowWithEachLevel()
	{
		foreach (SkillNodeDef node in Tree().Nodes)
		{
			for (int i = 1; i < node.Costs.Length; i++)
			{
				Assert.True(node.Costs[i] > node.Costs[i - 1], $"{node.Id} level {i + 1}");
			}
		}
	}
}
