extends TestCase


func test_new_run_starts_clean() -> void:
	var state := RunState.new(DefFactory.stage(), PlayerProfile.new(), 7)
	assert_eq(state.wallet.gold, 0)
	assert_eq(state.wallet.stone, 0)
	assert_eq(state.kills, 0)
	assert_eq(state.rng.seed_value, 7)


func test_to_result_reports_the_run() -> void:
	var state := RunState.new(DefFactory.stage(), PlayerProfile.new())
	state.wallet.add(Wallet.Kind.GOLD, 12)
	state.wallet.add(Wallet.Kind.STONE, 3)
	state.kills = 9
	var result := state.to_result(RunResult.Outcome.CLEARED, 42.0)
	assert_eq(result.outcome, RunResult.Outcome.CLEARED)
	assert_eq(result.gold, 12)
	assert_eq(result.stone, 3)
	assert_eq(result.kills, 9)
	assert_almost_eq(result.elapsed, 42.0)


func test_wallet_is_seeded_from_the_stage_def() -> void:
	var def := DefFactory.stage()
	def.start_gold = 200
	def.start_stone = 3
	var state := RunState.new(def, PlayerProfile.new(), 7)
	assert_eq(state.wallet.gold, 200)
	assert_eq(state.wallet.stone, 3)
