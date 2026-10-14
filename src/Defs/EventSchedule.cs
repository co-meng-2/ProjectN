using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VampireSurvivor.Core;

namespace VampireSurvivor.Defs;

/// <summary>
/// When a stage's events happen. Times are whole seconds rolled once per run: a second is allowed when it lies inside
/// [WindowStart, WindowEnd] (and inside the stage) and is more than <see cref="WaveClearance"/> from every wave time.
/// Events are placed in order; each is drawn uniformly from the allowed seconds at least <see cref="MinGap"/> after the
/// previous event that still leave room to place the remaining events greedily, so a schedule whose greedy earliest
/// placement fits always rolls <see cref="Count"/> times.
/// </summary>
[GlobalClass]
public partial class EventSchedule : Resource
{
	[Export]
	public int Count { get; set; }

	[Export]
	public float WindowStart { get; set; }

	[Export]
	public float WindowEnd { get; set; }

	/// <summary>Between two event starts; must cover the longest time limit so at most one event runs at a time.</summary>
	[Export]
	public float MinGap { get; set; }

	[Export]
	public float WaveClearance { get; set; }

	[Export]
	public Godot.Collections.Array<EventDef> Pool { get; set; } = [];

	public int[] AllowedSeconds(float duration, float[] waveTimes)
	{
		var allowed = new List<int>();
		for (int second = Mathf.CeilToInt(WindowStart); second <= Mathf.FloorToInt(WindowEnd); second++)
		{
			if (second <= 0 || second >= duration || NearAWave(second, waveTimes))
			{
				continue;
			}

			allowed.Add(second);
		}

		return [.. allowed];
	}

	/// <summary><see cref="Count"/> ascending times, or an empty array when the schedule cannot be placed.</summary>
	public float[] RollTimes(float duration, float[] waveTimes, RunRng rng)
	{
		var times = new List<float>();
		if (Count <= 0)
		{
			return [];
		}

		int[] allowed = AllowedSeconds(duration, waveTimes);
		float previous = float.NegativeInfinity;
		for (int placed = 0; placed < Count; placed++)
		{
			int remaining = Count - placed - 1;
			int[] candidates = [.. allowed.Where(s => s >= previous + MinGap && FitsAfter(allowed, s, remaining))];
			if (candidates.Length == 0)
			{
				return [];
			}

			previous = candidates[rng.Int(0, candidates.Length - 1)];
			times.Add(previous);
		}

		return [.. times];
	}

	/// <summary>The greedy earliest placement; shorter than <see cref="Count"/> when the schedule is infeasible.</summary>
	public float[] EarliestTimes(float duration, float[] waveTimes)
	{
		var times = new List<float>();
		int[] allowed = AllowedSeconds(duration, waveTimes);
		float cursor = float.NegativeInfinity;
		for (int i = 0; i < Math.Max(Count, 0); i++)
		{
			int next = FirstAtOrAfter(allowed, cursor + MinGap);
			if (next < 0)
			{
				break;
			}

			times.Add(next);
			cursor = next;
		}

		return [.. times];
	}

	/// <summary>
	/// Defs for <paramref name="amount"/> events drawn from the pool, never the same def twice in a row when the pool has
	/// alternatives. Null entries are skipped (validation reports them).
	/// </summary>
	public List<EventDef> RollDefs(int amount, RunRng rng)
	{
		var picks = new List<EventDef>();
		List<EventDef> candidates = [.. Pool.Where(d => d is not null)];
		if (candidates.Count == 0)
		{
			return picks;
		}

		EventDef? previous = null;
		for (int i = 0; i < amount; i++)
		{
			List<EventDef> options = [.. candidates.Where(d => d != previous)];
			if (options.Count == 0)
			{
				options = candidates;
			}

			previous = rng.Pick(options)!;
			picks.Add(previous);
		}

		return picks;
	}

	public float LongestTimeLimit() => Pool.Where(d => d is not null).Select(d => d.TimeLimit).DefaultIfEmpty(0f).Max();

	public List<string> ValidateAgainst(float duration, float[] waveTimes)
	{
		var errors = new List<string>();
		if (Count < 0)
		{
			errors.Add("EventSchedule: count must be >= 0");
		}

		if (WindowStart <= 0f || WindowEnd >= duration || WindowStart > WindowEnd)
		{
			errors.Add($"EventSchedule: window [{WindowStart}, {WindowEnd}] must lie inside (0, stage duration {duration})");
		}

		if (WaveClearance < 0f)
		{
			errors.Add("EventSchedule: wave_clearance must be >= 0");
		}

		float longest = LongestTimeLimit();
		if (WindowEnd + longest > duration)
		{
			errors.Add($"EventSchedule: window_end {WindowEnd} + longest time_limit {longest} exceeds the stage duration {duration} (an event must be able to finish before time is up)");
		}

		int usable = 0;
		foreach (EventDef? def in Pool)
		{
			if (def is null)
			{
				errors.Add("EventSchedule: null event in the pool");
				continue;
			}

			usable++;
			errors.AddRange(def.Validate());
		}

		if (Count > 0 && usable == 0)
		{
			errors.Add($"EventSchedule: count is {Count} but the pool is empty");
		}

		if (MinGap < longest)
		{
			errors.Add($"EventSchedule: min_gap {MinGap} is shorter than the longest time_limit {longest} (two events could overlap)");
		}
		else if (Count > 0 && EarliestTimes(duration, waveTimes).Length < Count)
		{
			errors.Add($"EventSchedule: cannot place {Count} events in the window with min_gap {MinGap} and wave_clearance {WaveClearance}");
		}

		return errors;
	}

	private static int FirstAtOrAfter(int[] allowed, float from)
	{
		foreach (int second in allowed)
		{
			if (second >= from)
			{
				return second;
			}
		}

		return -1;
	}

	private bool NearAWave(float second, float[] waveTimes) => waveTimes.Any(t => Math.Abs(second - t) <= WaveClearance);

	/// <summary>Whether <paramref name="remaining"/> more events fit greedily after one placed at <paramref name="start"/>.</summary>
	private bool FitsAfter(int[] allowed, int start, int remaining)
	{
		float cursor = start;
		for (int i = 0; i < remaining; i++)
		{
			int next = FirstAtOrAfter(allowed, cursor + MinGap);
			if (next < 0)
			{
				return false;
			}

			cursor = next;
		}

		return true;
	}
}
