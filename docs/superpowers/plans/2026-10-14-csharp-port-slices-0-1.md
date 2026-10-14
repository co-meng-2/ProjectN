# C# + Forge Port — Slices 0 and 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the C# project, the C# test runner and the `Core/` port (slice 0), then prove or disprove that Forge can carry this game's combat model (slice 1, the go/no-go gate).

**Architecture:** One C# assembly (`VampireSurvivor`) built from the repo root; the C# tree grows under `port/` beside the untouched GDScript tree. Forge for Godot 0.4.0 is vendored in `addons/forge`; every Forge type the game touches sits behind `Gas/` and `Components/ActorEntity`. Tests are a C# port of the existing `TestCase` + headless runner, run by `tools/run_tests.sh`.

**Tech Stack:** Godot 4.7.2 .NET build, C# net8.0, `Godot.NET.Sdk` 4.7.0 (what the spike built and ran with), Forge for Godot tag `v0.4.0` + NuGet `Gamesmiths.Forge` 0.4.0.

**Spec:** `docs/superpowers/specs/2026-10-14-csharp-forge-port-design.md` (read it first; this plan covers its slices 0 and 1 only. Slices 2-5 get their own plans after the slice 1 gate).

## Global Constraints

- Engine: Godot 4.7.2 **.NET** build; project TFM `net8.0`; the machine only has the .NET 10 runtime, so the csproj sets `<RollForward>Major</RollForward>`.
- Forge is pinned to **0.4.0**: the addon is vendored from the `forge-godot` tag `v0.4.0` (NOT `main`/HEAD, which differ), the NuGet package is `Gamesmiths.Forge` 0.4.0 (via `addons/forge/Forge.props`). 0.4.0 method names are `Activate` / `CommitCooldown` (not `TryActivate` / `TryCommitCooldown`). 0.4.0 has **no public way to revoke a permanently granted ability**: abilities are granted through an INFINITE effect with `GrantAbilityEffectComponent`, and removing that effect revokes the ability.
- One assembly, `VampireSurvivor.csproj` at the repo root, `project/assembly_name="VampireSurvivor"`, `<WarningsAsErrors>nullable</WarningsAsErrors>`. Root namespace `VampireSurvivor`; namespace = folder (`port/src/Core/Wallet.cs` -> `VampireSurvivor.Core`). Test namespaces are `VampireSurvivor.Tests.<Layer>`.
- C# files are PascalCase (`Wallet.cs`); scenes/resources stay snake_case (`actor_entity.tscn`). `.cs.uid` files are committed.
- Layer order (a file may only `using` layers below it): `Core -> Defs -> Gas -> Components -> Actors -> Stage | Abilities | Boss -> Events -> Ui -> App | Meta`. Only `Stage/Stage.cs` may use `Abilities`/`Boss`; `App` and `Meta` may use each other. Project namespaces are never written fully qualified.
- Upward communication: `Node` classes use `[Signal]`, plain classes use C# `event`.
- Tags are lowercase dotted (`ability.boss.charge`). `GameplayTags.cs` is the source; `tools` regenerates `addons/forge/core/forge_data.tres`; that file is the only thing under `addons/` the project writes, and only via the tool.
- Forge `EffectData`/`AbilityData` are built in code and never saved to `.tres`.
- Attribute storage is integer: Health/MaxHealth/MoveSpeed 2 decimal places, Attack and Gold/Stone gain 3. Results truncate toward zero exactly once, in raw integer arithmetic.
- Tests throw on the first failed assertion. Runner selection env var is `RUNNER=gd|cs|both` (not `LANG`, which is a shell locale variable).
- The working tree has **uncommitted user changes in `src/app/logo.gd` and `src/app/main.gd`. Never stage them**; always `git add` explicit paths, never `-A`/`.`.
- Commit messages end with `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>` (pass it as a second `-m`).
- Work happens on branch `port/csharp`.

## Review Focus

Input classes the spec implies but its slice-1 test list does not name (each is pinned by a test in the task shown):

1. Absurd or invalid `StatBlock` values (`MaxHealth <= 0`, `5,000,000`) must be rejected by `Validate()` or clamped, never corrupt an attribute (Task 6).
2. A zero or negative damage amount must never heal the target (Task 7).
3. Damage aimed at an entity that has no vitals attribute set must be a no-op, not an exception (Task 7).
4. A typo'd or unregistered tag must fail loudly at the call site instead of silently producing an empty tag (Task 5).
5. A malformed passive curve (wrong point count or x values) must be reported by `PassiveDef.Validate()` (Task 8).

Also guarded outside the test suite: running the scripts with a non-.NET Godot binary (Task 2 adds a check to `tools/run_tests.sh`).

---

## File Structure

```
VampireSurvivor.csproj                          (T1)
addons/forge/                                   (T1, vendored v0.4.0)
tools/run_tests.sh                              (T1, T2)
port/tests/run_tests.tscn, TestRunner.cs        (T2)
port/tests/Support/{TestCase,Assert}.cs         (T2)
port/tests/Support/{ActorFixture,TestEffects,DefFactory,ProbeAbilityDef}.cs   (T6, T8, T9)
port/src/Core/*.cs                              (T3)
port/src/Gas/{GameplayTags,Stats,StatEffects,PassiveEffects,CooldownEffects,AbilityFactory,DamageEffects}.cs
port/src/Gas/Attributes/{Vitals,Combat,Economy}Attributes.cs
port/src/Gas/Executions/DamageExecution.cs
port/src/Defs/{StatBlock,StatId,PassiveDef,ActiveAbilityDef}.cs
port/src/Components/{ActorEntity,Health}.cs, actor_entity.tscn
port/tools/{SyncTags.cs,sync_tags.tscn}         (T5)
port/tests/{Core,Architecture,Gas,Defs,Components}/*Test.cs
docs/superpowers/notes/2026-10-14-slice1-forge-gate.md   (T11)
```

---

### Task 1: .NET toolchain, Forge vendoring and project scaffold

**Files:**
- Create: `VampireSurvivor.csproj`
- Create: `addons/forge/**` (vendored, its own commit)
- Modify: `project.godot`, `.gitignore`, `tools/run_tests.sh`

**Interfaces:**
- Produces: a buildable `VampireSurvivor.csproj`; the .NET Godot binary at `D:\CM\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe` (bash path `/d/CM/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe`); `tools/run_tests.sh` defaults `GODOT_BIN` to it and builds C# first.

> **User action required before this plan runs (tell the owner):** close the running standard Godot editor and reopen the project with `Godot_v4.7.2-stable_mono_win64.exe` after this task. The standard build cannot load the C# autoload that this task adds.

- [ ] **Step 1: Create the branch**

```bash
git switch -c port/csharp
git status --short
```
Expected: still lists ` M src/app/logo.gd` and ` M src/app/main.gd` (user work, leave unstaged) and the untracked spec/plan docs.

- [ ] **Step 2: Install the .NET Godot build next to the standard one**

```bash
cd /d/CM/Godot
D=$(mktemp -d) && curl -sL -o "$D/godot-mono.zip" https://github.com/godotengine/godot-builds/releases/download/4.7.2-stable/Godot_v4.7.2-stable_mono_win64.zip && unzip -q "$D/godot-mono.zip" -d /d/CM/Godot && rm -rf "$D"
"/d/CM/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe" --version
```
Expected: `4.7.2.stable.mono.official.<hash>`.

- [ ] **Step 3: Record the GDScript baseline with the .NET binary (project still unmodified)**

```bash
cd /d/CM/Godot/vampire-survivor-ai
GODOT_BIN=/d/CM/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe IMPORT=1 tools/run_tests.sh | tail -3
```
Expected: a line `---- N passed, 0 failed ----`. **Write N down** (call it BASELINE); every later GDScript run must show the same N and 0 failed.

- [ ] **Step 4: Vendor Forge for Godot at tag v0.4.0 (own commit)**

```bash
D=$(mktemp -d) && git clone --quiet --depth 1 --branch v0.4.0 https://github.com/gamesmiths-guild/forge-godot.git "$D/forge-godot"
cp -r "$D/forge-godot/addons/forge" addons/forge
grep version addons/forge/plugin.cfg
rm -rf "$D"
git add addons/forge
git commit -m "chore(forge): vendor Forge for Godot v0.4.0" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
Expected: `version="0.4.0"`.

- [ ] **Step 5: Add the csproj**

Create `VampireSurvivor.csproj`:

```xml
<Project Sdk="Godot.NET.Sdk/4.7.0">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <RootNamespace>VampireSurvivor</RootNamespace>
    <AssemblyName>VampireSurvivor</AssemblyName>
    <RollForward>Major</RollForward>
    <WarningsAsErrors>nullable</WarningsAsErrors>
  </PropertyGroup>
  <Import Project="addons\forge\Forge.props" />
</Project>
```

- [ ] **Step 6: Wire `project.godot`**

Run `grep -n "editor_plugins" -A2 project.godot`. Add `"res://addons/forge/plugin.cfg"` to the existing `enabled=PackedStringArray(...)` list (keep godot_ai). Add the autoload line under `[autoload]`:

```
"Forge Bootstrap"="*res://addons/forge/core/ForgeBootstrap.cs"
```
Add a new section (before `[editor_plugins]`):

```
[dotnet]

project/assembly_name="VampireSurvivor"
```
In `[application]` change `config/features` to include `"C#"` (for example `PackedStringArray("4.7", "C#", "Forward Plus")`).

- [ ] **Step 7: Update `.gitignore`**

Replace the line `# Mono / C# (unused, kept for safety)` with `# Mono / C#`, and append under that block:

```
bin/
obj/
*.csproj.user
```

- [ ] **Step 8: Build**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q; echo "exit=$?"
```
Expected: `exit=0`. If the addon at v0.4.0 fails to compile against NuGet 0.4.0, **stop and report the compiler errors**; do not patch `addons/forge`.

- [ ] **Step 9: Update `tools/run_tests.sh` (default binary + build step)**

Replace the first lines (`GODOT_BIN=...` default) so the default is the .NET binary and add the build step before the import:

```bash
GODOT_BIN="${GODOT_BIN:-/d/CM/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe}"
if [ "${NO_BUILD:-0}" != "1" ]; then
	if ! build_output="$(dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1)"; then
		echo "$build_output" | grep "error" | sort -u
		echo "!! dotnet build failed"
		exit 1
	fi
fi
```
(keep the existing `IMPORT=1` block and the rest unchanged; Task 2 rewrites the file fully.)

- [ ] **Step 10: Verify the GDScript suite is unchanged with C# present**

```bash
IMPORT=1 tools/run_tests.sh | tail -3
```
Expected: `---- BASELINE passed, 0 failed ----` and no `!! script errors detected`.

- [ ] **Step 11: Commit (explicit paths only)**

```bash
git add VampireSurvivor.csproj project.godot .gitignore tools/run_tests.sh
git commit -m "build: add C# project, .NET Godot binary and Forge autoload" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
git status --short
```
Expected: `src/app/logo.gd` and `src/app/main.gd` still modified-unstaged.

---

### Task 2: C# test runner (`TestCase`, `Assert`, `TestRunner`, `RUNNER` switch)

**Files:**
- Create: `port/tests/Support/TestCase.cs`, `port/tests/Support/Assert.cs`, `port/tests/TestRunner.cs`, `port/tests/run_tests.tscn`
- Test: `port/tests/Support/RunnerSelfTest.cs`
- Modify: `tools/run_tests.sh` (full rewrite)

**Interfaces:**
- Produces: `VampireSurvivor.Tests.TestCase` (`Host`, `Adopt<T>`, `NextFrame()`, `NextPhysicsFrame()`, `Settle()`, `WaitSeconds(double)`, virtual `BeforeEach/AfterEach`); `VampireSurvivor.Tests.Assert` (`True`, `False`, `Eq<T>`, `SequenceEq<T>`, `AlmostEq`, `Null`, `NotNull`, `NoErrors`, `Throws<T>`), `AssertionException`; runner scene `res://port/tests/run_tests.tscn`.

- [ ] **Step 1: Write the self-test (it cannot compile yet)**

Create `port/tests/Support/RunnerSelfTest.cs`:

```csharp
using System;
using System.Threading.Tasks;
using Godot;

namespace VampireSurvivor.Tests.Support;

public class RunnerSelfTest : TestCase
{
	public void TestPassingSyncTest()
	{
		Assert.Eq(1 + 1, 2);
	}

	public async Task TestAsyncTestsAreAwaited()
	{
		await WaitSeconds(0.05);
		Assert.True(true);
	}

	public void TestAdoptedNodesAreInTheTreeAndFreedAfterwards()
	{
		var node = Adopt(new Node());
		Assert.True(node.IsInsideTree());
	}

	public void TestFailedAssertionsThrowAssertionException()
	{
		Assert.Throws<AssertionException>(() => Assert.Eq(1, 2));
		Assert.Throws<AssertionException>(() => Assert.True(false));
		Assert.Throws<AssertionException>(() => Assert.AlmostEq(1.0, 2.0));
	}

	public void TestSequenceEqComparesElements()
	{
		Assert.SequenceEq(new[] { 1, 2, 3 }, new[] { 1, 2, 3 });
		Assert.Throws<AssertionException>(() => Assert.SequenceEq(new[] { 1 }, new[] { 2 }));
	}

	public void TestNoErrorsAcceptsEmptyAndRejectsMessages()
	{
		Assert.NoErrors(Array.Empty<string>());
		Assert.Throws<AssertionException>(() => Assert.NoErrors(new[] { "bad" }));
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
NO_BUILD=0 dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: a count greater than 0 (`TestCase`/`Assert` do not exist).

- [ ] **Step 3: Implement `Assert`**

Create `port/tests/Support/Assert.cs`:

```csharp
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
```

- [ ] **Step 4: Implement `TestCase`**

Create `port/tests/Support/TestCase.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace VampireSurvivor.Tests;

/// <summary>
/// Base class for headless tests. Subclasses add public <c>Test*</c> methods returning void or Task.
/// The runner sets <see cref="Host"/> to a node inside the SceneTree before each test.
/// </summary>
public abstract class TestCase
{
	private readonly List<Node> _owned = [];

	public Node Host { get; set; } = null!;

	public virtual void BeforeEach()
	{
	}

	public virtual void AfterEach()
	{
	}

	/// <summary>Adds the node under <see cref="Host"/> and frees it automatically after the test.</summary>
	public T Adopt<T>(T node)
		where T : Node
	{
		Host.AddChild(node);
		_owned.Add(node);
		return node;
	}

	public void ReleaseOwnedNodes()
	{
		foreach (Node node in _owned)
		{
			if (GodotObject.IsInstanceValid(node))
			{
				node.Free();
			}
		}

		_owned.Clear();
	}

