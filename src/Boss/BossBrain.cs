using System;
using System.Collections.Generic;
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Boss;

/// <summary>
/// Decides when the boss attacks: after the pattern gap of chasing it picks the next pattern (Sequence = list order,
/// wrapping; Random = weighted, never the same twice in a row when there are alternatives), activates it through its
/// grant, waits for the entity to report that instance ended, then starts the next gap. Plain code, not Forge: the
/// patterns are the abilities.
/// </summary>
public partial class BossBrain : Node
{
	private readonly List<AbilityGrant> _patterns = [];
	private readonly List<float> _weights = [];
	private ActorEntity? _entity;
	private BossOrder _order = BossOrder.Sequence;
	private float _gap;
	private RunRng _rng = null!;
	private float _gapLeft;
	private int _nextIndex;
	private int _lastIndex = -1;
	private bool _stopped;

	/// <summary>The index (in def order) of every pattern the brain starts.</summary>
	[Signal]
	public delegate void PatternStartedEventHandler(int index);

	/// <summary>The pattern the brain is waiting on, or null while it counts down a gap.</summary>
	public AbilityGrant? Casting { get; private set; }

	/// <summary>Adds a brain named "Brain" under a set-up boss and binds it to the boss's patterns and def.</summary>
	public static BossBrain AttachTo(BossActor boss, RunRng rng)
	{
		var brain = new BossBrain { Name = "Brain" };
		boss.AddChild(brain);
		brain.Bind(boss.Entity, boss.Patterns, boss.PatternWeights, boss.Def!.Order, boss.Def.PatternGap, rng);
		boss.Died += brain.OnBossDied;
		return brain;
	}

	/// <summary>
	/// Boss-layer check run by the stage (defs cannot name pattern classes): every pattern is a <see cref="PatternDef"/>
	/// whose tags are registered, and no two patterns share an ability tag.
	/// </summary>
	public static List<string> ValidateDef(BossDef? def)
	{
		var errors = new List<string>();
		if (def is null)
		{
			return errors;
		}

		var seen = new HashSet<string>();
		foreach (BossPatternEntry? entry in def.Patterns)
		{
			if (entry?.Pattern is not { } pattern)
			{
				continue; // BossDef.Validate reports these
			}

			if (pattern is not PatternDef)
			{
				errors.Add($"boss '{def.Id}': pattern '{pattern.Tag}' is not a boss pattern");
				continue;
			}

			foreach (string error in AbilityFactory.Validate(pattern))
			{
				errors.Add($"boss '{def.Id}': {error}");
			}

			if (!seen.Add(pattern.Tag))
			{
				errors.Add($"boss '{def.Id}': two patterns share the ability tag '{pattern.Tag}'");
			}
		}

		return errors;
	}

	public void Bind(ActorEntity entity, IReadOnlyList<AbilityGrant> patterns, IReadOnlyList<float> weights, BossOrder order, float gap, RunRng rng)
	{
		if (_entity is not null)
		{
			_entity.AbilityEnded -= OnAbilityEnded;
		}

		_entity = entity;
		_patterns.Clear();
		_patterns.AddRange(patterns);
		_weights.Clear();
		_weights.AddRange(weights);
		_order = order;
		_gap = gap;
		_rng = rng;
		_gapLeft = gap;
		_nextIndex = 0;
		_lastIndex = -1;
		Casting = null;
		_stopped = false;
		entity.AbilityEnded += OnAbilityEnded;
	}

	public void Stop() => _stopped = true;

	public void Step(float delta)
	{
		if (_stopped || Casting is not null || _patterns.Count == 0)
		{
			return;
		}

		_gapLeft -= delta;
		if (_gapLeft > 0f)
		{
			return;
		}

		Activate(Pick());
	}

	public override void _PhysicsProcess(double delta) => Step((float)delta);

	/// <summary>The entity's event is plain C#: let go of it so a freed brain is never called.</summary>
	public override void _ExitTree()
	{
		if (_entity is not null && IsInstanceValid(_entity))
		{
			_entity.AbilityEnded -= OnAbilityEnded;
		}
	}

	private int Pick()
	{
		int index;
		if (_order == BossOrder.Sequence)
		{
			index = _nextIndex % _patterns.Count;
			_nextIndex = index + 1;
		}
		else
		{
			var weights = new List<float>(_weights);
			if (_patterns.Count > 1 && _lastIndex >= 0)
			{
				weights[_lastIndex] = 0f;
			}

			index = Math.Max(_rng.WeightedIndex(weights), 0);
		}

		_lastIndex = index;
		return index;
	}

	private void Activate(int index)
	{
		AbilityGrant pattern = _patterns[index];
		Casting = pattern;
		_gapLeft = _gap;
		EmitSignal(SignalName.PatternStarted, index);

		// A pattern that finishes (or refuses) at once has already cleared Casting through AbilityEnded.
		bool activated = pattern.Ability is { IsValid: true } ability && ability.Activate(out AbilityActivationFailures _);
		if (!activated && Casting == pattern)
		{
			Casting = null;
		}
	}

	private void OnAbilityEnded(AbilityHandle handle, bool cancelled)
	{
		if (Casting?.Ability != handle)
		{
			return;
		}

		Casting = null;
		_gapLeft = _gap;
	}

	private void OnBossDied(BossActor boss) => Stop();
}
