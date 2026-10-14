using Godot;
using VampireSurvivor.Core;

namespace VampireSurvivor.Defs;

/// <summary>The concrete result of rolling one <see cref="DropEntry"/>.</summary>
public sealed class DropRoll
{
	public CurrencyKind Kind { get; init; } = CurrencyKind.Gold;

	public int Amount { get; init; }

	public Texture2D? Icon { get; init; }
}
