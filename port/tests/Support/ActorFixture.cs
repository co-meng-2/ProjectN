using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Tests.Support;

/// <summary>Builds bare actors for Forge-level tests.</summary>
public static class ActorFixture
{
	private const string ScenePath = "res://port/src/Components/actor_entity.tscn";

	/// <summary>
	/// An ActorEntity inside the test tree with physics ticking off (call <see cref="ActorEntity.Tick"/> yourself).
	/// Stats are applied after the node entered the tree: attribute sets are created in _Ready.
	/// </summary>
	public static ActorEntity BareActor(TestCase test, StatBlock? stats = null)
	{
		var entity = GD.Load<PackedScene>(ScenePath).Instantiate<ActorEntity>();
		entity.SetPhysicsProcess(false);
		test.Adopt(entity);
		if (stats is not null)
		{
			entity.ApplyStats(stats);
		}

		return entity;
	}
}
