using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
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

	/// <summary>
	/// An entity with a circle hurtbox (radius 12) under a Node2D at <paramref name="position"/>, so projectiles and sweeps can
	/// damage it. Read its health through <c>Entity.Attributes[Stats.Health]</c>.
	/// </summary>
	public static (ActorEntity Entity, Hurtbox Hurtbox) HurtTarget(
		TestCase test,
		Vector2 position,
		int layer = PhysicsLayers.EnemyHurt,
		float maxHealth = 100f,
		bool inEnemyGroup = false)
	{
		ActorEntity entity = BareActor(test, new StatBlock { MaxHealth = maxHealth });
		Node2D holder = test.Adopt(new Node2D { Position = position });
		if (inEnemyGroup)
		{
			holder.AddToGroup(Groups.Enemies);
		}

		var hurtbox = new Hurtbox { CollisionLayer = (uint)layer, CollisionMask = 0, Monitoring = false };
		hurtbox.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 12f } });
		holder.AddChild(hurtbox);
		hurtbox.Bind(entity, holder);
		return (entity, hurtbox);
	}

	/// <summary>An ability context for <paramref name="entity"/>: an actor node at <paramref name="at"/>, plus the helpers abilities need.</summary>
	public static AbilityContext Context(TestCase test, ActorEntity entity, Vector2 at)
	{
		var actor = test.Adopt(new Node2D { Position = at });
		var context = new AbilityContext(
			actor, test.Adopt(new Mover()), test.Adopt(new TargetProvider()), test.Adopt(new Node2D()), new Wallet());
		entity.Context = context;
		return context;
	}

	/// <summary>A set-up Player (real scene) in the test tree with a fresh TargetProvider and projectile layer (see <c>Context</c>).</summary>
	public static Player Player(TestCase test, PlayerProfile? profile = null, Wallet? wallet = null)
	{
		var layer = test.Adopt(new Node2D());
		var targets = test.Adopt(new TargetProvider());
		var player = test.Adopt(GD.Load<PackedScene>("res://port/src/Actors/Player/player.tscn").Instantiate<Player>());
		player.Setup(profile ?? DefFactory.Profile(), targets, layer, wallet ?? new Wallet());
		targets.Player = player;
		return player;
	}
}
