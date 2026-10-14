using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

namespace VampireSurvivor.Tests;

/// <summary>
/// Headless test runner.
/// Usage: Godot --headless --path . res://tests/run_tests.tscn -- --filter=substring
/// </summary>
public partial class TestRunner : Node
{
	private const double TimeoutSeconds = 10.0;
	private const string TestNamespacePrefix = "VampireSurvivor.Tests.";

	public override async void _Ready()
	{
		string filter = ReadFilter();
		int passed = 0;
		int failed = 0;

		foreach (Type type in DiscoverTestTypes())
		{
			foreach (MethodInfo method in TestMethods(type))
			{
				string label = $"{LabelOf(type)}::{method.Name}";
				if (filter != string.Empty && !label.Contains(filter, StringComparison.Ordinal))
				{
					continue;
				}

				if (await RunOne(type, method, label))
				{
					passed++;
				}
				else
				{
					failed++;
				}
			}
		}

		GD.Print($"---- {passed} passed, {failed} failed ----");
		GetTree().Quit(failed > 0 ? 1 : 0);
	}

	private static string ReadFilter()
	{
		foreach (string argument in OS.GetCmdlineUserArgs())
		{
			if (argument.StartsWith("--filter=", StringComparison.Ordinal))
			{
				return argument["--filter=".Length..];
			}
		}

		return string.Empty;
	}

	private static IEnumerable<Type> DiscoverTestTypes()
	{
		return typeof(TestRunner).Assembly.GetTypes()
			.Where(t => t.IsSubclassOf(typeof(TestCase)) && !t.IsAbstract)
			.OrderBy(t => t.FullName, StringComparer.Ordinal);
	}

	private static IEnumerable<MethodInfo> TestMethods(Type type)
	{
		return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Where(m => m.Name.StartsWith("Test", StringComparison.Ordinal)
				&& m.GetParameters().Length == 0
				&& (m.ReturnType == typeof(void) || m.ReturnType == typeof(Task)))
			.OrderBy(m => m.Name, StringComparer.Ordinal);
	}

	/// <summary>"VampireSurvivor.Tests.Core.WalletTest" -> "Core/WalletTest".</summary>
	private static string LabelOf(Type type)
	{
		string fullName = type.FullName ?? type.Name;
		string relative = fullName.StartsWith(TestNamespacePrefix, StringComparison.Ordinal)
			? fullName[TestNamespacePrefix.Length..]
			: fullName;
		int lastDot = relative.LastIndexOf('.');
		return lastDot < 0 ? relative : relative[..lastDot].Replace('.', '/') + "/" + relative[(lastDot + 1)..];
	}

	/// <summary>Returns true when the test passed.</summary>
	private async Task<bool> RunOne(Type type, MethodInfo method, string label)
	{
		var test = (TestCase)Activator.CreateInstance(type)!;
		test.Host = this;
		string? failure = null;
		try
		{
			test.BeforeEach();
			object? result = method.Invoke(test, null);
			if (result is Task task)
			{
				SceneTreeTimer timer = GetTree().CreateTimer(TimeoutSeconds);
				while (!task.IsCompleted && timer.TimeLeft > 0.0)
				{
					await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				}

				if (task.IsCompleted)
				{
					await task;
				}
				else
				{
					failure = $"timed out after {TimeoutSeconds:0} s";
				}
			}
		}
		catch (TargetInvocationException e)
		{
			failure = Describe(e.InnerException ?? e);
		}
		catch (Exception e)
		{
			failure = Describe(e);
		}
		finally
		{
			try
			{
				test.AfterEach();
			}
			catch (Exception e)
			{
				failure ??= "AfterEach: " + Describe(e);
			}

			test.ReleaseOwnedNodes();
		}

		if (failure is null)
		{
			GD.Print($"PASS  {label}");
			return true;
		}

		GD.Print($"FAIL  {label}\n        - {failure}");
		return false;
	}

	private static string Describe(Exception e)
	{
		return e is AssertionException ? e.Message : $"{e.GetType().Name}: {e.Message}\n{e.StackTrace}";
	}
}
