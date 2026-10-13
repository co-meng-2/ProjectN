class_name Hud
extends CanvasLayer
## Read-only view of the stage. It binds to models through signals and never mutates them.

## Bound by Main (the stage layer must not know the HUD); empty until then.
@onready var ability_bar: AbilityBar = %AbilityBar
@onready var _player_bar: ProgressBar = %PlayerBar
@onready var _building_bar: ProgressBar = %BuildingBar
@onready var _wallet_label: Label = %WalletLabel
@onready var _timeline: TimelineBar = %Timeline
@onready var _result_label: Label = %ResultLabel


func bind(player_health: Health, building_health: Health, wallet: Wallet, clock: StageClock, timeline: Timeline) -> void:
	player_health.changed.connect(func(current: float, maximum: float) -> void: _set_bar(_player_bar, current, maximum))
	building_health.changed.connect(func(current: float, maximum: float) -> void: _set_bar(_building_bar, current, maximum))
	_set_bar(_player_bar, player_health.current(), player_health.maximum())
	_set_bar(_building_bar, building_health.current(), building_health.maximum())
	wallet.changed.connect(_on_wallet_changed)
	_on_wallet_changed(wallet.gold, wallet.stone)
	_timeline.configure(timeline.duration, timeline.marks)
	clock.ticked.connect(_timeline.set_elapsed)


func show_result(result: RunResult) -> void:
	var title := "STAGE CLEARED" if result.outcome == RunResult.Outcome.CLEARED else "GAME OVER"
	_result_label.text = "%s\nKills %d   Gold %d   Stone %d\nPress R to restart" % [title, result.kills, result.gold, result.stone]
	_result_label.visible = true


func _set_bar(bar: ProgressBar, current: float, maximum: float) -> void:
	bar.max_value = maxf(maximum, 1.0)
	bar.value = current


func _on_wallet_changed(gold: int, stone: int) -> void:
	_wallet_label.text = "Gold %d   Stone %d" % [gold, stone]
