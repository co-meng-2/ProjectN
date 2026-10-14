#!/usr/bin/env bash
# Runs the headless test suites.
# Env: GODOT_BIN, FILTER (test label substring), IMPORT=1 (run --import first),
#      NO_BUILD=1 (skip dotnet build), TEST_SUITE_TIMEOUT (seconds, default 120).
set -u
cd "$(dirname "$0")/.."
GODOT_BIN="${GODOT_BIN:-/d/CM/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe}"
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

run_suite res://tests/run_tests.tscn cs
