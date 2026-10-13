class_name Hud
extends CanvasLayer
## Read-only view of the stage. It binds to models through signals and never mutates them.
## Main binds it (the stage layer must not know the HUD) and forwards the event and boss signals.

## Bound by Main; empty until then.
@onready var ability_bar: AbilityBar = %AbilityBar
@onready var _player_bar: ProgressBar = %PlayerBar
@onready var _building_bar: ProgressBar = %BuildingBar
@onready var _wallet_label: Label = %WalletLabel
@onready var _timeline: TimelineBar = %Timeline
@onready var _result_label: Label = %ResultLabel
@onready var _event_banner: EventBanner = %EventBanner
@onready var _objective_arrows: ObjectiveArrows = %ObjectiveArrows
@onready var _boss_bar: BossBar = %BossBar


func bind(player_health: Health, building_health: Health, wallet: Wallet, clock: StageClock, timeline: Timeline, event_times: PackedFloat32Array = PackedFloat32Array()) -> void:
	player_health.changed.connect(func(current: float, maximum: float) -> void: _set_bar(_player_bar, current, maximum))
	building_health.changed.connect(func(current: float, maximum: float) -> void: _set_bar(_building_bar, current, maximum))
	_set_bar(_player_bar, player_health.current(), player_health.maximum())
	_set_bar(_building_bar, building_health.current(), building_health.maximum())
	wallet.changed.connect(_on_wallet_changed)
	_on_wallet_changed(wallet.gold, wallet.stone, wallet.tickets)
	_timeline.configure(timeline.duration, timeline.marks, event_times)
	clock.ticked.connect(_timeline.set_elapsed)


## Stage.events.event_started.
func show_event(def: EventDef, event: StageEvent) -> void:
	_event_banner.show_event(def, event)
	_objective_arrows.track(event)


## Stage.events.event_progressed.
func show_event_progress(_def: EventDef, text: String) -> void:
	_event_banner.show_progress(text)


## Stage.events.event_ended.
func show_event_result(def: EventDef, success: bool) -> void:
	_event_banner.show_result(def, success)
	_objective_arrows.clear()


## Stage.events.event_aborted.
func show_event_aborted(def: EventDef) -> void:
	_event_banner.show_aborted(def)
	_objective_arrows.clear()


## Stage.boss_appeared.
func show_boss(boss_health: Health, display_name: String) -> void:
	_boss_bar.show_boss(boss_health, display_name)
	_timeline.show_boss()


func show_result(result: RunResult) -> void:
	var title := "STAGE CLEARED" if result.outcome == RunResult.Outcome.CLEARED else "GAME OVER"
	_result_label.text = "%s\nKills %d   Gold %d   Stone %d\nPress R to restart" % [title, result.kills, result.gold, result.stone]
	_result_label.visible = true


func _set_bar(bar: ProgressBar, current: float, maximum: float) -> void:
	bar.max_value = maxf(maximum, 1.0)
	bar.value = current


func _on_wallet_changed(gold: int, stone: int, tickets: int) -> void:
	_wallet_label.text = "Gold %d   Stone %d   Ticket %d" % [gold, stone, tickets]
