using System;
using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.Core;

/// <summary>Seeded random source for one stage run so spawns, drops and rerolls are reproducible.</summary>
public sealed class RunRng
{
	private readonly RandomNumberGenerator _rng = new();

	public RunRng(long seed = 0)
	{
		SeedValue = seed != 0 ? seed : (long)Time.GetTicksUsec();
		_rng.Seed = (ulong)SeedValue;
	}

	public long SeedValue { get; }

	public float Float() => _rng.Randf();

	public float Range(float from, float to) => _rng.RandfRange(from, to);

	public int Int(int from, int to) => _rng.RandiRange(from, to);

	public bool Chance(float probability) => _rng.Randf() < probability;

	/// <summary>A uniformly chosen item, or null for an empty list.</summary>
	public T? Pick<T>(IReadOnlyList<T> items)
		where T : class
	{
		return items.Count == 0 ? null : items[_rng.RandiRange(0, items.Count - 1)];
	}

	/// <summary>Index chosen proportionally to <paramref name="weights"/>; -1 when no weight is positive.</summary>
	public int WeightedIndex(IReadOnlyList<float> weights)
	{
		float total = 0f;
		foreach (float weight in weights)
		{
			total += Math.Max(weight, 0f);
		}

		if (total <= 0f)
		{
			return -1;
		}

		float roll = _rng.Randf() * total;
		int lastPositive = -1;
		for (int i = 0; i < weights.Count; i++)
		{
			if (weights[i] <= 0f)
			{
				continue;
			}

			lastPositive = i;
			roll -= weights[i];
			if (roll < 0f)
			{
				return i;
			}
		}

		return lastPositive;
	}

	public T? Weighted<T>(IReadOnlyList<T> items, IReadOnlyList<float> weights)
		where T : class
	{
		int index = WeightedIndex(weights);
		return index >= 0 && index < items.Count ? items[index] : null;
	}

	/// <summary>Uniformly distributed point inside a circle around the origin.</summary>
	public Vector2 OffsetInCircle(float radius)
	{
		return Vector2.Right.Rotated(_rng.Randf() * Mathf.Tau) * radius * Mathf.Sqrt(_rng.Randf());
	}
}
