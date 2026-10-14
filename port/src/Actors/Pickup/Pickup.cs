using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Actors;

/// <summary>A currency drop. Sits still until a collector attracts it, then flies to the target and is consumed.</summary>
public partial class Pickup : Area2D
{
	private const float IconSize = 16f;

	private Node2D? _target;

	[Signal]
	public delegate void ReachedEventHandler(Pickup pickup);

	[Export]
	public float AttractSpeed { get; set; } = 260f;

	[Export]
	public float ReachDistance { get; set; } = 8f;

	[Export]
	public Sprite2D Sprite { get; set; } = null!;

	public CurrencyKind Kind { get; private set; } = CurrencyKind.Gold;

	public int Amount { get; private set; }

	/// <summary>Call after the pickup has entered the tree.</summary>
	public void Setup(DropRoll roll)
	{
		Kind = roll.Kind;
		Amount = roll.Amount;
		Sprite.Texture = roll.Icon;
		if (roll.Icon is not null)
		{
			float largest = Mathf.Max(roll.Icon.GetWidth(), roll.Icon.GetHeight());
			Sprite.Scale = Vector2.One * (IconSize / largest);
		}
	}

	public void AttractTo(Node2D? target) => _target = target;

	public void Step(double delta)
	{
		if (!IsInstanceValid(_target))
		{
			return;
		}

		Vector2 offset = _target!.GlobalPosition - GlobalPosition;
		float travel = AttractSpeed * (float)delta;
		if (offset.Length() <= ReachDistance + travel)
		{
			_target = null;
			EmitSignal(SignalName.Reached, this);
			QueueFree();
			return;
		}

		GlobalPosition += offset.Normalized() * travel;
	}

	public override void _PhysicsProcess(double delta) => Step(delta);
}
