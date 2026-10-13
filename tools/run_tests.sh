#!/usr/bin/env bash
# Runs the headless test suite. Env: GODOT_BIN, FILTER (path substring), IMPORT=1 (run --import first).
set -u
cd "$(dirname "$0")/.."
GODOT_BIN="${GODOT_BIN:-/d/CM/Godot/Godot_v4.7.2-stable_win64.exe}"
if [ "${IMPORT:-0}" = "1" ]; then
	"$GODOT_BIN" --headless --path . --import >/dev/null 2>&1
fi
timeout_seconds="${TEST_SUITE_TIMEOUT:-120}"
output="$(timeout "$timeout_seconds" "$GODOT_BIN" --headless --path . res://tests/run_tests.tscn -- --filter="${FILTER:-}" 2>&1)"
status=$?
echo "$output" | grep -v "ObjectDB instances were leaked\|resources still in use\|at: cleanup\|at: clear\|BUG: Unreferenced static string\|\[godot_ai game_helper\]"
if [ "$status" -eq 124 ]; then
	echo "!! test run timed out after ${timeout_seconds}s (parse error or hang?)"
	exit 124
fi
if echo "$output" | grep -q "SCRIPT ERROR\|Parse Error\|Compile Error"; then
	echo "!! script errors detected"
	exit 1
fi
exit $status
