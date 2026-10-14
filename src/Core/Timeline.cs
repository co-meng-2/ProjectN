using System;
using System.Collections.Generic;

namespace VampireSurvivor.Core;

/// <summary>Pure schedule math: a duration plus sorted mark times (for example wave starts).</summary>
public sealed class Timeline
{
	public Timeline(float duration, float[]? marks = null)
	{
		Duration = duration;
		Marks = marks is null ? [] : [.. marks];
		Array.Sort(Marks);
	}

	public float Duration { get; }

	public float[] Marks { get; }

	public float Progress(float time)
	{
		if (Duration <= 0f)
		{
			return 1f;
		}

		return Math.Clamp(time / Duration, 0f, 1f);
	}

	/// <summary>Time of the first mark strictly after <paramref name="time"/>, or -1 when none remain.</summary>
	public float NextAfter(float time)
	{
		foreach (float mark in Marks)
		{
			if (mark > time)
			{
				return mark;
			}
		}

		return -1f;
	}

	/// <summary>Indices of marks with from &lt; mark &lt;= to, in ascending order.</summary>
	public int[] Crossed(float fromTime, float toTime)
	{
		var indices = new List<int>();
		for (int i = 0; i < Marks.Length; i++)
		{
			if (Marks[i] > fromTime && Marks[i] <= toTime)
			{
				indices.Add(i);
			}
		}

		return [.. indices];
	}
}
