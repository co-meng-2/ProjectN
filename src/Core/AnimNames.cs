using Godot;

namespace VampireSurvivor.Core;

/// <summary>Animation names shared by visual definitions and ActorVisual.</summary>
public static class AnimNames
{
	public static readonly StringName Idle = "idle";
	public static readonly StringName Walk = "walk";
	public static readonly StringName Attack = "attack";
	public static readonly StringName Hurt = "hurt";
	public static readonly StringName Death = "death";
}
