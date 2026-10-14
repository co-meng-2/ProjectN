using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Stage;

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

	/// <summary>Deals far more damage than any test actor has health, from a fresh bare attacker.</summary>
	public static void Kill(TestCase test, ActorEntity victim)
	{
		DamageEffects.Hit(BareActor(test, new StatBlock()), victim, 1_000_000f);
	}

	/// <summary>Children of <paramref name="parent"/> of type <typeparamref name="T"/> that are not queued for deletion.</summary>
	public static System.Collections.Generic.List<T> LiveChildren<T>(Node parent)
		where T : Node
	{
		var found = new System.Collections.Generic.List<T>();
		foreach (Node child in parent.GetChildren())
		{
			if (child is T typed && !child.IsQueuedForDeletion())
			{
				found.Add(typed);
			}
		}

		return found;
	}

	/// <summary>
	/// An event context with a fresh layer, factory and target provider: the building at <paramref name="buildingAt"/>,
	/// the player (or a stand-in 150 px below the building), arena (-1200, -800, 2400, 1600), spawn radius 300, rng seed 7.
	/// </summary>
	public static EventContext ContextFor(TestCase test, EventDef def, Node2D? player = null, Vector2 buildingAt = default)
	{
		Node2D layer = test.Adopt(new Node2D());
		Node2D enemies = test.Adopt(new Node2D());
		TargetProvider targets = test.Adopt(new TargetProvider());
		Node2D building = test.Adopt(new Node2D { Position = buildingAt });
		player ??= test.Adopt(new Node2D { Position = buildingAt + new Vector2(0f, 150f) });
		targets.Building = building;
		targets.Player = player;
		var rng = new RunRng(7);
		EnemyFactory factory = test.Adopt(new EnemyFactory());
		factory.Bind(enemies, targets, rng);
		return new EventContext(layer, factory, targets, player, building, rng, new Rect2(-1200f, -800f, 2400f, 1600f), 300f, def);
	}

	/// <summary>Records an event's signals in emission order. Call before <c>Begin</c>: it may report progress immediately.</summary>
	public static EventRecord WatchEvent(StageEvent stageEvent)
	{
		var record = new EventRecord();
		stageEvent.Succeeded += () => record.Succeeded++;
		stageEvent.Failed += () => record.Failed++;
		stageEvent.ProgressChanged += record.Progress.Add;
		return record;
	}

	public sealed class EventRecord
	{
		public int Succeeded { get; set; }

		public int Failed { get; set; }

		public System.Collections.Generic.List<string> Progress { get; } = [];
	}
}
