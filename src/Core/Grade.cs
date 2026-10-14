using Godot;

namespace VampireSurvivor.Core;

/// <summary>
/// The quality ladder of an ability. Revealed is the enhanced form of the same Legendary ability, so only
/// Common..Legendary are rolled from pools. <see cref="GradeExtensions.Level"/> is the Forge ability level and curve X.
/// </summary>
public enum Grade
{
	None,
	Common,
	Rare,
	Epic,
	Unique,
	Legendary,
	Revealed,
}

public static class GradeExtensions
{
	private static readonly string[] Names = ["Empty", "Common", "Rare", "Epic", "Unique", "Legendary", "Revealed"];

	private static readonly Color[] Tints =
	[
		new(0.35f, 0.35f, 0.38f), // None
		new(0.80f, 0.80f, 0.80f), // Common
		new(0.30f, 0.60f, 1.00f), // Rare
		new(0.70f, 0.35f, 1.00f), // Epic
		new(1.00f, 0.55f, 0.20f), // Unique
		new(1.00f, 0.85f, 0.25f), // Legendary
		new(0.55f, 1.00f, 0.95f), // Revealed
	];

	public static Grade Next(this Grade grade) => grade == Grade.Revealed ? Grade.Revealed : grade + 1;

	public static bool IsRollable(this Grade grade) => grade != Grade.None && grade != Grade.Revealed;

	public static float Level(this Grade grade) => (float)grade;

	public static string DisplayName(this Grade grade) => Names[(int)grade];

	public static Color Tint(this Grade grade) => Tints[(int)grade];
}
