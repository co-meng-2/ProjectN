namespace VampireSurvivor.Core;

/// <summary>2D physics layer bitmasks. Names must match [layer_names] in project settings.</summary>
public static class PhysicsLayers
{
	public const int World = 1 << 0;
	public const int AllyBody = 1 << 1;
	public const int EnemyBody = 1 << 2;
	public const int AllyHurt = 1 << 3;
	public const int EnemyHurt = 1 << 4;
	public const int Pickup = 1 << 5;
}