	public async Task NextFrame()
	{
		await Host.ToSignal(Host.GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	public async Task NextPhysicsFrame()
	{
		await Host.ToSignal(Host.GetTree(), SceneTree.SignalName.PhysicsFrame);
	}

	/// <summary>Awaits two physics frames (enough for newly added areas to report overlaps).</summary>
	public async Task Settle()
	{
		await NextPhysicsFrame();
		await NextPhysicsFrame();
	}

	public async Task WaitSeconds(double seconds)
	{
		await Host.ToSignal(Host.GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	}
}
```

- [ ] **Step 5: Implement `TestRunner` and its scene**

Create `port/tests/TestRunner.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

namespace VampireSurvivor.Tests;

/// <summary>
/// Headless test runner.
/// Usage: Godot --headless --path . res://port/tests/run_tests.tscn -- --filter=substring
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
```

Create `port/tests/run_tests.tscn`:

```
[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://port/tests/TestRunner.cs" id="1_runner"]

[node name="RunTests" type="Node"]
script = ExtResource("1_runner")
```

- [ ] **Step 6: Rewrite `tools/run_tests.sh`**

```bash
#!/usr/bin/env bash
# Runs the headless test suites.
# Env: GODOT_BIN, RUNNER (gd|cs|both, default both), FILTER (label/path substring), IMPORT=1 (run --import first),
#      NO_BUILD=1 (skip dotnet build), TEST_SUITE_TIMEOUT (seconds, default 120).
set -u
cd "$(dirname "$0")/.."
GODOT_BIN="${GODOT_BIN:-/d/CM/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe}"
RUNNER="${RUNNER:-both}"
timeout_seconds="${TEST_SUITE_TIMEOUT:-120}"

if ! "$GODOT_BIN" --version 2>/dev/null | grep -q "mono"; then
	echo "!! GODOT_BIN is not a .NET (mono) build: $GODOT_BIN"
	exit 1
fi
if [ "${NO_BUILD:-0}" != "1" ]; then
	if ! build_output="$(dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1)"; then
		echo "$build_output" | grep "error" | sort -u
		echo "!! dotnet build failed"
		exit 1
	fi
fi
if [ "${IMPORT:-0}" = "1" ]; then
	"$GODOT_BIN" --headless --path . --import >/dev/null 2>&1
fi

# run_suite <scene> <label>
run_suite() {
	local output status
	output="$(timeout "$timeout_seconds" "$GODOT_BIN" --headless --path . "$1" -- --filter="${FILTER:-}" 2>&1)"
	status=$?
	echo "$output" | grep -v "ObjectDB instances were leaked\|resources still in use\|at: cleanup\|at: clear\|BUG: Unreferenced static string\|\[godot_ai game_helper\]"
	if [ "$status" -eq 124 ]; then
		echo "!! $2 test run timed out after ${timeout_seconds}s (parse error or hang?)"
		return 124
	fi
	if echo "$output" | grep -q "SCRIPT ERROR\|Parse Error\|Compile Error\|Unhandled exception"; then
		echo "!! script errors detected in $2 suite"
		return 1
	fi
	return $status
}

overall=0
case "$RUNNER" in
	gd | both)
		echo "== GDScript suite"
		run_suite res://tests/run_tests.tscn gd || overall=1
		;;
esac
case "$RUNNER" in
	cs | both)
		echo "== C# suite"
		run_suite res://port/tests/run_tests.tscn cs || overall=1
		;;
esac
exit $overall
```

- [ ] **Step 7: Run the C# suite**

```bash
RUNNER=cs IMPORT=1 tools/run_tests.sh
```
Expected: six `PASS  Support/RunnerSelfTest::...` lines and `---- 6 passed, 0 failed ----`.

- [ ] **Step 8: Prove failures are detected, then remove the probe**

Create `port/tests/Support/ZzDeliberateFailureTest.cs`:

```csharp
namespace VampireSurvivor.Tests.Support;

public class ZzDeliberateFailureTest : TestCase
{
	public void TestFails()
	{
		Assert.Eq(1, 2, "deliberate");
	}
}
```
Run `RUNNER=cs FILTER=ZzDeliberate tools/run_tests.sh; echo "exit=$?"`.
Expected: `FAIL  Support/ZzDeliberateFailureTest::TestFails`, `- deliberate (expected 2, got 1)`, `---- 0 passed, 1 failed ----`, `exit=1`.
Then delete the file: `rm port/tests/Support/ZzDeliberateFailureTest.cs*`.

- [ ] **Step 9: Both suites together**

```bash
tools/run_tests.sh | grep -E "^==|^----"
```
Expected: `== GDScript suite`, `---- BASELINE passed, 0 failed ----`, `== C# suite`, `---- 6 passed, 0 failed ----`.

- [ ] **Step 10: Commit**

```bash
git add port/tests tools/run_tests.sh
git commit -m "test: C# TestCase, Assert and headless runner with RUNNER switch" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Port `Core/` with its six test files

**Files:**
- Create: `port/src/Core/{Wallet,Price,Grade,Timeline,RunRng,SpawnGeometry,PhysicsLayers,Groups,InputActions,AnimNames}.cs`
- Test: `port/tests/Core/{WalletTest,GradeTest,TimelineTest,RunRngTest,SpawnGeometryTest,PhysicsLayersTest}.cs`

**Interfaces:**
- Produces (namespace `VampireSurvivor.Core`): `enum CurrencyKind { Gold, Stone, Ticket }`; `Wallet` (`Gold`, `Stone`, `Tickets`, `Balance(CurrencyKind)`, `Add(CurrencyKind,int)`, `TrySpend(CurrencyKind,int)`, `event Action<int,int,int>? Changed`); `readonly record struct Price(CurrencyKind Kind, int Amount)` with `IsAffordable(Wallet)`; `enum Grade { None, Common, Rare, Epic, Unique, Legendary, Revealed }` + `GradeExtensions` (`Next`, `IsRollable`, `Level`, `DisplayName`, `Tint`); `Timeline(float duration, float[]? marks)` (`Duration`, `Marks`, `Progress`, `NextAfter`, `Crossed`); `RunRng(long seed = 0)` (`SeedValue`, `Float`, `Range`, `Int`, `Chance`, `Pick<T>`, `WeightedIndex`, `Weighted<T>`, `OffsetInCircle`); `SpawnGeometry.RingPoint/ClampToRect`; constants classes `PhysicsLayers`, `Groups`, `InputActions`, `AnimNames`.
- Legacy: `src/core/*.gd`, `tests/core/test_*.gd` (same behaviour, test for test).

- [ ] **Step 1: Write the six test files**

`port/tests/Core/WalletTest.cs`:

```csharp
using System.Collections.Generic;
using VampireSurvivor.Core;

namespace VampireSurvivor.Tests.Core;

public class WalletTest : TestCase
{
	public void TestAddIncreasesBalanceAndEmitsChanged()
	{
		var wallet = new Wallet();
		var emitted = new List<(int, int, int)>();
		wallet.Changed += (gold, stone, tickets) => emitted.Add((gold, stone, tickets));
		wallet.Add(CurrencyKind.Gold, 5);
		wallet.Add(CurrencyKind.Stone, 2);
		Assert.Eq(wallet.Gold, 5);
		Assert.Eq(wallet.Stone, 2);
		Assert.SequenceEq(emitted, new[] { (5, 0, 0), (5, 2, 0) });
	}

	public void TestAddIgnoresNonPositiveAmounts()
	{
		var wallet = new Wallet();
		wallet.Add(CurrencyKind.Gold, 0);
		wallet.Add(CurrencyKind.Gold, -3);
		Assert.Eq(wallet.Gold, 0);
	}

	public void TestTrySpendSucceedsWhenAffordable()
	{
		var wallet = new Wallet();
		wallet.Add(CurrencyKind.Gold, 10);
		Assert.True(wallet.TrySpend(CurrencyKind.Gold, 4));
		Assert.Eq(wallet.Balance(CurrencyKind.Gold), 6);
	}

	public void TestTrySpendFailsWithoutChangingBalance()
	{
		var wallet = new Wallet();
		wallet.Add(CurrencyKind.Stone, 1);
		Assert.False(wallet.TrySpend(CurrencyKind.Stone, 2));
		Assert.False(wallet.TrySpend(CurrencyKind.Stone, -1));
		Assert.Eq(wallet.Stone, 1);
	}

	public void TestTicketsAreAThirdBalance()
	{
		var wallet = new Wallet();
		var emitted = new List<(int, int, int)>();
		wallet.Changed += (gold, stone, tickets) => emitted.Add((gold, stone, tickets));
		wallet.Add(CurrencyKind.Ticket, 2);
		Assert.Eq(wallet.Tickets, 2);
		Assert.Eq(wallet.Balance(CurrencyKind.Ticket), 2);
		Assert.Eq(wallet.Gold, 0, "tickets are not gold");
		Assert.True(wallet.TrySpend(CurrencyKind.Ticket, 1));
		Assert.False(wallet.TrySpend(CurrencyKind.Ticket, 2), "only one left");
		Assert.Eq(wallet.Tickets, 1);
		Assert.SequenceEq(emitted, new[] { (0, 0, 2), (0, 0, 1) });
	}

	public void TestPriceIsAffordableComparesTheMatchingBalance()
	{
		var wallet = new Wallet();
		wallet.Add(CurrencyKind.Gold, 5);
		Assert.True(new Price(CurrencyKind.Gold, 5).IsAffordable(wallet));
		Assert.False(new Price(CurrencyKind.Gold, 6).IsAffordable(wallet));
		Assert.False(new Price(CurrencyKind.Stone, 1).IsAffordable(wallet));
	}
}
```

`port/tests/Core/GradeTest.cs`:

```csharp
using System;
using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;

namespace VampireSurvivor.Tests.Core;

public class GradeTest : TestCase
{
	public void TestNextClimbsTheLadderAndStopsAtRevealed()
	{
		Assert.Eq(Grade.None.Next(), Grade.Common);
		Assert.Eq(Grade.Unique.Next(), Grade.Legendary);
		Assert.Eq(Grade.Legendary.Next(), Grade.Revealed);
		Assert.Eq(Grade.Revealed.Next(), Grade.Revealed);
	}

	public void TestLevelMapsCommonToOneAndRevealedToSix()
	{
		Assert.AlmostEq(Grade.None.Level(), 0.0);
		Assert.AlmostEq(Grade.Common.Level(), 1.0);
		Assert.AlmostEq(Grade.Epic.Level(), 3.0);
		Assert.AlmostEq(Grade.Revealed.Level(), 6.0);
	}

	public void TestIsRollableExcludesNoneAndRevealed()
	{
		Assert.False(Grade.None.IsRollable());
		Assert.True(Grade.Common.IsRollable());
		Assert.True(Grade.Legendary.IsRollable());
		Assert.False(Grade.Revealed.IsRollable());
	}

	public void TestEveryGradeHasANameAndADistinctColor()
	{
		var colors = new List<Color>();
		foreach (Grade grade in Enum.GetValues<Grade>())
		{
			Assert.False(grade.DisplayName().Length == 0, $"name for {grade}");
			Assert.False(colors.Contains(grade.Tint()), $"colour reused by {grade.DisplayName()}");
			colors.Add(grade.Tint());
		}

		Assert.Eq(colors.Count, 7);
	}
}
```

`port/tests/Core/TimelineTest.cs`:

```csharp
using VampireSurvivor.Core;

namespace VampireSurvivor.Tests.Core;

public class TimelineTest : TestCase
{
	public void TestMarksAreSorted()
	{
		var timeline = new Timeline(100f, [60f, 20f, 40f]);
		Assert.SequenceEq(timeline.Marks, new[] { 20f, 40f, 60f });
	}

	public void TestProgressIsClamped()
	{
		var timeline = new Timeline(100f);
		Assert.AlmostEq(timeline.Progress(25f), 0.25);
		Assert.AlmostEq(timeline.Progress(-5f), 0.0);
		Assert.AlmostEq(timeline.Progress(500f), 1.0);
	}

	public void TestProgressWithZeroDurationIsComplete()
	{
		Assert.AlmostEq(new Timeline(0f).Progress(3f), 1.0);
	}

	public void TestNextAfterSkipsCurrentAndPastMarks()
	{
		var timeline = new Timeline(100f, [20f, 40f]);
		Assert.AlmostEq(timeline.NextAfter(0f), 20.0);
		Assert.AlmostEq(timeline.NextAfter(20f), 40.0);
		Assert.AlmostEq(timeline.NextAfter(40f), -1.0);
	}

	public void TestCrossedExcludesStartAndIncludesEnd()
	{
		var timeline = new Timeline(100f, [20f, 40f, 60f]);
		Assert.SequenceEq(timeline.Crossed(20f, 40f), new[] { 1 });
		Assert.SequenceEq(timeline.Crossed(0f, 100f), new[] { 0, 1, 2 });
		Assert.SequenceEq(timeline.Crossed(41f, 59f), System.Array.Empty<int>());
	}
}
```

`port/tests/Core/RunRngTest.cs`:

```csharp
using System;
using VampireSurvivor.Core;

namespace VampireSurvivor.Tests.Core;

public class RunRngTest : TestCase
{
	public void TestSameSeedGivesSameSequence()
	{
		var a = new RunRng(42);
		var b = new RunRng(42);
		for (int i = 0; i < 5; i++)
		{
			Assert.Eq(a.Float(), b.Float(), $"draw {i}");
		}
	}

	public void TestPickReturnsNullForEmptyArray()
	{
		Assert.Null(new RunRng(1).Pick(Array.Empty<string>()));
	}

	public void TestPickReturnsAnElement()
	{
		string[] items = ["a", "b", "c"];
		Assert.True(Array.IndexOf(items, new RunRng(1).Pick(items)) >= 0);
	}

	public void TestWeightedIndexNeverPicksZeroWeight()
	{
		var rng = new RunRng(7);
		float[] weights = [0f, 0f, 5f, 0f];
		for (int i = 0; i < 30; i++)
		{
			Assert.Eq(rng.WeightedIndex(weights), 2);
		}
	}

	public void TestWeightedIndexIsMinusOneWithoutPositiveWeight()
	{
		Assert.Eq(new RunRng(1).WeightedIndex([0f, -1f]), -1);
		Assert.Eq(new RunRng(1).WeightedIndex(Array.Empty<float>()), -1);
	}

	public void TestWeightedReturnsMatchingItem()
	{
		Assert.Eq(new RunRng(3).Weighted(["x", "y"], [0f, 1f]), "y");
	}

	public void TestChanceExtremes()
	{
		var rng = new RunRng(9);
		for (int i = 0; i < 20; i++)
		{
			Assert.False(rng.Chance(0f));
			Assert.True(rng.Chance(1f));
		}
	}

	public void TestOffsetInCircleStaysWithinRadius()
	{
		var rng = new RunRng(5);
		for (int i = 0; i < 50; i++)
		{
			Assert.True(rng.OffsetInCircle(10f).Length() <= 10.0001f);
		}
	}
}
```

`port/tests/Core/SpawnGeometryTest.cs`:

```csharp
using Godot;
using VampireSurvivor.Core;

namespace VampireSurvivor.Tests.Core;

public class SpawnGeometryTest : TestCase
{
	public void TestRingPointIsRadiusAway()
	{
		Vector2 point = SpawnGeometry.RingPoint(new Vector2(10, 10), 50f, 0f);
		Assert.AlmostEq(point.X, 60.0);
		Assert.AlmostEq(point.Y, 10.0);
	}

	public void TestClampToRectRespectsMargin()
	{
		var bounds = new Rect2(-100, -100, 200, 200);
		Assert.Eq(SpawnGeometry.ClampToRect(new Vector2(500, 0), bounds, 10f), new Vector2(90, 0));
		Assert.Eq(SpawnGeometry.ClampToRect(new Vector2(0, -500), bounds), new Vector2(0, -100));
		Assert.Eq(SpawnGeometry.ClampToRect(new Vector2(5, 5), bounds), new Vector2(5, 5));
	}
}
```

`port/tests/Core/PhysicsLayersTest.cs`:

```csharp
using VampireSurvivor.Core;

namespace VampireSurvivor.Tests.Core;

public class PhysicsLayersTest : TestCase
{
	public void TestLayersAreDistinctSingleBits()
	{
		int[] layers =
		[
			PhysicsLayers.World, PhysicsLayers.AllyBody, PhysicsLayers.EnemyBody,
			PhysicsLayers.AllyHurt, PhysicsLayers.EnemyHurt, PhysicsLayers.Pickup,
		];
		int combined = 0;
		foreach (int layer in layers)
		{
			Assert.Eq(layer & (layer - 1), 0, "single bit");
			Assert.Eq(combined & layer, 0, "overlap");
			combined |= layer;
		}
	}
}
```

- [ ] **Step 2: Run to see the build fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: count greater than 0 (`Wallet` etc. do not exist).

- [ ] **Step 3: Implement the Core classes**

`port/src/Core/Wallet.cs`:

```csharp
using System;

namespace VampireSurvivor.Core;

public enum CurrencyKind
{
	Gold,
	Stone,
	Ticket,
}

/// <summary>
/// Per-run balances: gold, stone and promotion tickets (paid by stage events; one ticket pays any shop action).
/// Gem is persistent meta currency and lives elsewhere.
/// </summary>
public sealed class Wallet
{
	public event Action<int, int, int>? Changed;

	public int Gold { get; private set; }

	public int Stone { get; private set; }

	public int Tickets { get; private set; }

	public int Balance(CurrencyKind kind) => kind switch
	{
		CurrencyKind.Gold => Gold,
		CurrencyKind.Stone => Stone,
		_ => Tickets,
	};

	public void Add(CurrencyKind kind, int amount)
	{
		if (amount <= 0)
		{
			return;
		}

		Apply(kind, Balance(kind) + amount);
	}

	public bool TrySpend(CurrencyKind kind, int amount)
	{
		if (amount < 0 || Balance(kind) < amount)
		{
			return false;
		}

		Apply(kind, Balance(kind) - amount);
		return true;
	}

	private void Apply(CurrencyKind kind, int value)
	{
		switch (kind)
		{
			case CurrencyKind.Gold:
				Gold = value;
				break;
			case CurrencyKind.Stone:
				Stone = value;
				break;
			default:
				Tickets = value;
				break;
		}

		Changed?.Invoke(Gold, Stone, Tickets);
	}
}
```

`port/src/Core/Price.cs`:

```csharp
namespace VampireSurvivor.Core;

/// <summary>What one shop action costs: an amount of a single currency.</summary>
public readonly record struct Price(CurrencyKind Kind, int Amount)
{
	public bool IsAffordable(Wallet wallet) => wallet.Balance(Kind) >= Amount;
}
```

`port/src/Core/Grade.cs`:

```csharp
using Godot;

namespace VampireSurvivor.Core;

/// <summary>
/// The quality ladder of an ability. Revealed is the enhanced form of the same Legendary ability, so only
/// Common..Legendary are rolled from pools. <see cref="GradeExtensions.Level"/> is the Forge ability level and curve X.
/// </summary>
public enum Grade
{
	None,
	Common,
	Rare,
	Epic,
	Unique,
	Legendary,
	Revealed,
}

public static class GradeExtensions
{
	private static readonly string[] Names = ["Empty", "Common", "Rare", "Epic", "Unique", "Legendary", "Revealed"];

	private static readonly Color[] Tints =
	[
		new(0.35f, 0.35f, 0.38f), // None
		new(0.80f, 0.80f, 0.80f), // Common
		new(0.30f, 0.60f, 1.00f), // Rare
		new(0.70f, 0.35f, 1.00f), // Epic
		new(1.00f, 0.55f, 0.20f), // Unique
		new(1.00f, 0.85f, 0.25f), // Legendary
		new(0.55f, 1.00f, 0.95f), // Revealed
	];

	public static Grade Next(this Grade grade) => grade == Grade.Revealed ? Grade.Revealed : grade + 1;

	public static bool IsRollable(this Grade grade) => grade != Grade.None && grade != Grade.Revealed;

	public static float Level(this Grade grade) => (float)grade;

	public static string DisplayName(this Grade grade) => Names[(int)grade];

	public static Color Tint(this Grade grade) => Tints[(int)grade];
}
```

`port/src/Core/Timeline.cs`:

```csharp
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
```

`port/src/Core/RunRng.cs`:

```csharp
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
```

`port/src/Core/SpawnGeometry.cs`:

```csharp
using Godot;

namespace VampireSurvivor.Core;

/// <summary>Pure helpers for choosing spawn positions.</summary>
public static class SpawnGeometry
{
	public static Vector2 RingPoint(Vector2 center, float radius, float angle)
	{
		return center + (Vector2.Right.Rotated(angle) * radius);
	}

	public static Vector2 ClampToRect(Vector2 point, Rect2 bounds, float margin = 0f)
	{
		Rect2 inner = bounds.Grow(-margin);
		return new Vector2(
			Mathf.Clamp(point.X, inner.Position.X, inner.End.X),
			Mathf.Clamp(point.Y, inner.Position.Y, inner.End.Y));
	}
}
```

`port/src/Core/PhysicsLayers.cs`:

```csharp
namespace VampireSurvivor.Core;

/// <summary>2D physics layer bitmasks. Names must match [layer_names] in project settings.</summary>
public static class PhysicsLayers
{
	public const int World = 1 << 0;
	public const int AllyBody = 1 << 1;
	public const int EnemyBody = 1 << 2;
	public const int AllyHurt = 1 << 3;
	public const int EnemyHurt = 1 << 4;
	public const int Pickup = 1 << 5;
}
```

`port/src/Core/Groups.cs`:

```csharp
using Godot;

namespace VampireSurvivor.Core;

/// <summary>Scene-tree group names. Used only for targeting queries.</summary>
public static class Groups
{
	public static readonly StringName Enemies = "enemies";
	public static readonly StringName Player = "player";
	public static readonly StringName Building = "building";
}
```

`port/src/Core/InputActions.cs`:

```csharp
using Godot;

namespace VampireSurvivor.Core;

/// <summary>Input action names. Bindings live in the project's InputMap.</summary>
public static class InputActions
{
	public static readonly StringName MoveLeft = "move_left";
	public static readonly StringName MoveRight = "move_right";
	public static readonly StringName MoveUp = "move_up";
	public static readonly StringName MoveDown = "move_down";
	public static readonly StringName Dash = "dash";
	public static readonly StringName Restart = "restart";

	/// <summary>Enter / KP Enter only. Not ui_accept: that also fires on Space, which is the dash key.</summary>
	public static readonly StringName Confirm = "continue_run";

	public static Vector2 MoveVector() => Input.GetVector(MoveLeft, MoveRight, MoveUp, MoveDown);
}
```

`port/src/Core/AnimNames.cs`:

```csharp
using Godot;

namespace VampireSurvivor.Core;

/// <summary>Animation names shared by visual definitions and ActorVisual.</summary>
public static class AnimNames
{
	public static readonly StringName Idle = "idle";
	public static readonly StringName Walk = "walk";
	public static readonly StringName Attack = "attack";
	public static readonly StringName Hurt = "hurt";
	public static readonly StringName Death = "death";
}
```

- [ ] **Step 4: Run the C# suite**

```bash
RUNNER=cs tools/run_tests.sh | tail -4
```
Expected: `---- 32 passed, 0 failed ----` (6 runner self-tests + 6 wallet + 4 grade + 5 timeline + 8 run-rng + 2 spawn + 1 physics-layers).

- [ ] **Step 5: Commit**

```bash
git add port/src/Core port/tests/Core
git commit -m "feat(core): port Core to C# with its tests" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Layer-rule architecture test

**Files:**
- Create: `port/tests/Architecture/LayerRules.cs`
- Test: `port/tests/Architecture/LayerRulesTest.cs`

**Interfaces:**
- Produces: `VampireSurvivor.Tests.Architecture.LayerRules.FindViolations(string relativePath, string source) : IReadOnlyList<string>` (empty = OK). `relativePath` is relative to `port/src`, for example `Components/Health.cs`.

- [ ] **Step 1: Write the failing test**

`port/tests/Architecture/LayerRulesTest.cs`:

```csharp
using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.Tests.Architecture;

public class LayerRulesTest : TestCase
{
	private const string SourceRoot = "res://port/src";

	public void TestDownwardUsingsAreAllowed()
	{
		const string source = "using VampireSurvivor.Core;\nusing VampireSurvivor.Gas;\n";
		Assert.NoErrors(LayerRules.FindViolations("Components/Health.cs", source));
	}

	public void TestUpwardUsingsAreViolations()
	{
		const string source = "using VampireSurvivor.Actors;\n";
		IReadOnlyList<string> violations = LayerRules.FindViolations("Components/Health.cs", source);
		Assert.Eq(violations.Count, 1);
		Assert.True(violations[0].Contains("Components", System.StringComparison.Ordinal)
			&& violations[0].Contains("Actors", System.StringComparison.Ordinal));
	}

	public void TestOnlyStageFileMayUseAbilitiesAndBoss()
	{
		const string source = "using VampireSurvivor.Abilities;\nusing VampireSurvivor.Boss;\n";
		Assert.NoErrors(LayerRules.FindViolations("Stage/Stage.cs", source));
		Assert.Eq(LayerRules.FindViolations("Stage/WaveDirector.cs", source).Count, 2);
	}

	public void TestStageNeverUsesEvents()
	{
		Assert.Eq(LayerRules.FindViolations("Stage/Stage.cs", "using VampireSurvivor.Events;\n").Count, 1);
	}

	public void TestAppAndMetaMayUseEachOther()
	{
		Assert.NoErrors(LayerRules.FindViolations("App/Main.cs", "using VampireSurvivor.Meta;\n"));
		Assert.NoErrors(LayerRules.FindViolations("Meta/Hub/Hub.cs", "using VampireSurvivor.App;\n"));
	}

	public void TestFullyQualifiedProjectNamesAreViolations()
	{
		const string source = "namespace VampireSurvivor.Components;\nclass A { VampireSurvivor.Core.Wallet W = new(); }\n";
		Assert.Eq(LayerRules.FindViolations("Components/A.cs", source).Count, 1);
	}

	public void TestNonProjectUsingsAreIgnored()
	{
		const string source = "using System;\nusing Godot;\nusing Gamesmiths.Forge.Effects;\n";
		Assert.NoErrors(LayerRules.FindViolations("Core/X.cs", source));
	}

	public void TestSourceTreeHasNoViolations()
	{
		var all = new List<string>();
		foreach (string path in SourceFiles(SourceRoot))
		{
			string relative = path[(SourceRoot.Length + 1)..];
			all.AddRange(LayerRules.FindViolations(relative, FileAccess.GetFileAsString(path)));
		}

		Assert.NoErrors(all);
	}

	private static IEnumerable<string> SourceFiles(string directory)
	{
		DirAccess? access = DirAccess.Open(directory);
		if (access is null)
		{
			yield break;
		}

		foreach (string sub in access.GetDirectories())
		{
			foreach (string file in SourceFiles($"{directory}/{sub}"))
			{
				yield return file;
			}
		}

		foreach (string file in access.GetFiles())
		{
			if (file.EndsWith(".cs", System.StringComparison.Ordinal))
			{
				yield return $"{directory}/{file}";
			}
		}
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: count greater than 0 (`LayerRules` missing).

- [ ] **Step 3: Implement `LayerRules`**

`port/tests/Architecture/LayerRules.cs`:

```csharp
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VampireSurvivor.Tests.Architecture;

/// <summary>
/// The layer table as code. A file may only use layers with a lower rank; the few allowed exceptions are listed
/// explicitly. Reads <c>using VampireSurvivor.X;</c> lines, so it needs no extra dependency.
/// </summary>
public static partial class LayerRules
{
	private static readonly Dictionary<string, int> Rank = new()
	{
		["Core"] = 0,
		["Defs"] = 1,
		["Gas"] = 2,
		["Components"] = 3,
		["Actors"] = 4,
		["Stage"] = 5,
		["Abilities"] = 5,
		["Boss"] = 5,
		["Events"] = 6,
		["Ui"] = 7,
		["App"] = 8,
		["Meta"] = 8,
	};

	public static IReadOnlyList<string> FindViolations(string relativePath, string source)
	{
		var violations = new List<string>();
		string ownLayer = relativePath.Split('/')[0];
		if (!Rank.TryGetValue(ownLayer, out int ownRank))
		{
			return violations;
		}

		foreach (Match match in UsingPattern().Matches(source))
		{
			string target = match.Groups[1].Value;
			if (!Rank.TryGetValue(target, out int targetRank) || target == ownLayer)
			{
				continue;
			}

			bool allowed = targetRank < ownRank
				|| (relativePath == "Stage/Stage.cs" && (target == "Abilities" || target == "Boss"))
				|| (ownLayer == "App" && target == "Meta")
				|| (ownLayer == "Meta" && target == "App");
			if (!allowed)
			{
				violations.Add($"{relativePath}: layer {ownLayer} must not use {target}");
			}
		}

		foreach (string line in source.Split('\n'))
		{
			string trimmed = line.TrimStart();
			if (trimmed.StartsWith("using ", System.StringComparison.Ordinal)
				|| trimmed.StartsWith("namespace ", System.StringComparison.Ordinal))
			{
				continue;
			}

			if (QualifiedPattern().IsMatch(line))
			{
				violations.Add($"{relativePath}: write a using, not a fully qualified VampireSurvivor name: {trimmed}");
			}
		}

		return violations;
	}

	[GeneratedRegex(@"^\s*using\s+VampireSurvivor\.(\w+)", RegexOptions.Multiline)]
	private static partial Regex UsingPattern();

	[GeneratedRegex(@"\bVampireSurvivor\.\w+\.")]
	private static partial Regex QualifiedPattern();
}
```

- [ ] **Step 4: Run**

```bash
RUNNER=cs tools/run_tests.sh | grep -E "Architecture|^----"
```
Expected: eight `PASS  Architecture/LayerRulesTest::...` lines and a final `0 failed`.

- [ ] **Step 5: Commit**

```bash
git add port/tests/Architecture
git commit -m "test: layer-rule architecture test" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 5: `GameplayTags` and the tag sync tool

**Files:**
- Create: `port/src/Gas/GameplayTags.cs`, `port/tools/SyncTags.cs`, `port/tools/sync_tags.tscn`
- Modify: `addons/forge/core/forge_data.tres` (generated by the tool, nothing else under `addons/`)
- Test: `port/tests/Gas/GameplayTagsTest.cs`

**Interfaces:**
- Produces (namespace `VampireSurvivor.Gas`): `static class GameplayTags` with `const string` tags (`AbilityArrow`, `AbilityBossBurst`, `AbilityBossCharge`, `AbilityBossSlam`, `AbilityBossSummon`, `AbilityBounty`, `AbilityDash`, `AbilitySweep`, `AbilityVolley`, `AbilityProbe`, `CooldownArrow`, `CooldownDash`, `CooldownVolley`, `CooldownSweep`, `CooldownProbe`, `DamageAmount`, `EventKill`, `StateCasting`, `StateInvulnerable`), `static readonly string[] All`, `IEnumerable<string> WithParents(IEnumerable<string>)`, `string CooldownFor(string abilityTag)`, `Tag Request(string tag)` (throws `ArgumentException` for unregistered tags), `TagContainer ContainerOf(string tag)`.
- `AbilityProbe`/`CooldownProbe` exist only for tests (documented in the file).

- [ ] **Step 1: Write the failing test**

`port/tests/Gas/GameplayTagsTest.cs`:

```csharp
using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Gamesmiths.Forge.Godot.Core;
using Gamesmiths.Forge.Tags;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Tests.Gas;

public class GameplayTagsTest : TestCase
{
	public void TestEveryConstantIsListedInAll()
	{
		string[] constants = typeof(GameplayTags)
			.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Where(f => f.IsLiteral && f.FieldType == typeof(string))
			.Select(f => (string)f.GetRawConstantValue()!)
			.OrderBy(s => s, StringComparer.Ordinal)
			.ToArray();
		Assert.SequenceEq(GameplayTags.All.OrderBy(s => s, StringComparer.Ordinal), constants);
	}

	public void TestAllTagsAreLowercaseDotted()
	{
		foreach (string tag in GameplayTags.All)
		{
			Assert.True(Regex.IsMatch(tag, "^[a-z]+(\\.[a-z]+)*$"), $"bad tag '{tag}'");
		}
	}

	public void TestWithParentsAddsEveryPrefixOnce()
	{
		string[] expanded = GameplayTags.WithParents(["ability.boss.charge", "ability.boss"]).ToArray();
		Assert.SequenceEq(expanded.OrderBy(s => s, StringComparer.Ordinal), new[] { "ability", "ability.boss", "ability.boss.charge" });
	}

	public void TestCooldownForSwapsThePrefix()
	{
		Assert.Eq(GameplayTags.CooldownFor(GameplayTags.AbilityVolley), GameplayTags.CooldownVolley);
		Assert.Eq(GameplayTags.CooldownFor(GameplayTags.AbilityDash), GameplayTags.CooldownDash);
		Assert.Eq(GameplayTags.CooldownFor(GameplayTags.AbilityProbe), GameplayTags.CooldownProbe);
	}

	public void TestForgeRegistryContainsEveryTagAndParent()
	{
		TagsManager manager = ForgeManagers.Instance.TagsManager;
		foreach (string tag in GameplayTags.WithParents(GameplayTags.All))
		{
			Assert.True(Tag.RequestTag(manager, tag, errorIfNotFound: false).IsValid, $"'{tag}' is not registered; run the sync tool");
		}
	}

	public void TestUnregisteredTagFailsLoudly()
	{
		Assert.Throws<ArgumentException>(() => GameplayTags.Request("ability.nonexistent"));
	}

	public void TestRequestedTagsMatchHierarchically()
	{
		Tag boss = GameplayTags.Request("ability.boss");
		Tag charge = GameplayTags.Request(GameplayTags.AbilityBossCharge);
		Assert.True(charge.MatchesTag(boss), "child tag matches its parent");
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: count greater than 0.

- [ ] **Step 3: Implement `GameplayTags`**

`port/src/Gas/GameplayTags.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Gamesmiths.Forge.Godot.Core;
using Gamesmiths.Forge.Tags;

namespace VampireSurvivor.Gas;

/// <summary>
/// The only place tag names are spelled. <see cref="All"/> is the source from which tools/sync_tags writes
/// addons/forge/core/forge_data.tres. Tags are lowercase and dotted.
/// </summary>
public static class GameplayTags
{
	public const string AbilityArrow = "ability.arrow";
	public const string AbilityBossBurst = "ability.boss.burst";
	public const string AbilityBossCharge = "ability.boss.charge";
	public const string AbilityBossSlam = "ability.boss.slam";
	public const string AbilityBossSummon = "ability.boss.summon";
	public const string AbilityBounty = "ability.bounty";
	public const string AbilityDash = "ability.dash";
	public const string AbilitySweep = "ability.sweep";
	public const string AbilityVolley = "ability.volley";

	/// <summary>Used by tests only (probe abilities).</summary>
	public const string AbilityProbe = "ability.probe";

	public const string CooldownArrow = "cooldown.arrow";
	public const string CooldownDash = "cooldown.dash";
	public const string CooldownVolley = "cooldown.volley";
	public const string CooldownSweep = "cooldown.sweep";

	/// <summary>Used by tests only (probe abilities).</summary>
	public const string CooldownProbe = "cooldown.probe";

	public const string DamageAmount = "damage.amount";
	public const string EventKill = "event.kill";
	public const string StateCasting = "state.casting";
	public const string StateInvulnerable = "state.invulnerable";

	public static readonly string[] All =
	[
		AbilityArrow, AbilityBossBurst, AbilityBossCharge, AbilityBossSlam, AbilityBossSummon, AbilityBounty,
		AbilityDash, AbilitySweep, AbilityVolley, AbilityProbe,
		CooldownArrow, CooldownDash, CooldownVolley, CooldownSweep, CooldownProbe,
		DamageAmount, EventKill, StateCasting, StateInvulnerable,
	];

	/// <summary>Every tag plus all of its dotted prefixes, each once ("a.b.c" -> a, a.b, a.b.c).</summary>
	public static IEnumerable<string> WithParents(IEnumerable<string> tags)
	{
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (string tag in tags)
		{
			string[] parts = tag.Split('.');
			for (int length = 1; length <= parts.Length; length++)
			{
				string prefix = string.Join('.', parts.Take(length));
				if (seen.Add(prefix))
				{
					yield return prefix;
				}
			}
		}
	}

	/// <summary>"ability.volley" -> "cooldown.volley".</summary>
	public static string CooldownFor(string abilityTag)
	{
		const string abilityPrefix = "ability.";
		if (!abilityTag.StartsWith(abilityPrefix, StringComparison.Ordinal))
		{
			throw new ArgumentException($"'{abilityTag}' is not an ability tag", nameof(abilityTag));
		}

		return "cooldown." + abilityTag[abilityPrefix.Length..];
	}

	/// <summary>The registered tag, or an <see cref="ArgumentException"/> naming the missing tag.</summary>
	public static Tag Request(string tag)
	{
		Tag result = Tag.RequestTag(ForgeManagers.Instance.TagsManager, tag, errorIfNotFound: false);
		if (!result.IsValid)
		{
			throw new ArgumentException(
				$"Tag '{tag}' is not registered. Add it to GameplayTags.All and run tools/sync_tags.",
				nameof(tag));
		}

		return result;
	}

	public static TagContainer ContainerOf(string tag) => Request(tag).GetSingleTagContainer()!;
}
```

- [ ] **Step 4: Implement the sync tool**

`port/tools/SyncTags.cs`:

```csharp
using Gamesmiths.Forge.Godot.Core;
using Godot;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Tools;

/// <summary>
/// Rebuilds addons/forge/core/forge_data.tres from GameplayTags.All (with parents).
/// Run: Godot --headless --path . res://port/tools/sync_tags.tscn
/// </summary>
public partial class SyncTags : Node
{
	public override void _Ready()
	{
		var tags = new Godot.Collections.Array<string>(GameplayTags.WithParents(GameplayTags.All));
		var data = new ForgeData { RegisteredTags = tags };
		Error error = ResourceSaver.Save(data, ForgeData.ForgeDataResourcePath);
		GD.Print($"sync_tags: {tags.Count} tags -> {error}");
		GetTree().Quit(error == Error.Ok ? 0 : 1);
	}
}
```

`port/tools/sync_tags.tscn`:

```
[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://port/tools/SyncTags.cs" id="1_sync"]

[node name="SyncTags" type="Node"]
script = ExtResource("1_sync")
```

- [ ] **Step 5: Generate the registry, then run**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q && /d/CM/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . res://port/tools/sync_tags.tscn 2>&1 | grep sync_tags
git diff --stat addons/forge/core/forge_data.tres
RUNNER=cs tools/run_tests.sh | grep -E "GameplayTags|^----"
```
Expected: `sync_tags: <N> tags -> Ok`, a diff on `forge_data.tres` only, seven `PASS  Gas/GameplayTagsTest::...` lines and `0 failed`. If Godot reports the script classes are unknown, run `IMPORT=1` once and repeat.

- [ ] **Step 6: Commit**

```bash
git add port/src/Gas port/tools port/tests/Gas addons/forge/core/forge_data.tres
git commit -m "feat(gas): GameplayTags constants and forge_data.tres sync tool" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Attribute sets, `Stats`, `StatBlock`, `ActorEntity` and the bare-actor fixture

**Files:**
- Create: `port/src/Defs/StatBlock.cs`, `port/src/Gas/Stats.cs`, `port/src/Gas/StatEffects.cs`, `port/src/Gas/Attributes/VitalsAttributes.cs`, `port/src/Gas/Attributes/CombatAttributes.cs`, `port/src/Gas/Attributes/EconomyAttributes.cs`, `port/src/Components/ActorEntity.cs`, `port/src/Components/actor_entity.tscn`, `port/tests/Support/ActorFixture.cs`, `port/tests/Support/TestEffects.cs`
- Test: `port/tests/Components/ActorEntityTest.cs`, `port/tests/Defs/StatBlockTest.cs`

**Interfaces:**
- Consumes: `GameplayTags` (Task 5).
- Produces:
  - `VampireSurvivor.Defs.StatBlock : Resource` — `[Export] float MaxHealth = 100`, `MoveSpeed = 100`, `Attack = 1`; `List<string> Validate()`.
  - `VampireSurvivor.Gas.Stats` — consts `Health = "VitalsAttributes.Health"`, `MaxHealth`, `MoveSpeed = "CombatAttributes.MoveSpeed"`, `Attack`, `GoldGain = "EconomyAttributes.GoldGain"`, `StoneGain`; `HealthDecimals = 2`, `SpeedDecimals = 2`, `AttackDecimals = 3`, `GainDecimals = 3`; `static int Scale(int decimals)`.
  - `VampireSurvivor.Gas.StatEffects.SetBase(string key, int raw) : EffectData` (instant Override of the base value).
  - `VampireSurvivor.Components.ActorEntity : ForgeEntity` — `Tick(double)`, `ApplyStats(StatBlock)`, `FillHealth()`, `Apply(EffectData)`; ticks from `_PhysicsProcess`.
  - Tests: `ActorFixture.BareActor(TestCase test, StatBlock? stats = null) : ActorEntity`; `TestEffects.Apply(ActorEntity, EffectData)`, `TestEffects.Flat(ActorEntity, string key, int raw)`.

- [ ] **Step 1: Write the failing tests**

`port/tests/Defs/StatBlockTest.cs`:

```csharp
using VampireSurvivor.Defs;

namespace VampireSurvivor.Tests.Defs;

public class StatBlockTest : TestCase
{
	public void TestDefaultsAreValid()
	{
		Assert.NoErrors(new StatBlock().Validate());
	}

	public void TestNonPositiveMaxHealthIsRejected()
	{
		Assert.Eq(new StatBlock { MaxHealth = 0f }.Validate().Count, 1);
		Assert.Eq(new StatBlock { MaxHealth = -5f }.Validate().Count, 1);
	}

	public void TestNegativeMoveSpeedIsRejected()
	{
		Assert.Eq(new StatBlock { MoveSpeed = -1f }.Validate().Count, 1);
	}
}
```

`port/tests/Components/ActorEntityTest.cs`:

```csharp
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class ActorEntityTest : TestCase
{
	private static StatBlock MakeStats(float maxHealth, float attack = 1f) => new() { MaxHealth = maxHealth, Attack = attack };

	private static double Display(ActorEntity actor, string key) => actor.Attributes[key].DisplayValue;

	public void TestApplyStatsSetsHealthAndMaxHealth()
	{
		ActorEntity actor = ActorFixture.BareActor(this, MakeStats(50f));
		Assert.AlmostEq(Display(actor, Stats.Health), 50.0);
		Assert.AlmostEq(Display(actor, Stats.MaxHealth), 50.0);
	}

	public void TestStatsAboveTheDefaultMaxApplyToBoth()
	{
		ActorEntity actor = ActorFixture.BareActor(this, MakeStats(150f));
		Assert.AlmostEq(Display(actor, Stats.Health), 150.0);
		Assert.AlmostEq(Display(actor, Stats.MaxHealth), 150.0);
	}

	public void TestApplyStatsSetsSpeedAndAttackWithTheirScales()
	{
		var stats = new StatBlock { MaxHealth = 100f, MoveSpeed = 140f, Attack = 1.35f };
		ActorEntity actor = ActorFixture.BareActor(this, stats);
		Assert.AlmostEq(Display(actor, Stats.MoveSpeed), 140.0);
		Assert.Eq(actor.Attributes[Stats.Attack].CurrentValue, 1350);
	}

	public void TestHealthIsClampedToMaxHealth()
	{
		ActorEntity actor = ActorFixture.BareActor(this, MakeStats(50f));
		TestEffects.Apply(actor, StatEffects.SetBase(Stats.Health, 8000));
		Assert.AlmostEq(Display(actor, Stats.Health), 50.0);
	}

	public void TestAttributeSetsAreNotSharedBetweenActors()
	{
		ActorEntity first = ActorFixture.BareActor(this, MakeStats(100f));
		ActorEntity second = ActorFixture.BareActor(this, MakeStats(100f));
		TestEffects.Flat(first, Stats.Health, -4000);
		Assert.AlmostEq(Display(first, Stats.Health), 60.0);
		Assert.AlmostEq(Display(second, Stats.Health), 100.0);
	}

	public void TestLoweringMaxHealthPullsHealthDown()
	{
		ActorEntity actor = ActorFixture.BareActor(this, MakeStats(100f));
		TestEffects.Flat(actor, Stats.MaxHealth, -4000);
		Assert.AlmostEq(Display(actor, Stats.MaxHealth), 60.0);
		Assert.AlmostEq(Display(actor, Stats.Health), 60.0);
	}

	public void TestRaisingMaxHealthNeverHeals()
	{
		ActorEntity actor = ActorFixture.BareActor(this, MakeStats(100f));
		TestEffects.Flat(actor, Stats.Health, -3000);
		TestEffects.Flat(actor, Stats.MaxHealth, 5000);
		Assert.AlmostEq(Display(actor, Stats.MaxHealth), 150.0);
		Assert.AlmostEq(Display(actor, Stats.Health), 70.0);
	}

	public void TestFillHealthRaisesHealthToMax()
	{
		ActorEntity actor = ActorFixture.BareActor(this, MakeStats(100f));
		TestEffects.Flat(actor, Stats.Health, -3000);
		actor.FillHealth();
		Assert.AlmostEq(Display(actor, Stats.Health), 100.0);
	}

	public void TestAbsurdMaxHealthIsClampedToTheAttributeLimit()
	{
		ActorEntity actor = ActorFixture.BareActor(this, MakeStats(5_000_000f));
		Assert.Eq(actor.Attributes[Stats.MaxHealth].CurrentValue, 10_000_000);
		Assert.True(actor.Attributes[Stats.Health].CurrentValue <= 10_000_000);
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: count greater than 0.

- [ ] **Step 3: Implement `StatBlock`**

`port/src/Defs/StatBlock.cs`:

```csharp
using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.Defs;

/// <summary>Base stats of an actor, authored in display units (floats) and converted to Forge integers once.</summary>
[GlobalClass]
public partial class StatBlock : Resource
{
	[Export]
	public float MaxHealth { get; set; } = 100f;

	[Export]
	public float MoveSpeed { get; set; } = 100f;

	/// <summary>Damage multiplier applied to hits this actor lands (1.0 = base damage).</summary>
	[Export]
	public float Attack { get; set; } = 1f;

	public List<string> Validate()
	{
		var errors = new List<string>();
		if (MaxHealth <= 0f)
		{
			errors.Add("StatBlock: MaxHealth must be > 0");
		}

		if (MoveSpeed < 0f)
		{
			errors.Add("StatBlock: MoveSpeed must be >= 0");
		}

		return errors;
	}
}
```

- [ ] **Step 4: Implement `Stats`, `StatEffects` and the attribute sets**

`port/src/Gas/Stats.cs`:

```csharp
namespace VampireSurvivor.Gas;

/// <summary>
/// Attribute keys ("{AttributeSetClass}.{Property}") and the fixed-point scale of each stat. Data and UI use floats;
/// Forge stores integers, so the conversion happens only at the edges (ApplyStats, DamageExecution, display).
/// </summary>
public static class Stats
{
	public const string Health = "VitalsAttributes.Health";
	public const string MaxHealth = "VitalsAttributes.MaxHealth";
	public const string MoveSpeed = "CombatAttributes.MoveSpeed";
	public const string Attack = "CombatAttributes.Attack";
	public const string GoldGain = "EconomyAttributes.GoldGain";
	public const string StoneGain = "EconomyAttributes.StoneGain";

	public const int HealthDecimals = 2;
	public const int SpeedDecimals = 2;
	public const int AttackDecimals = 3;
	public const int GainDecimals = 3;

	/// <summary>10^decimals: the raw value that stands for 1.0.</summary>
	public static int Scale(int decimals)
	{
		int scale = 1;
		for (int i = 0; i < decimals; i++)
		{
			scale *= 10;
		}

		return scale;
	}
}
```

`port/src/Gas/StatEffects.cs`:

```csharp
using Gamesmiths.Forge.Effects;
using Gamesmiths.Forge.Effects.Duration;
using Gamesmiths.Forge.Effects.Magnitudes;
using Gamesmiths.Forge.Effects.Modifiers;

namespace VampireSurvivor.Gas;

/// <summary>Instant effects that set an attribute's base value (Forge changes attributes only through effects).</summary>
public static class StatEffects
{
	/// <summary>Raw values up to 2^24 are exact as floats; the attribute limits keep every stat below that.</summary>
	public static EffectData SetBase(string key, int raw)
	{
		var magnitude = new ModifierMagnitude(MagnitudeCalculationType.ScalableFloat, new ScalableFloat(raw));
		return new EffectData(
			$"SetBase:{key}",
			new DurationData(DurationType.Instant),
			[new Modifier(key, ModifierOperation.Override, magnitude)]);
	}
}
```

`port/src/Gas/Attributes/VitalsAttributes.cs`:

```csharp
using Gamesmiths.Forge.Attributes;

namespace VampireSurvivor.Gas.Attributes;

/// <summary>Hit points. Health is clamped to [0, MaxHealth]: a lowered MaxHealth pulls Health down, a raised one never heals.</summary>
public class VitalsAttributes : AttributeSet
{
	public VitalsAttributes()
	{
		MaxHealth = InitializeAttribute(nameof(MaxHealth), 10_000, 1, 10_000_000, decimalPlaces: Stats.HealthDecimals);
		Health = InitializeAttribute(nameof(Health), 10_000, 0, 10_000, decimalPlaces: Stats.HealthDecimals);
	}

	public EntityAttribute Health { get; }

	public EntityAttribute MaxHealth { get; }

	protected override void AttributeOnValueChanged(EntityAttribute attribute, int change)
	{
		if (attribute == MaxHealth)
		{
			SetAttributeMaxValue(Health, MaxHealth.CurrentValue);
		}
	}
}
```

`port/src/Gas/Attributes/CombatAttributes.cs`:

```csharp
using Gamesmiths.Forge.Attributes;

namespace VampireSurvivor.Gas.Attributes;

/// <summary>Offensive and mobility stats.</summary>
public class CombatAttributes : AttributeSet
{
	public CombatAttributes()
	{
		MoveSpeed = InitializeAttribute(nameof(MoveSpeed), 10_000, 0, 1_000_000, decimalPlaces: Stats.SpeedDecimals);
		Attack = InitializeAttribute(nameof(Attack), 1_000, 0, 1_000_000, decimalPlaces: Stats.AttackDecimals);
	}

	/// <summary>Pixels per second (x100).</summary>
	public EntityAttribute MoveSpeed { get; }

	/// <summary>Damage multiplier for hits this actor lands (x1000).</summary>
	public EntityAttribute Attack { get; }
}
```

`port/src/Gas/Attributes/EconomyAttributes.cs`:

```csharp
using Gamesmiths.Forge.Attributes;

namespace VampireSurvivor.Gas.Attributes;

/// <summary>Multipliers applied to currency the actor collects.</summary>
public class EconomyAttributes : AttributeSet
{
	public EconomyAttributes()
	{
		GoldGain = InitializeAttribute(nameof(GoldGain), 1_000, 0, 1_000_000, decimalPlaces: Stats.GainDecimals);
		StoneGain = InitializeAttribute(nameof(StoneGain), 1_000, 0, 1_000_000, decimalPlaces: Stats.GainDecimals);
	}

	public EntityAttribute GoldGain { get; }

	public EntityAttribute StoneGain { get; }
}
```

Note: `ForgeAttributeSet` finds the set by class **name** (`Name == "VitalsAttributes"`), which is why the keys in `Stats` start with the class name.

- [ ] **Step 5: Implement `ActorEntity` and its scene**

`port/src/Components/ActorEntity.cs`:

```csharp
using Gamesmiths.Forge.Effects;
using Gamesmiths.Forge.Godot.Nodes;
using Godot;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Components;

/// <summary>
/// The game-facing Forge seam: a ForgeEntity with the game's three attribute sets and a thin API. Game code talks to
/// this class, never to Forge types directly. Ticks in _PhysicsProcess (one game clock); tests turn physics
/// processing off and call <see cref="Tick"/> themselves.
/// </summary>
[GlobalClass]
public partial class ActorEntity : ForgeEntity
{
	public override void _Ready()
	{
		base._Ready();
		SetProcess(false); // ForgeEntity ticks in _Process; the game ticks in _PhysicsProcess.
	}

	public override void _PhysicsProcess(double delta) => Tick(delta);

	public void Tick(double delta)
	{
		EffectsManager.UpdateEffects(delta);
		Abilities.UpdateAbilities(delta);
	}

	/// <summary>Applies base stats. Call after the node is in the tree. MaxHealth first: Health clamps to it.</summary>
	public void ApplyStats(StatBlock stats)
	{
		SetFromDisplay(Stats.MaxHealth, stats.MaxHealth);
		SetFromDisplay(Stats.Health, stats.MaxHealth);
		SetFromDisplay(Stats.MoveSpeed, stats.MoveSpeed);
		SetFromDisplay(Stats.Attack, stats.Attack);
	}

	public void FillHealth()
	{
		Apply(StatEffects.SetBase(Stats.Health, Attributes[Stats.MaxHealth].CurrentValue));
	}

	/// <summary>Applies a prepared effect with this entity as owner and source.</summary>
	public void Apply(EffectData data)
	{
		EffectsManager.ApplyEffect(new Effect(data, new EffectOwnership(this, this)));
	}

	private void SetFromDisplay(string key, float displayValue)
	{
		Apply(StatEffects.SetBase(key, Attributes[key].ToRawValue(displayValue)));
	}
}
```

`port/src/Components/actor_entity.tscn`:

```
[gd_scene load_steps=3 format=3]

[ext_resource type="Script" path="res://port/src/Components/ActorEntity.cs" id="1_entity"]
[ext_resource type="Script" path="res://addons/forge/nodes/ForgeAttributeSet.cs" id="2_set"]

[node name="ActorEntity" type="Node"]
script = ExtResource("1_entity")

[node name="Vitals" type="Node" parent="."]
script = ExtResource("2_set")
AttributeSetClass = "VitalsAttributes"

[node name="Combat" type="Node" parent="."]
script = ExtResource("2_set")
AttributeSetClass = "CombatAttributes"

[node name="Economy" type="Node" parent="."]
script = ExtResource("2_set")
AttributeSetClass = "EconomyAttributes"
```

- [ ] **Step 6: Implement the test helpers**

`port/tests/Support/TestEffects.cs`:

```csharp
using Gamesmiths.Forge.Effects;
using Gamesmiths.Forge.Effects.Duration;
using Gamesmiths.Forge.Effects.Magnitudes;
using Gamesmiths.Forge.Effects.Modifiers;
using VampireSurvivor.Components;

namespace VampireSurvivor.Tests.Support;

public static class TestEffects
{
	public static void Apply(ActorEntity entity, EffectData data) => entity.Apply(data);

	/// <summary>An instant FlatBonus of <paramref name="raw"/> (raw attribute units) on one attribute.</summary>
	public static void Flat(ActorEntity entity, string key, int raw)
	{
		var magnitude = new ModifierMagnitude(MagnitudeCalculationType.ScalableFloat, new ScalableFloat(raw));
		var data = new EffectData(
			$"TestFlat:{key}",
			new DurationData(DurationType.Instant),
			[new Modifier(key, ModifierOperation.FlatBonus, magnitude)]);
		entity.Apply(data);
	}
}
```

`port/tests/Support/ActorFixture.cs`:

```csharp
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
```

- [ ] **Step 7: Run**

```bash
RUNNER=cs IMPORT=1 tools/run_tests.sh | grep -E "ActorEntityTest|StatBlockTest|^----|FAIL|-"
```
Expected: nine `PASS  Components/ActorEntityTest::...`, three `PASS  Defs/StatBlockTest::...`, `0 failed`.
**Gate item (a) lives here** (`TestLoweringMaxHealthPullsHealthDown`, `TestRaisingMaxHealthNeverHeals`). If either fails because the change event does not reach `AttributeOnValueChanged` for modifier-driven changes, record the exact failure for Task 11 and try the alternative of subscribing to `OnValueChanged` for `MaxHealth` inside `ActorEntity._Ready` (which calls the same `SetMaxValue` through a small public method on `VitalsAttributes`); do not skip the test.

- [ ] **Step 8: Commit**

```bash
git add port/src/Defs port/src/Gas port/src/Components port/tests/Support port/tests/Components port/tests/Defs
git commit -m "feat(gas): attribute sets, stats scale and ActorEntity seam" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 7: `DamageEffects` and `DamageExecution`

**Files:**
- Create: `port/src/Gas/Executions/DamageExecution.cs`, `port/src/Gas/DamageEffects.cs`
- Test: `port/tests/Gas/DamageTest.cs`

**Interfaces:**
- Consumes: `ActorEntity`, `ActorFixture`, `Stats`, `GameplayTags` (Tasks 5-6).
- Produces: `VampireSurvivor.Gas.DamageEffects.Hit(IForgeEntity source, IForgeEntity target, float amount)` — `amount` in display units (8.0 = eight hit points); does nothing for `amount <= 0` or a target without `Stats.Health`; damage in raw units is `amountRaw * attackRaw / attackScale` in `long` arithmetic, truncated once, where `amountRaw = round(amount * 100)`.
- Out of scope here: blocking damage while the target carries `state.invulnerable` (the legacy `test_damage` case). It needs the Dash ability and lands with slice 2, where Dash exists.

- [ ] **Step 1: Write the failing tests**

`port/tests/Gas/DamageTest.cs`:

```csharp
using Gamesmiths.Forge.Godot.Nodes;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Gas;

public class DamageTest : TestCase
{
	private static StatBlock MakeStats(float maxHealth, float attack = 1f) => new() { MaxHealth = maxHealth, Attack = attack };

	private static double HealthOf(ActorEntity actor) => actor.Attributes[Stats.Health].DisplayValue;

	public void TestDamageScalesWithSourceAttack()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f, 2f));
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(100f));
		DamageEffects.Hit(attacker, victim, 10f);
		Assert.AlmostEq(HealthOf(victim), 80.0);
	}

	public void TestFractionalAttackKeepsTwoDecimalsOfDamage()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f, 1.35f));
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(100f));
		DamageEffects.Hit(attacker, victim, 8f);
		Assert.Eq(victim.Attributes[Stats.Health].CurrentValue, 8920, "8.0 x 1.35 = 10.80 damage -> 89.20 health");
	}

	public void TestDamageIsTruncatedOnceBelowTheHundredth()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f, 1.5f));
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(100f));
		DamageEffects.Hit(attacker, victim, 0.03f);
		Assert.Eq(victim.Attributes[Stats.Health].CurrentValue, 10_000 - 4, "0.03 x 1.5 = 0.045 -> 4 raw, truncated");
	}

	public void TestDamageCannotPushHealthBelowZero()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f));
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(30f));
		DamageEffects.Hit(attacker, victim, 500f);
		Assert.AlmostEq(HealthOf(victim), 0.0);
	}

	public void TestZeroOrNegativeDamageNeverHeals()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f));
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(100f));
		TestEffects.Flat(victim, Stats.Health, -3000);
		DamageEffects.Hit(attacker, victim, 0f);
		DamageEffects.Hit(attacker, victim, -50f);
		Assert.AlmostEq(HealthOf(victim), 70.0);
	}

	public void TestHittingAnEntityWithoutVitalsIsANoOp()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f));
		var bare = Adopt(new ForgeEntity());
		DamageEffects.Hit(attacker, bare, 10f);
		Assert.True(bare.IsInsideTree());
	}

	public void TestEachHitRemovesDamageFromTheSameVictimCumulatively()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f));
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(100f));
		DamageEffects.Hit(attacker, victim, 10f);
		DamageEffects.Hit(attacker, victim, 15f);
		Assert.AlmostEq(HealthOf(victim), 75.0);
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: count greater than 0 (`DamageEffects` missing).

- [ ] **Step 3: Implement `DamageExecution`**

`port/src/Gas/Executions/DamageExecution.cs`:

```csharp
using System;
using Gamesmiths.Forge.Attributes;
using Gamesmiths.Forge.Core;
using Gamesmiths.Forge.Effects;
using Gamesmiths.Forge.Effects.Calculator;
using Gamesmiths.Forge.Effects.Magnitudes;
using Gamesmiths.Forge.Effects.Modifiers;
using Gamesmiths.Forge.Tags;

namespace VampireSurvivor.Gas.Executions;

/// <summary>
/// Damage = the amount injected by the caller (set-by-caller "damage.amount", display units) x the source's Attack
/// multiplier, as a negative FlatBonus on the target's Health. All arithmetic is in raw integers and truncates once.
/// </summary>
public sealed class DamageExecution : CustomExecution
{
	public DamageExecution()
	{
		AttributesToCapture.Add(new AttributeCaptureDefinition(Stats.Attack, AttributeCaptureSource.Source));
	}

	public override ModifierEvaluatedData[] EvaluateExecution(
		Effect effect,
		IForgeEntity target,
		EffectEvaluatedData? effectEvaluatedData)
	{
		if (!target.Attributes.ContainsAttribute(Stats.Health))
		{
			return [];
		}

		Tag amountTag = GameplayTags.Request(GameplayTags.DamageAmount);
		float amount = effect.DataTag.TryGetValue(amountTag, out float value) ? value : 0f;
		if (amount <= 0f)
		{
			return [];
		}

		int attackRaw = CaptureAttributeMagnitude(AttributesToCapture[0], effect, target, effectEvaluatedData);
		long amountRaw = (long)MathF.Round(amount * Stats.Scale(Stats.HealthDecimals));
		long damageRaw = amountRaw * attackRaw / Stats.Scale(Stats.AttackDecimals);

		return [new ModifierEvaluatedData(target.Attributes[Stats.Health], ModifierOperation.FlatBonus, -damageRaw)];
	}
}
```

- [ ] **Step 4: Implement `DamageEffects`**

`port/src/Gas/DamageEffects.cs`:

```csharp
using Gamesmiths.Forge.Core;
using Gamesmiths.Forge.Effects;
using Gamesmiths.Forge.Effects.Duration;
using VampireSurvivor.Gas.Executions;

namespace VampireSurvivor.Gas;

/// <summary>The single place that assembles a hit, so every hit carries the same context.</summary>
public static class DamageEffects
{
	private static EffectData? _data;

	/// <summary>
	/// Damages <paramref name="target"/> by <paramref name="amount"/> display units times the source's Attack.
	/// Zero or negative amounts, and targets without vitals, are ignored: a hit can never heal.
	/// </summary>
	public static void Hit(IForgeEntity source, IForgeEntity target, float amount)
	{
		if (amount <= 0f || !target.Attributes.ContainsAttribute(Stats.Health))
		{
			return;
		}

		_data ??= new EffectData(
			"Damage",
			new DurationData(DurationType.Instant),
			customExecutions: [new DamageExecution()]);
		var effect = new Effect(_data.Value, new EffectOwnership(source, source));
		effect.SetSetByCallerMagnitude(GameplayTags.Request(GameplayTags.DamageAmount), amount);
		target.EffectsManager.ApplyEffect(effect);
	}
}
```

- [ ] **Step 5: Run**

```bash
RUNNER=cs tools/run_tests.sh | grep -E "DamageTest|^----|FAIL|  -"
```
Expected: seven `PASS  Gas/DamageTest::...` and `0 failed`.
**Gate item (b) lives here** (`TestDamageScalesWithSourceAttack`, `TestFractionalAttackKeepsTwoDecimalsOfDamage`). If the custom execution cannot capture the source's `Attack`, switch `Hit` to the spike method — compute `amount * attackRaw / attackScale` in `Hit` from `source.Attributes[Stats.Attack].CurrentValue`, pass the result as the set-by-caller amount, and use a plain set-by-caller modifier on Health — keep the tests unchanged, and record the fallback for Task 11.

- [ ] **Step 6: Commit**

```bash
git add port/src/Gas port/tests/Gas/DamageTest.cs
git commit -m "feat(gas): DamageEffects.Hit and DamageExecution" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 8: `PassiveDef` and `ActorEntity.ApplyPassive/RemovePassive`

**Files:**
- Create: `port/src/Defs/StatId.cs`, `port/src/Defs/PassiveDef.cs`, `port/src/Gas/PassiveEffects.cs`, `port/tests/Support/DefFactory.cs`
- Modify: `port/src/Gas/Stats.cs` (add `KeyOf`, `DecimalsOf`), `port/src/Components/ActorEntity.cs` (add `ApplyPassive`, `RemovePassive`)
- Test: `port/tests/Defs/PassiveDefTest.cs`, `port/tests/Components/PassiveTest.cs`

**Interfaces:**
- Consumes: Tasks 5-7.
- Produces:
  - `enum StatId { MaxHealth, MoveSpeed, Attack, GoldGain, StoneGain }`, `enum PassiveOperation { Flat, Percent }` (namespace `VampireSurvivor.Defs`).
  - `PassiveDef : Resource` — `[Export] StatId Stat`, `PassiveOperation Operation`, `Curve? ByLevel`; `List<string> Validate()` (curve must have exactly 6 points at x = 1..6 and a non-zero sample).
  - `Stats.KeyOf(StatId)`, `Stats.DecimalsOf(StatId)`.
  - `PassiveEffects.Build(PassiveDef) : EffectData` (INFINITE; Flat magnitude = `Scale(decimals) * curve(level)`, Percent magnitude = `1.0 * curve(level)`).
  - `ActorEntity.ApplyPassive(PassiveDef def, int level) : ActiveEffectHandle?`, `ActorEntity.RemovePassive(ActiveEffectHandle? handle)`.
  - `DefFactory.Might()` (Attack, Percent, curve 0.1/0.2/0.35/0.5/0.7/1.0), `DefFactory.Vitality()` (MaxHealth, Flat, curve 20/40/70/110/160/240), `DefFactory.CurveOf(params float[] ys)`.

- [ ] **Step 1: Write the failing tests**

`port/tests/Defs/PassiveDefTest.cs`:

```csharp
using VampireSurvivor.Defs;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Defs;

public class PassiveDefTest : TestCase
{
	public void TestWellFormedPassivesAreValid()
	{
		Assert.NoErrors(DefFactory.Might().Validate());
		Assert.NoErrors(DefFactory.Vitality().Validate());
	}

	public void TestMissingCurveIsReported()
	{
		var def = new PassiveDef { Stat = StatId.Attack, Operation = PassiveOperation.Percent, ByLevel = null };
		Assert.Eq(def.Validate().Count, 1);
	}

	public void TestWrongPointCountIsReported()
	{
		var def = new PassiveDef { Stat = StatId.Attack, Operation = PassiveOperation.Percent, ByLevel = DefFactory.CurveOf(0.1f, 0.2f, 0.3f) };
		Assert.True(def.Validate().Count >= 1);
	}

	public void TestPointsNotAtLevelsOneToSixAreReported()
	{
		var curve = new Godot.Curve();
		for (int i = 0; i < 6; i++)
		{
			curve.AddPoint(new Godot.Vector2(i + 2, 1f));
		}

		var def = new PassiveDef { Stat = StatId.Attack, Operation = PassiveOperation.Percent, ByLevel = curve };
		Assert.True(def.Validate().Count >= 1);
	}

	public void TestAllZeroCurveIsReported()
	{
		var def = new PassiveDef { Stat = StatId.Attack, Operation = PassiveOperation.Percent, ByLevel = DefFactory.CurveOf(0f, 0f, 0f, 0f, 0f, 0f) };
		Assert.True(def.Validate().Count >= 1);
	}
}
```

`port/tests/Components/PassiveTest.cs`:

```csharp
using Gamesmiths.Forge.Effects;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class PassiveTest : TestCase
{
	private static double Display(ActorEntity actor, string key) => actor.Attributes[key].DisplayValue;

	public void TestPercentPassiveFollowsTheGradeCurve()
	{
		float[] expected = [1.1f, 1.2f, 1.35f, 1.5f, 1.7f, 2.0f];
		for (int level = 1; level <= 6; level++)
		{
			ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
			actor.ApplyPassive(DefFactory.Might(), level);
			Assert.AlmostEq(Display(actor, Stats.Attack), expected[level - 1], 0.0005, $"level {level}");
		}
	}

	public void TestFlatPassiveAddsDisplayUnits()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock { MaxHealth = 100f });
		actor.ApplyPassive(DefFactory.Vitality(), 3);
		Assert.AlmostEq(Display(actor, Stats.MaxHealth), 170.0);
	}

	public void TestVitalityDoesNotHeal()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock { MaxHealth = 100f });
		actor.ApplyPassive(DefFactory.Vitality(), 6);
		Assert.AlmostEq(Display(actor, Stats.MaxHealth), 340.0);
		Assert.AlmostEq(Display(actor, Stats.Health), 100.0);
	}

	public void TestRemovingTheOnlyPassiveRestoresTheStat()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ActiveEffectHandle? handle = actor.ApplyPassive(DefFactory.Might(), 4);
		Assert.NotNull(handle);
		actor.RemovePassive(handle);
		Assert.AlmostEq(Display(actor, Stats.Attack), 1.0);
	}

	public void TestRemovingVitalityPullsHealthBackToTheBaseMax()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock { MaxHealth = 100f });
		ActiveEffectHandle? handle = actor.ApplyPassive(DefFactory.Vitality(), 6);
		actor.FillHealth();
		Assert.AlmostEq(Display(actor, Stats.Health), 340.0);
		actor.RemovePassive(handle);
		Assert.AlmostEq(Display(actor, Stats.MaxHealth), 100.0);
		Assert.AlmostEq(Display(actor, Stats.Health), 100.0);
	}

	public void TestReapplyAtAnotherLevelUsesTheNewLevel()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ActiveEffectHandle? first = actor.ApplyPassive(DefFactory.Might(), 1);
		actor.RemovePassive(first);
		actor.ApplyPassive(DefFactory.Might(), 4);
		Assert.AlmostEq(Display(actor, Stats.Attack), 1.5, 0.0005);
	}

	public void TestTwoSlotsWithTheSamePassiveStack()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		PassiveDef might = DefFactory.Might();
		ActiveEffectHandle? a = actor.ApplyPassive(might, 1);
		ActiveEffectHandle? b = actor.ApplyPassive(might, 2);
		Assert.AlmostEq(Display(actor, Stats.Attack), 1.3, 0.0005, "1.0 + 0.1 + 0.2");
		actor.RemovePassive(a);
		Assert.AlmostEq(Display(actor, Stats.Attack), 1.2, 0.0005, "only the level-2 slot remains");
		actor.RemovePassive(b);
		Assert.AlmostEq(Display(actor, Stats.Attack), 1.0, 0.0005);
	}

	public void TestRemovingANullOrAlreadyRemovedHandleIsHarmless()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ActiveEffectHandle? handle = actor.ApplyPassive(DefFactory.Might(), 2);
		actor.RemovePassive(handle);
		actor.RemovePassive(handle);
		actor.RemovePassive(null);
		Assert.AlmostEq(Display(actor, Stats.Attack), 1.0, 0.0005);
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: count greater than 0.

- [ ] **Step 3: Implement the definitions**

`port/src/Defs/StatId.cs`:

```csharp
namespace VampireSurvivor.Defs;

/// <summary>The stats a passive can modify. The mapping to Forge attribute keys lives in Gas/Stats.</summary>
public enum StatId
{
	MaxHealth,
	MoveSpeed,
	Attack,
	GoldGain,
	StoneGain,
}

public enum PassiveOperation
{
	/// <summary>Adds the curve value in display units (+20 health).</summary>
	Flat,

	/// <summary>Adds the curve value as a fraction (0.1 = +10%).</summary>
	Percent,
}
```

`port/src/Defs/PassiveDef.cs`:

```csharp
using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.Defs;

/// <summary>
/// A permanent stat modifier scaled by a curve over the level (grade 1..6 for abilities, purchased level for skills).
/// Forge's EffectData is built from this at load time (Gas/PassiveEffects) and never saved.
/// </summary>
[GlobalClass]
public partial class PassiveDef : Resource
{
	public const int LevelCount = 6;

	[Export]
	public StatId Stat { get; set; }

	[Export]
	public PassiveOperation Operation { get; set; }

	/// <summary>Six points at x = 1..6; y is the display-unit amount (Flat) or fraction (Percent).</summary>
	[Export]
	public Curve? ByLevel { get; set; }

	public List<string> Validate()
	{
		var errors = new List<string>();
		if (ByLevel is null)
		{
			errors.Add($"PassiveDef {Stat}: ByLevel curve is null");
			return errors;
		}

		if (ByLevel.PointCount != LevelCount)
		{
			errors.Add($"PassiveDef {Stat}: ByLevel needs {LevelCount} points, has {ByLevel.PointCount}");
			return errors;
		}

		bool anyNonZero = false;
		for (int i = 0; i < LevelCount; i++)
		{
			if (!Mathf.IsEqualApprox(ByLevel.GetPointPosition(i).X, i + 1))
			{
				errors.Add($"PassiveDef {Stat}: point {i} must be at x = {i + 1}");
			}

			anyNonZero |= !Mathf.IsZeroApprox(ByLevel.GetPointPosition(i).Y);
		}

		if (!anyNonZero)
		{
			errors.Add($"PassiveDef {Stat}: every level is 0 (the passive would do nothing)");
		}

		return errors;
	}
}
```

- [ ] **Step 4: Extend `Stats`, add `PassiveEffects`, extend `ActorEntity`**

Add to `port/src/Gas/Stats.cs` (inside the class; add `using VampireSurvivor.Defs;` at the top):

```csharp
	public static string KeyOf(StatId stat) => stat switch
	{
		StatId.MaxHealth => MaxHealth,
		StatId.MoveSpeed => MoveSpeed,
		StatId.Attack => Attack,
		StatId.GoldGain => GoldGain,
		_ => StoneGain,
	};

	public static int DecimalsOf(StatId stat) => stat switch
	{
		StatId.MaxHealth => HealthDecimals,
		StatId.MoveSpeed => SpeedDecimals,
		StatId.Attack => AttackDecimals,
		_ => GainDecimals,
	};
```

`port/src/Gas/PassiveEffects.cs`:

```csharp
using Gamesmiths.Forge.Effects;
using Gamesmiths.Forge.Effects.Duration;
using Gamesmiths.Forge.Effects.Magnitudes;
using Gamesmiths.Forge.Effects.Modifiers;
using Gamesmiths.Forge.Godot.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Gas;

public static class PassiveEffects
{
	/// <summary>
	/// An INFINITE effect whose single modifier is scaled by the curve at the effect level. Flat curves are in display
	/// units, so the base value is the stat's scale; Percent curves are fractions, so the base value is 1.
	/// </summary>
	public static EffectData Build(PassiveDef def)
	{
		bool flat = def.Operation == PassiveOperation.Flat;
		float baseValue = flat ? Stats.Scale(Stats.DecimalsOf(def.Stat)) : 1f;
		var magnitude = new ModifierMagnitude(
			MagnitudeCalculationType.ScalableFloat,
			new ScalableFloat(baseValue, new ForgeCurve(def.ByLevel)));
		var modifier = new Modifier(
			Stats.KeyOf(def.Stat),
			flat ? ModifierOperation.FlatBonus : ModifierOperation.PercentBonus,
			magnitude);
		return new EffectData($"Passive:{def.Stat}", new DurationData(DurationType.Infinite), [modifier]);
	}
}
```

Add to `ActorEntity` (inside the class; add `using Gamesmiths.Forge.Effects;` already present):

```csharp
	/// <summary>Applies a permanent passive at <paramref name="level"/>; keep the handle to remove it later.</summary>
	public ActiveEffectHandle? ApplyPassive(PassiveDef def, int level)
	{
		return EffectsManager.ApplyEffect(new Effect(PassiveEffects.Build(def), new EffectOwnership(this, this), level));
	}

	public void RemovePassive(ActiveEffectHandle? handle)
	{
		if (handle is { IsValid: true })
		{
			EffectsManager.RemoveEffect(handle);
		}
	}
```

- [ ] **Step 5: Implement `DefFactory`**

`port/tests/Support/DefFactory.cs`:

```csharp
using Godot;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Tests.Support;

/// <summary>Small, valid definitions for tests (the real ones are .tres files).</summary>
public static class DefFactory
{
	/// <summary>A curve with points at x = 1..n.</summary>
	public static Curve CurveOf(params float[] ys)
	{
		var curve = new Curve { MinDomain = 1f, MaxDomain = ys.Length, MaxValue = 1000f };
		for (int i = 0; i < ys.Length; i++)
		{
			curve.AddPoint(new Vector2(i + 1, ys[i]));
		}

		return curve;
	}

	public static PassiveDef Might() => new()
	{
		Stat = StatId.Attack,
		Operation = PassiveOperation.Percent,
		ByLevel = CurveOf(0.1f, 0.2f, 0.35f, 0.5f, 0.7f, 1.0f),
	};

	public static PassiveDef Vitality() => new()
	{
		Stat = StatId.MaxHealth,
		Operation = PassiveOperation.Flat,
		ByLevel = CurveOf(20f, 40f, 70f, 110f, 160f, 240f),
	};
}
```

- [ ] **Step 6: Run**

```bash
RUNNER=cs tools/run_tests.sh | grep -E "PassiveTest|PassiveDefTest|^----|FAIL|  -"
```
Expected: eight `PASS  Components/PassiveTest::...`, five `PASS  Defs/PassiveDefTest::...`, `0 failed`.
**Gate item (c) lives here** (`TestTwoSlotsWithTheSamePassiveStack`, `TestReapplyAtAnotherLevelUsesTheNewLevel`, `TestRemovingVitalityPullsHealthBackToTheBaseMax`). If two applications of equivalent effect data do not stack, record the exact behaviour for Task 11 (the fix would be a distinct `EffectData` per slot or `StackingData`); do not weaken the test.

- [ ] **Step 7: Commit**

```bash
git add port/src/Defs port/src/Gas port/src/Components/ActorEntity.cs port/tests/Support/DefFactory.cs port/tests/Defs/PassiveDefTest.cs port/tests/Components/PassiveTest.cs
git commit -m "feat(gas): PassiveDef and passive effects on ActorEntity" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Abilities by effect grant, cooldowns, revoke and `Cleanup`

**Files:**
- Create: `port/src/Defs/ActiveAbilityDef.cs`, `port/src/Gas/CooldownEffects.cs`, `port/src/Gas/AbilityFactory.cs`, `port/src/Components/AbilityGrant.cs`, `port/tests/Support/ProbeAbilityDef.cs`
- Modify: `port/src/Components/ActorEntity.cs` (add `Grant`, `Revoke`, `Cleanup`, `CleanupDeferred`)
- Test: `port/tests/Components/AbilityGrantTest.cs`, `port/tests/Components/CleanupTest.cs`

**Interfaces:**
- Consumes: Tasks 5-8.
- Produces:
  - `ActiveAbilityDef : ForgeAbilityBehavior` (abstract; `[Export] string Tag`, `[Export] Curve? Cooldown`, `[Export] string[] OwnedTags`; `abstract IAbilityBehavior CreateBehavior()`; `GetBehavior()` returns it; `List<string> Validate()`).
  - `CooldownEffects.Build(string cooldownTag, Curve curve) : EffectData`; `AbilityFactory.Build(ActiveAbilityDef) : AbilityData` (cached per def instance).
  - `record AbilityGrant(ActiveEffectHandle Effect, AbilityHandle? Ability, AbilityData Data)` in `VampireSurvivor.Components`.
  - `ActorEntity.Grant(ActiveAbilityDef def, int level) : AbilityGrant`, `ActorEntity.Revoke(AbilityGrant? grant)`, `ActorEntity.Cleanup()`, `ActorEntity.CleanupDeferred()`.
  - Test support: `ProbeAbilityDef : ActiveAbilityDef` with public counters `Starts`, `Ends`, bool `StayActive`.
- 0.4.0 cannot revoke a permanent grant, so `Grant` applies an INFINITE effect carrying `GrantAbilityEffectComponent`; `Revoke` removes that effect (the grant goes with it) and also removes the ability's cooldown effect so a re-grant starts with a fresh cooldown.

- [ ] **Step 1: Write the failing tests**

`port/tests/Support/ProbeAbilityDef.cs`:

```csharp
using Gamesmiths.Forge.Abilities;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Tests.Support;

/// <summary>An ability that counts its starts and ends; optionally it stays active until cancelled.</summary>
public sealed partial class ProbeAbilityDef : ActiveAbilityDef
{
	public int Starts { get; set; }

	public int Ends { get; set; }

	public bool StayActive { get; set; }

	public override IAbilityBehavior CreateBehavior() => new ProbeBehavior(this);

	private sealed class ProbeBehavior(ProbeAbilityDef owner) : IAbilityBehavior
	{
		public void OnStarted(AbilityBehaviorContext context)
		{
			owner.Starts++;
			context.AbilityHandle.CommitCooldown();
			if (!owner.StayActive)
			{
				context.InstanceHandle.End();
			}
		}

		public void OnEnded(AbilityBehaviorContext context)
		{
			owner.Ends++;
		}
	}
}
```

`port/tests/Components/AbilityGrantTest.cs`:

```csharp
using Gamesmiths.Forge.Abilities;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class AbilityGrantTest : TestCase
{
	private static ProbeAbilityDef MakeProbe(params float[] cooldowns) => new()
	{
		Tag = GameplayTags.AbilityProbe,
		Cooldown = DefFactory.CurveOf(cooldowns),
	};

	private static bool Fire(AbilityGrant grant) => grant.Ability!.Activate(out AbilityActivationFailures _);

	public void TestGrantedAbilityActivatesAndItsCooldownBlocksReactivation()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ProbeAbilityDef probe = MakeProbe(1f, 1f, 1f, 1f, 1f, 1f);
		AbilityGrant grant = actor.Grant(probe, 1);
		Assert.NotNull(grant.Ability);
		Assert.True(Fire(grant));
		Assert.Eq(probe.Starts, 1);
		Assert.False(Fire(grant), "cooling down");
		actor.Tick(1.1);
		Assert.True(Fire(grant), "cooldown elapsed");
		Assert.Eq(probe.Starts, 2);
	}

	public void TestCooldownScalesWithTheAbilityLevel()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ProbeAbilityDef slow = MakeProbe(2f, 1.8f, 1.6f, 1.4f, 1.2f, 1f);
		AbilityGrant levelOne = actor.Grant(slow, 1);
		Assert.True(Fire(levelOne));
		actor.Tick(1.5);
		Assert.False(Fire(levelOne), "level 1 cooldown is 2 s");

		ActorEntity other = ActorFixture.BareActor(this, new StatBlock());
		ProbeAbilityDef fast = MakeProbe(2f, 1.8f, 1.6f, 1.4f, 1.2f, 1f);
		AbilityGrant levelSix = other.Grant(fast, 6);
		Assert.True(Fire(levelSix));
		other.Tick(1.1);
		Assert.True(Fire(levelSix), "level 6 cooldown is 1 s");
	}

	public void TestRevokeRemovesTheAbilityAndItsCooldown()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ProbeAbilityDef probe = MakeProbe(5f, 5f, 5f, 5f, 5f, 5f);
		AbilityGrant grant = actor.Grant(probe, 1);
		Assert.True(Fire(grant));
		actor.Revoke(grant);
		Assert.False(grant.Ability!.IsValid, "the handle is dead");
		Assert.Eq(actor.Abilities.GrantedAbilities.Count, 0);

		AbilityGrant again = actor.Grant(probe, 1);
		Assert.True(Fire(again), "a re-granted ability starts with a fresh cooldown");
		Assert.Eq(probe.Starts, 2);
	}

	public void TestRevokeCancelsARunningInstance()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ProbeAbilityDef probe = MakeProbe(1f, 1f, 1f, 1f, 1f, 1f);
		probe.StayActive = true;
		AbilityGrant grant = actor.Grant(probe, 1);
		Assert.True(Fire(grant));
		Assert.True(grant.Ability!.IsActive);
		actor.Revoke(grant);
		Assert.Eq(probe.Ends, 1, "OnEnded ran when the grant was removed");
	}

	public void TestRevokingNullOrTwiceIsHarmless()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		AbilityGrant grant = actor.Grant(MakeProbe(1f, 1f, 1f, 1f, 1f, 1f), 1);
		actor.Revoke(grant);
		actor.Revoke(grant);
		actor.Revoke(null);
		Assert.Eq(actor.Abilities.GrantedAbilities.Count, 0);
	}

	public void TestSameDefGrantedTwiceYieldsTwoIndependentGrants()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		ProbeAbilityDef probe = MakeProbe(1f, 1f, 1f, 1f, 1f, 1f);
		AbilityGrant first = actor.Grant(probe, 1);
		AbilityGrant second = actor.Grant(probe, 2);
		actor.Revoke(first);
		Assert.True(second.Ability!.IsValid, "revoking one grant leaves the other");
	}

	public void TestActiveAbilityDefValidateReportsMissingTagAndBadCooldown()
	{
		var noTag = new ProbeAbilityDef { Tag = string.Empty, Cooldown = DefFactory.CurveOf(1f) };
		Assert.True(noTag.Validate().Count >= 1);
		var noCurve = new ProbeAbilityDef { Tag = GameplayTags.AbilityProbe, Cooldown = null };
		Assert.Eq(noCurve.Validate().Count, 0, "no cooldown curve means no cooldown");
		var unregistered = new ProbeAbilityDef { Tag = "ability.nonexistent", Cooldown = DefFactory.CurveOf(1f) };
		Assert.True(AbilityFactory.Validate(unregistered).Count >= 1, "tags are checked against the Forge registry");
	}
}
```

`port/tests/Components/CleanupTest.cs`:

```csharp
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class CleanupTest : TestCase
{
	public void TestCleanupEndsRunningAbilitiesAndRemovesEveryEffect()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		var probe = new ProbeAbilityDef { Tag = GameplayTags.AbilityProbe, Cooldown = DefFactory.CurveOf(1f, 1f, 1f, 1f, 1f, 1f), StayActive = true };
		AbilityGrant grant = actor.Grant(probe, 1);
		actor.ApplyPassive(DefFactory.Might(), 3);
		Assert.True(grant.Ability!.Activate(out AbilityActivationFailures _));

		actor.Cleanup();

		Assert.Eq(probe.Ends, 1, "the running instance was ended");
		Assert.Eq(System.Linq.Enumerable.Count(actor.EffectsManager.GetActiveEffects()), 0);
		Assert.Eq(actor.Abilities.GrantedAbilities.Count, 0);
	}

	public void TestFreeingACleanedUpActorLeavesOtherEntitiesTicking()
	{
		ActorEntity doomed = ActorFixture.BareActor(this, new StatBlock());
		ActorEntity survivor = ActorFixture.BareActor(this, new StatBlock());
		doomed.Grant(new ProbeAbilityDef { Tag = GameplayTags.AbilityProbe, Cooldown = DefFactory.CurveOf(1f, 1f, 1f, 1f, 1f, 1f) }, 1);
		survivor.ApplyPassive(DefFactory.Might(), 2);

		doomed.Cleanup();
		doomed.Free();
		survivor.Tick(0.5);

		Assert.AlmostEq(survivor.Attributes[Stats.Attack].DisplayValue, 1.2, 0.0005);
	}

	public async System.Threading.Tasks.Task TestCleanupDeferredRunsAfterTheCurrentCallback()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		actor.ApplyPassive(DefFactory.Might(), 3);
		actor.CleanupDeferred();
		Assert.Eq(System.Linq.Enumerable.Count(actor.EffectsManager.GetActiveEffects()), 1, "not yet");
		await NextFrame();
		Assert.Eq(System.Linq.Enumerable.Count(actor.EffectsManager.GetActiveEffects()), 0, "after the frame");
	}

	public void TestCleanupTwiceIsHarmless()
	{
		ActorEntity actor = ActorFixture.BareActor(this, new StatBlock());
		actor.ApplyPassive(DefFactory.Might(), 3);
		actor.Cleanup();
		actor.Cleanup();
		Assert.True(GodotObject.IsInstanceValid(actor));
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: count greater than 0.

- [ ] **Step 3: Implement `ActiveAbilityDef`, `CooldownEffects`, `AbilityFactory`**

`port/src/Defs/ActiveAbilityDef.cs`:

```csharp
using System.Collections.Generic;
using Gamesmiths.Forge.Abilities;
using Gamesmiths.Forge.Godot.Resources;
using Godot;

namespace VampireSurvivor.Defs;

/// <summary>
/// An ability an actor performs (attack, dash, boss pattern). Authored as a resource with its numbers as exports;
/// the behavior is built from it on demand. Forge's AbilityData is assembled from this in Gas/AbilityFactory.
/// </summary>
[Tool]
public abstract partial class ActiveAbilityDef : ForgeAbilityBehavior
{
	/// <summary>The ability tag, for example "ability.volley". Must be registered in GameplayTags.</summary>
	[Export]
	public string Tag { get; set; } = string.Empty;

	/// <summary>Seconds over the ability level (x = 1..6). Null means the ability has no cooldown.</summary>
	[Export]
	public Curve? Cooldown { get; set; }

	/// <summary>Tags the owner carries while an instance runs (for example "state.invulnerable").</summary>
	[Export]
	public string[] OwnedTags { get; set; } = [];

	public abstract IAbilityBehavior CreateBehavior();

	public override IAbilityBehavior GetBehavior() => CreateBehavior();

	public virtual List<string> Validate()
	{
		var errors = new List<string>();
		if (string.IsNullOrEmpty(Tag))
		{
			errors.Add($"{GetType().Name}: Tag is empty");
		}

		return errors;
	}
}
```

`ActiveAbilityDef.Validate()` only checks what `Defs` can know (the tag is non-empty). Whether a tag is registered needs the Forge registry, which belongs to `Gas`, so `AbilityFactory.Validate(def)` (below) adds that check on top.

`port/src/Gas/CooldownEffects.cs`:

```csharp
using Gamesmiths.Forge.Effects;
using Gamesmiths.Forge.Effects.Components;
using Gamesmiths.Forge.Effects.Duration;
using Gamesmiths.Forge.Effects.Magnitudes;
using Gamesmiths.Forge.Godot.Core;
using Godot;

namespace VampireSurvivor.Gas;

public static class CooldownEffects
{
	/// <summary>
	/// A timed effect that grants <paramref name="cooldownTag"/> for the curve's value at the ability level (seconds).
	/// Forge blocks activation while the owner carries the tag.
	/// </summary>
	public static EffectData Build(string cooldownTag, Curve seconds)
	{
		var duration = new ModifierMagnitude(
			MagnitudeCalculationType.ScalableFloat,
			new ScalableFloat(1f, new ForgeCurve(seconds)));
		return new EffectData(
			$"Cooldown:{cooldownTag}",
			new DurationData(DurationType.HasDuration, duration),
			effectComponents: [new ModifierTagsEffectComponent(GameplayTags.ContainerOf(cooldownTag))]);
	}
}
```

`port/src/Gas/AbilityFactory.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Gamesmiths.Forge.Abilities;
using Gamesmiths.Forge.Effects;
using Gamesmiths.Forge.Godot.Core;
using Gamesmiths.Forge.Tags;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Gas;

public static class AbilityFactory
{
	private static readonly ConditionalWeakTable<ActiveAbilityDef, StrongBox<AbilityData>> Cache = [];

	/// <summary>The def's AbilityData, built once per def instance (Forge compares ability data by value).</summary>
	public static AbilityData Build(ActiveAbilityDef def)
	{
		if (Cache.TryGetValue(def, out StrongBox<AbilityData>? cached))
		{
			return cached.Value;
		}

		EffectData[]? cooldowns = def.Cooldown is null
			? null
			: [CooldownEffects.Build(GameplayTags.CooldownFor(def.Tag), def.Cooldown)];
		TagContainer? owned = def.OwnedTags.Length == 0
			? null
			: new TagContainer(ForgeManagers.Instance.TagsManager, def.OwnedTags.Select(GameplayTags.Request).ToHashSet());
		var data = new AbilityData(
			def.Tag,
			cooldownEffects: cooldowns,
			abilityTags: GameplayTags.ContainerOf(def.Tag),
			activationOwnedTags: owned,
			behaviorFactory: def.CreateBehavior);
		Cache.Add(def, new StrongBox<AbilityData>(data));
		return data;
	}

	/// <summary>Def validation plus the checks that need the Forge tag registry.</summary>
	public static List<string> Validate(ActiveAbilityDef def)
	{
		List<string> errors = def.Validate();
		foreach (string tag in new[] { def.Tag }.Concat(def.OwnedTags).Where(t => t.Length > 0))
		{
			try
			{
				GameplayTags.Request(tag);
			}
			catch (System.ArgumentException e)
			{
				errors.Add($"{def.GetType().Name}: {e.Message}");
			}
		}

		return errors;
	}
}
```

Note: `AbilityData` cooldown tags derive from `GameplayTags.CooldownFor(def.Tag)`, so every ability tag with a cooldown needs its `cooldown.*` twin in `GameplayTags` (already true for arrow, dash, volley, sweep, probe).

- [ ] **Step 4: Implement `AbilityGrant` and the `ActorEntity` additions**

`port/src/Components/AbilityGrant.cs`:

```csharp
using Gamesmiths.Forge.Abilities;
using Gamesmiths.Forge.Effects;

namespace VampireSurvivor.Components;

/// <summary>What <see cref="ActorEntity.Grant"/> returns: keep it to activate or revoke the ability.</summary>
public sealed record AbilityGrant(ActiveEffectHandle Effect, AbilityHandle? Ability, AbilityData Data);
```

Add to `ActorEntity` (add `using System.Linq;`, `using Gamesmiths.Forge.Abilities;`, `using Gamesmiths.Forge.Effects.Components;`, `using Gamesmiths.Forge.Effects.Duration;`, `using Gamesmiths.Forge.Effects.Magnitudes;`):

```csharp
	/// <summary>
	/// Grants an ability for as long as the returned grant lives. Forge 0.4.0 cannot revoke a permanent grant, so the
	/// grant is carried by an INFINITE effect: removing the effect (see <see cref="Revoke"/>) removes the ability.
	/// </summary>
	public AbilityGrant Grant(ActiveAbilityDef def, int level)
	{
		AbilityData data = AbilityFactory.Build(def);
		var config = new GrantAbilityConfig(
			data,
			new ScalableInt(level),
			AbilityDeactivationPolicy.CancelImmediately,
			AbilityDeactivationPolicy.CancelImmediately);
		var effectData = new EffectData(
			$"Grant:{def.Tag}",
			new DurationData(DurationType.Infinite),
			effectComponents: [new GrantAbilityEffectComponent([config])]);
		ActiveEffectHandle effect = EffectsManager.ApplyEffect(new Effect(effectData, new EffectOwnership(this, this)))!;
		Abilities.TryGetAbility(data, out AbilityHandle? ability);
		return new AbilityGrant(effect, ability, data);
	}

	/// <summary>Ends the ability (running instances are cancelled) and clears its cooldown so a re-grant starts fresh.</summary>
	public void Revoke(AbilityGrant? grant)
	{
		if (grant is null)
		{
			return;
		}

		if (grant.Effect.IsValid)
		{
			EffectsManager.RemoveEffect(grant.Effect, forceRemoval: true);
		}

		foreach (EffectData cooldown in grant.Data.CooldownEffects ?? [])
		{
			EffectsManager.RemoveEffectData(cooldown, forceRemoval: true);
		}
	}

	/// <summary>Ends every running ability and removes every effect. Call before freeing the actor.</summary>
	public void Cleanup()
	{
		Abilities.CancelAbilities(null, null);
		foreach (ActiveEffectHandle handle in EffectsManager.GetActiveEffects().ToArray())
		{
			if (handle.IsValid)
			{
				EffectsManager.RemoveEffect(handle, forceRemoval: true);
			}
		}
	}

	/// <summary>Cleanup after the current physics callback finishes (death happens inside physics callbacks).</summary>
	public void CleanupDeferred() => CallDeferred(MethodName.Cleanup);
```
Also add `using VampireSurvivor.Defs;` is already present for `StatBlock`; `ActiveAbilityDef` lives there too.

- [ ] **Step 5: Run**

```bash
RUNNER=cs tools/run_tests.sh | grep -E "AbilityGrantTest|CleanupTest|^----|FAIL|  -"
```
Expected: seven `PASS  Components/AbilityGrantTest::...`, four `PASS  Components/CleanupTest::...`, `0 failed`.
**Gate item (d) and the 0.4.0 revoke finding live here.** If `Revoke` leaves `GrantedAbilities.Count > 0`, or `Cleanup`'s forced removal does not end a running instance, record exactly which assertion failed; the likely remedy is to call `grant.Ability.Cancel()` before removing the effect. Do not weaken the tests.

- [ ] **Step 6: Commit**

```bash
git add port/src/Defs/ActiveAbilityDef.cs port/src/Gas port/src/Components port/tests/Support/ProbeAbilityDef.cs port/tests/Components
git commit -m "feat(gas): effect-granted abilities, cooldowns, revoke and cleanup" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 10: `Health` component

**Files:**
- Create: `port/src/Components/Health.cs`
- Test: `port/tests/Components/HealthTest.cs`

**Interfaces:**
- Consumes: `ActorEntity`, `DamageEffects`, `Stats`.
- Produces: `VampireSurvivor.Components.Health : Node` — `Bind(ActorEntity entity)`, `float Current()`, `float Maximum()`, `bool IsDead`; signals `Changed(float current, float maximum)`, `Damaged(float amount)`, `Died()`. The legacy `instigator` argument of `damaged` is dropped (its only consumer was the hit flash).

- [ ] **Step 1: Write the failing test**

`port/tests/Components/HealthTest.cs`:

```csharp
using System.Collections.Generic;
using VampireSurvivor.Components;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Components;

public class HealthTest : TestCase
{
	private static StatBlock MakeStats(float maxHealth) => new() { MaxHealth = maxHealth };

	private Health HealthFor(ActorEntity actor)
	{
		var health = new Health();
		actor.AddChild(health);
		return health;
	}

	public void TestBindEmitsInitialValues()
	{
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(50f));
		Health health = HealthFor(victim);
		var seen = new List<(float, float)>();
		health.Changed += (current, maximum) => seen.Add((current, maximum));
		health.Bind(victim);
		Assert.SequenceEq(seen, new[] { (50f, 50f) });
	}

	public void TestDamageEmitsDamagedAndChanged()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f));
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(50f));
		Health health = HealthFor(victim);
		health.Bind(victim);
		var damages = new List<float>();
		var changes = new List<float>();
		health.Damaged += amount => damages.Add(amount);
		health.Changed += (current, _) => changes.Add(current);
		DamageEffects.Hit(attacker, victim, 12f);
		Assert.SequenceEq(damages, new[] { 12f });
		Assert.SequenceEq(changes, new[] { 38f });
		Assert.AlmostEq(health.Current(), 38.0);
	}

	public void TestLethalDamageEmitsDiedExactlyOnce()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f));
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(20f));
		Health health = HealthFor(victim);
		health.Bind(victim);
		int deaths = 0;
		health.Died += () => deaths++;
		DamageEffects.Hit(attacker, victim, 500f);
		DamageEffects.Hit(attacker, victim, 500f);
		Assert.Eq(deaths, 1);
		Assert.True(health.IsDead);
	}

	public void TestMaxHealthChangesEmitChangedWithoutDamage()
	{
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(100f));
		Health health = HealthFor(victim);
		health.Bind(victim);
		int damaged = 0;
		var maxima = new List<float>();
		health.Damaged += _ => damaged++;
		health.Changed += (_, maximum) => maxima.Add(maximum);
		TestEffects.Flat(victim, Stats.MaxHealth, 5000);
		Assert.Eq(damaged, 0, "raising max is not damage");
		Assert.SequenceEq(maxima, new[] { 150f });
	}

	public void TestFreeingTheHealthNodeStopsItsSubscriptions()
	{
		ActorEntity attacker = ActorFixture.BareActor(this, MakeStats(100f));
		ActorEntity victim = ActorFixture.BareActor(this, MakeStats(50f));
		Health health = HealthFor(victim);
		health.Bind(victim);
		health.Free();
		DamageEffects.Hit(attacker, victim, 10f);
		Assert.AlmostEq(victim.Attributes[Stats.Health].DisplayValue, 40.0);
	}
}
```

- [ ] **Step 2: Run to see it fail**

```bash
dotnet build VampireSurvivor.csproj -nologo -v:q 2>&1 | grep -c "error CS"
```
Expected: count greater than 0 (`Health` missing).

- [ ] **Step 3: Implement `Health`**

`port/src/Components/Health.cs`:

```csharp
using Gamesmiths.Forge.Attributes;
using Godot;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Components;

/// <summary>
/// Domain view of an actor's vitals. It observes the entity's attributes and never stores hit points itself.
/// Bind it AFTER the actor's stats were applied so the initial values do not count as damage.
/// </summary>
public partial class Health : Node
{
	[Signal]
	public delegate void ChangedEventHandler(float current, float maximum);

	[Signal]
	public delegate void DamagedEventHandler(float amount);

	[Signal]
	public delegate void DiedEventHandler();

	private EntityAttribute? _health;
	private EntityAttribute? _maxHealth;

	public bool IsDead { get; private set; }

	public void Bind(ActorEntity entity)
	{
		Unbind();
		_health = entity.Attributes[Stats.Health];
		_maxHealth = entity.Attributes[Stats.MaxHealth];
		_health.OnValueChanged += OnHealthChanged;
		_maxHealth.OnValueChanged += OnMaxHealthChanged;
		EmitSignal(SignalName.Changed, Current(), Maximum());
	}

	public float Current() => _health?.DisplayValue ?? 0f;

	public float Maximum() => _maxHealth?.DisplayValue ?? 0f;

	public override void _ExitTree() => Unbind();

	private void Unbind()
	{
		if (_health is not null)
		{
			_health.OnValueChanged -= OnHealthChanged;
		}

		if (_maxHealth is not null)
		{
			_maxHealth.OnValueChanged -= OnMaxHealthChanged;
		}

		_health = null;
		_maxHealth = null;
	}

	private void OnHealthChanged(EntityAttribute attribute, int change)
	{
		if (change < 0)
		{
			EmitSignal(SignalName.Damaged, attribute.ToDisplayValue(-change));
		}

		EmitSignal(SignalName.Changed, Current(), Maximum());
		if (attribute.CurrentValue <= 0 && !IsDead)
		{
			IsDead = true;
			EmitSignal(SignalName.Died);
		}
	}

	private void OnMaxHealthChanged(EntityAttribute attribute, int change)
	{
		EmitSignal(SignalName.Changed, Current(), Maximum());
	}
}
```
- [ ] **Step 4: Run**

```bash
RUNNER=cs tools/run_tests.sh | grep -E "HealthTest|^----|FAIL|  -"
```
Expected: five `PASS  Components/HealthTest::...`, `0 failed`.
Note for Task 11: lowering `MaxHealth` pulls `Health` down inside `AttributeOnValueChanged`, so `Health` also emits `Damaged` for the pulled-down amount — the same behaviour as the GDScript version (its pull-down also went through `attribute_changed`). Add a test only if the owner wants it to differ.

- [ ] **Step 5: Commit**

```bash
git add port/src/Components/Health.cs port/tests/Components/HealthTest.cs
git commit -m "feat(components): Health observes ActorEntity vitals" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Gate report, spec update and project docs

**Files:**
- Create: `docs/superpowers/notes/2026-10-14-slice1-forge-gate.md`
- Modify: `docs/superpowers/specs/2026-10-14-csharp-forge-port-design.md`, `CLAUDE.md`

- [ ] **Step 1: Run everything**

```bash
tools/run_tests.sh | grep -E "^==|^----|FAIL|!!"
```
Expected: `== GDScript suite`, `---- BASELINE passed, 0 failed ----`, `== C# suite`, `---- <total> passed, 0 failed ----`, no `FAIL`/`!!` lines. Note the C# total.

- [ ] **Step 2: Write the gate report**

Create `docs/superpowers/notes/2026-10-14-slice1-forge-gate.md` with this structure, filling every cell from the actual run (no cell may stay empty):

```markdown
# Slice 1 — Forge go/no-go

Date: <today>
Forge: addon `v0.4.0`, NuGet `Gamesmiths.Forge` 0.4.0. C# suite: <total> tests green; GDScript suite: <BASELINE> green.

| # | Question | Test(s) | Result | Notes / fallback used |
|---|---|---|---|---|
| a | Lowering MaxHealth pulls Health down; raising never heals | `ActorEntityTest::TestLoweringMaxHealthPullsHealthDown`, `::TestRaisingMaxHealthNeverHeals`, `PassiveTest::TestRemovingVitalityPullsHealthBackToTheBaseMax` | pass / fail | |
| b | `DamageExecution` reads the source's Attack in raw units | `DamageTest::TestDamageScalesWithSourceAttack`, `::TestFractionalAttackKeepsTwoDecimalsOfDamage` | pass / fail | custom execution or `Hit` fallback |
| c | Passive apply -> remove -> re-apply at another level; two slots stack | `PassiveTest::TestReapplyAtAnotherLevelUsesTheNewLevel`, `::TestTwoSlotsWithTheSamePassiveStack` | pass / fail | |
| d | Actor with active effects/abilities cleaned up and freed safely | `CleanupTest::*` | pass / fail | |
| e | (new) 0.4.0 has no public revoke: abilities are granted through an INFINITE effect | `AbilityGrantTest::TestRevoke*` | pass / fail | |

## Decision
GO / NO-GO for Forge, with one paragraph of reasoning. If NO-GO, name the failing row and the candidate replacement (a thin in-house attribute/effect layer behind the same `ActorEntity` API, or GodotGAS via GDScript).

## Surprises
Anything that differed from the spec (API names, event timing, rounding), one bullet each.
```

- [ ] **Step 3: Update the spec**

In `docs/superpowers/specs/2026-10-14-csharp-forge-port-design.md`:
- Section 2 "Still unverified": mark items 1-4 with their results from the report and add the new finding "0.4.0 cannot revoke a permanent grant; abilities are granted through an INFINITE `GrantAbilityEffectComponent` effect and removed by removing it".
- Section 4 mapping row for `gas/actor_asc.tscn`: change `Grant(ActiveAbilityDef, level) -> AbilityHandle, Revoke(handle)` to `Grant -> AbilityGrant`, `Revoke(AbilityGrant)`.
- Section 6 table row `tools/run_tests.sh`: replace `LANG=gd|cs|both` with `RUNNER=gd|cs|both`.
- Section 1 header line: `Godot.NET.Sdk` stays "4.7.x"; note the vendored addon is the `forge-godot` tag `v0.4.0`.
- Section 3: note `Wallet.Kind` became the top-level `CurrencyKind`, `Grade` helpers are extension methods (`Tint()` instead of `Color()`), `StatBlock` etc. use the C# naming.

- [ ] **Step 4: Update `CLAUDE.md` Commands**

Replace the "Run all tests" bullet with:

```
- Run all tests: `tools/run_tests.sh` (`RUNNER=gd|cs|both`, default both; `FILTER=<substring>` for one file/label; `IMPORT=1` if the
  editor is closed; `NO_BUILD=1` skips `dotnet build`; `TEST_SUITE_TIMEOUT=<seconds>`, default 120). Needs the **.NET** Godot build
  (`/d/CM/Godot/Godot_v4.7.2-stable_mono_win64/...`); open the project with it, not the standard build. GDScript tests live in `tests/`,
  C# tests in `port/tests/` (mirroring `port/src/`), both discovered by their runner scenes.
- Regenerate Forge tags after editing `GameplayTags.All`: `Godot --headless --path . res://port/tools/sync_tags.tscn`.
```
and add one line under the top description: `A C# + Forge port is in progress under port/ (see docs/superpowers/specs/2026-10-14-csharp-forge-port-design.md); the GDScript tree stays the regression reference until cutover.`

- [ ] **Step 5: Commit**

```bash
git add docs/superpowers/notes docs/superpowers/specs/2026-10-14-csharp-forge-port-design.md docs/superpowers/plans/2026-10-14-csharp-port-slices-0-1.md CLAUDE.md
git commit -m "docs: Forge go/no-go report, spec update and test commands" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 6: Hand the decision to the owner**

Report the table and the GO/NO-GO to the owner. If GO, slices 2-5 get their own plans (next: slice 2 core loop). If NO-GO, stop; do not start slice 2.
