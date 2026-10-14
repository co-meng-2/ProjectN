using System.Collections.Generic;
using Godot;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Tests.Support;

/// <summary>A scene root that reports <see cref="RejectReason"/> from its own Validate (empty = valid).</summary>
public partial class RejectingNode2D : Node2D, IValidatable
{
	[Export]
	public string RejectReason { get; set; } = string.Empty;

	public List<string> Validate() => RejectReason.Length == 0 ? [] : [RejectReason];
}
