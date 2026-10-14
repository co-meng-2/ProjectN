using Godot;

namespace VampireSurvivor.Core;

/// <summary>Input action names. Bindings live in the project's InputMap.</summary>
public static class InputActions
{
	public static readonly StringName MoveLeft = "move_left";
	public static readonly StringName MoveRight = "move_right";
	public static readonly StringName MoveUp = "move_up";
	public static readonly StringName MoveDown = "move_down";
	public static readonly StringName Dash = "dash";
	public static readonly StringName Restart = "restart";

	/// <summary>Enter / KP Enter only. Not ui_accept: that also fires on Space, which is the dash key.</summary>
	public static readonly StringName Confirm = "continue_run";

	public static Vector2 MoveVector() => Input.GetVector(MoveLeft, MoveRight, MoveUp, MoveDown);
}
