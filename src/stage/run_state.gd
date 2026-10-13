class_name RunState
extends RefCounted
## Mutable state of one stage run. Dies with the stage; nothing here is global.

var stage_def: StageDef
var profile: PlayerProfile
var rng: RunRng
var wallet: Wallet = Wallet.new()
var kills: int = 0


func _init(p_stage_def: StageDef, p_profile: PlayerProfile, run_seed: int = 0) -> void:
	stage_def = p_stage_def
	profile = p_profile
	rng = RunRng.new(run_seed)
	wallet.add(Wallet.Kind.GOLD, p_stage_def.start_gold)
	wallet.add(Wallet.Kind.STONE, p_stage_def.start_stone)


func to_result(outcome: RunResult.Outcome, elapsed: float) -> RunResult:
	var result := RunResult.new()
	result.outcome = outcome
	result.elapsed = elapsed
	result.kills = kills
	result.gold = wallet.gold
	result.stone = wallet.stone
	return result
