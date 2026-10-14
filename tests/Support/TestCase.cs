using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace VampireSurvivor.Tests;

/// <summary>
/// Base class for headless tests. Subclasses add public <c>Test*</c> methods returning void or Task.
/// The runner sets <see cref="Host"/> to a node inside the SceneTree before each test.
/// </summary>
public abstract class TestCase
{
	private readonly List<Node> _owned = [];

	public Node Host { get; set; } = null!;

	public virtual void BeforeEach()
	{
	}

	public virtual void AfterEach()
	{
	}

	/// <summary>Adds the node under <see cref="Host"/> and frees it automatically after the test.</summary>
	public T Adopt<T>(T node)
		where T : Node
	{
		Host.AddChild(node);
		_owned.Add(node);
		return node;
	}

	public void ReleaseOwnedNodes()
	{
		foreach (Node node in _owned)
		{
			if (GodotObject.IsInstanceValid(node))
			{
				node.Free();
			}
		}

		_owned.Clear();
	}

	public async Task NextFrame()
	{
		await Host.ToSignal(Host.GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	public async Task NextPhysicsFrame()
	{
		await Host.ToSignal(Host.GetTree(), SceneTree.SignalName.PhysicsFrame);
	}

	/// <summary>Awaits two physics frames (enough for newly added areas to report overlaps).</summary>
	public async Task Settle()
	{
		await NextPhysicsFrame();
		await NextPhysicsFrame();
	}

	public async Task WaitSeconds(double seconds)
	{
		await Host.ToSignal(Host.GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	}
}
