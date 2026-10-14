using Godot;

namespace VampireSurvivor.Defs;

/// <summary>One surge: <see cref="Count"/> enemies of one type arrive at stage time <see cref="Time"/>.</summary>
[GlobalClass]
public partial class WaveEntry : Resource
{
	[Export]
	public float Time { get; set; } = 30f;

	[Export]
	public EnemyDef? Enemy { get; set; }

	[Export]
	public int Count { get; set; } = 10;

	[Export]
	public string Label { get; set; } = "Wave";
}
