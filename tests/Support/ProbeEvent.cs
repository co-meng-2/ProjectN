using System.Collections.Generic;
using Godot;
using VampireSurvivor.Stage;

namespace VampireSurvivor.Tests.Support;

/// <summary>
/// Test double: counts begins and cleanups, finishes on demand, optionally ignores its time limit (<see cref="Endless"/>),
/// exposes objectives, and reports <see cref="RejectReason"/> from Validate so def validation can be tested.
/// </summary>
public partial class ProbeEvent : StageEvent
{
	[Export]
	public bool Endless { get; set; }

	[Export]
	public string RejectReason { get; set; } = string.Empty;

	public int Begins { get; private set; }

	public int Cleanups { get; private set; }

	public List<Node2D> ObjectiveNodes { get; } = [];

	public override List<string> Validate() => RejectReason.Length == 0 ? [] : [RejectReason];

	public override List<Node2D> Objectives() => [.. ObjectiveNodes.FindAll(IsInstanceValid)];

	public void Finish(bool success)
	{
		if (success)
		{
			Succeed();
		}
		else
		{
			Fail();
		}
	}

	protected override void OnBegin()
	{
		Begins++;
		ReportProgress($"Probe {Context!.Def.Id}");
	}

	protected override void OnTimeUp()
	{
		if (!Endless)
		{
			base.OnTimeUp();
		}
	}

	protected override void Cleanup() => Cleanups++;
}
