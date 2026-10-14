using VampireSurvivor.Core;

namespace VampireSurvivor.Tests.Core;

public class PhysicsLayersTest : TestCase
{
	public void TestLayersAreDistinctSingleBits()
	{
		int[] layers =
		[
			PhysicsLayers.World, PhysicsLayers.AllyBody, PhysicsLayers.EnemyBody,
			PhysicsLayers.AllyHurt, PhysicsLayers.EnemyHurt, PhysicsLayers.Pickup,
		];
		int combined = 0;
		foreach (int layer in layers)
		{
			Assert.Eq(layer & (layer - 1), 0, "single bit");
			Assert.Eq(combined & layer, 0, "overlap");
			combined |= layer;
		}
	}
}
