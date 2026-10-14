using System;
using System.Collections.Generic;
using System.Linq;

namespace VampireSurvivor.Tests;

public sealed class AssertionException(string message) : Exception(message);

/// <summary>Static assertions. The first failed assertion throws and ends the test.</summary>
public static class Assert
{
	public static void True(bool condition, string message = "expected true")
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}

	public static void False(bool condition, string message = "expected false") => True(!condition, message);

	public static void Eq<T>(T actual, T expected, string message = "")
	{
		if (!EqualityComparer<T>.Default.Equals(actual, expected))
		{
			throw new AssertionException($"{message} (expected {expected}, got {actual})".Trim());
		}
	}

	public static void SequenceEq<T>(IEnumerable<T> actual, IEnumerable<T> expected, string message = "")
	{
		T[] actualItems = actual.ToArray();
		T[] expectedItems = expected.ToArray();
		if (!actualItems.SequenceEqual(expectedItems))
		{
			throw new AssertionException(
				$"{message} (expected [{string.Join(", ", expectedItems)}], got [{string.Join(", ", actualItems)}])".Trim());
		}
	}

	public static void AlmostEq(double actual, double expected, double epsilon = 0.001, string message = "")
	{
		if (Math.Abs(actual - expected) > epsilon)
		{
			throw new AssertionException($"{message} (expected {expected} +/- {epsilon}, got {actual})".Trim());
		}
	}

	public static void NotNull(object? value, string message = "expected non-null")
	{
		if (value is null)
		{
			throw new AssertionException(message);
		}
	}

	public static void Null(object? value, string message = "")
	{
		if (value is not null)
		{
			throw new AssertionException($"{message} expected null, got {value}".Trim());
		}
	}

	public static void NoErrors(IEnumerable<string> errors, string message = "")
	{
		string[] list = errors.ToArray();
		if (list.Length > 0)
		{
			throw new AssertionException($"{message} validation errors: {string.Join(", ", list)}".Trim());
		}
	}

	public static TException Throws<TException>(Action action)
		where TException : Exception
	{
		try
		{
			action();
		}
		catch (TException expected)
		{
			return expected;
		}

		throw new AssertionException($"expected {typeof(TException).Name} to be thrown");
	}
}
