using Godot;

namespace VampireSurvivor.Core;

/// <summary>Scene-tree group names. Used only for targeting queries.</summary>
public static class Groups
{
	public static readonly StringName Enemies = "enemies";
	public static readonly StringName Player = "player";
	public static readonly StringName Building = "building";
}
