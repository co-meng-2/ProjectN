using System.Collections.Generic;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Stage;

namespace VampireSurvivor.Events;

/// <summary>
/// "Meteor shower": every <see cref="Interval"/> seconds a warning circle appears within <see cref="LandingSpread"/> of the
/// player; <see cref="Warning"/> seconds later an AreaStrike hits only the player there. More than <see cref="MaxHits"/>
/// landed hits fails at once; surviving the time limit succeeds. The event owns a small entity so the damage has a source.
/// </summary>
public partial class MeteorEvent : StageEvent
{
	private const string EntityScene = "res://port/src/Components/actor_entity.tscn";

	private readonly List<Incoming> _incoming = [];
	private ActorEntity? _source;
	private float _nextDropIn;

	[Export]
	public PackedScene? StrikeScene { get; set; }

	[Export]
	public float Interval { get; set; }

	[Export]
	public float Warning { get; set; }

	[Export]
	public float Radius { get; set; }

	[Export]
	public float Damage { get; set; }

	[Export]
	public float LandingSpread { get; set; }

	[Export]
	public int MaxHits { get; set; }

	/// <summary>How long each landed strike stays active; it must outlive a physics step so overlaps are reported.</summary>
	[Export]
	public float StrikeSeconds { get; set; }

	public int Hits { get; private set; }

	public override List<string> Validate()
	{
		var errors = new List<string>();
		if (StrikeScene is null)
		{
			errors.Add("MeteorEvent: strike_scene is required");
		}
		else
		{
			Node probe = StrikeScene.Instantiate();
			if (probe is not AreaStrike)
			{
				errors.Add("MeteorEvent: strike_scene root is not an AreaStrike");
			}

			probe.Free();
		}

		if (Interval <= 0f || Warning <= 0f || Radius <= 0f || StrikeSeconds <= 0f)
		{
			errors.Add("MeteorEvent: interval, warning, radius and strike_seconds must be > 0");
		}

		if (Damage < 0f || LandingSpread < 0f || MaxHits < 0)
		{
			errors.Add("MeteorEvent: damage, landing_spread and max_hits must be >= 0");
		}

		return errors;
	}

	protected override void OnBegin()
	{
		_source = GD.Load<PackedScene>(EntityScene).Instantiate<ActorEntity>();
		AddChild(_source);
		_nextDropIn = Interval;
		Report();
	}

	protected override void OnStep(float delta)
	{
		for (int i = _incoming.Count - 1; i >= 0; i--)
		{
			Incoming meteor = _incoming[i];
			meteor.Left -= delta;
			if (meteor.Left <= 0f)
			{
				_incoming.RemoveAt(i);
				Land(meteor);
			}
		}

		_nextDropIn -= delta;
		while (_nextDropIn <= 0f)
		{
			Drop();
			_nextDropIn += Interval;
		}
	}

	/// <summary>Surviving the shower is the objective.</summary>
	protected override void OnTimeUp() => Succeed();

	protected override void Cleanup()
	{
		foreach (Incoming meteor in _incoming)
		{
			if (IsInstanceValid(meteor.Telegraph))
			{
				meteor.Telegraph.QueueFree();
			}
		}

		_incoming.Clear();
		if (IsInstanceValid(_source))
		{
			_source!.CleanupDeferred();
		}
	}

	private void Drop()
	{
		if (!IsInstanceValid(Context!.Player))
		{
			return;
		}

		Vector2 point = Context.Player!.GlobalPosition + Context.Rng.OffsetInCircle(LandingSpread);
		_incoming.Add(new Incoming(point, Warning, Telegraph.SpawnCircle(this, point, Radius, Warning)));
	}

	private void Land(Incoming meteor)
	{
		if (IsInstanceValid(meteor.Telegraph))
		{
			meteor.Telegraph.QueueFree();
		}

		if (!IsInstanceValid(Context!.Player))
		{
			return; // a freed only-actor would turn the filter off and hit the building
		}

		var strike = StrikeScene!.Instantiate<AreaStrike>();
		AddChild(strike);
		strike.GlobalPosition = meteor.Position;
		strike.Struck += OnPlayerStruck;
		strike.Strike(_source!, Damage, Radius, StrikeSeconds, PhysicsLayers.AllyHurt, Context.Player);
	}

	private void OnPlayerStruck(Hurtbox hurtbox)
	{
		if (!IsRunning)
		{
			return;
		}

		Hits++;
		Report();
		if (Hits > MaxHits)
		{
			Fail();
		}
	}

	private void Report() => ReportProgress($"Hits {Hits}/{MaxHits}");

	/// <summary>A warned meteor that has not landed yet.</summary>
	private sealed class Incoming(Vector2 position, float left, Telegraph telegraph)
	{
		public Vector2 Position { get; } = position;

		public float Left { get; set; } = left;

		public Telegraph Telegraph { get; } = telegraph;
	}
}
