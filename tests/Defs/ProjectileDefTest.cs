using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class ProjectileDefTest : TestCase
{
	private static ProjectileDef Valid() => new() { Texture = DefFactory.Texture(8, 8) };

	public void TestDefaultsWithATextureAreValid()
	{
		Assert.NoErrors(Valid().Validate());
	}

	public void TestHitMaskFollowsTheTeam()
	{
		ProjectileDef def = Valid();
		Assert.Eq(def.HitMask(), PhysicsLayers.EnemyHurt);
		def.Hits = ProjectileTeam.Allies;
		Assert.Eq(def.HitMask(), PhysicsLayers.AllyHurt);
	}

	public void TestMissingTextureAndNonPositiveNumbersAreReported()
	{
		var def = new ProjectileDef { Texture = null, Speed = 0f };
		Assert.Eq(def.Validate().Count, 2);
	}
}
