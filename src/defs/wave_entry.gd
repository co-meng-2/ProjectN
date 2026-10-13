class_name WaveEntry
extends Resource
## One surge: `count` enemies of one type arrive at stage time `time`.

@export var time: float = 30.0
@export var enemy: EnemyDef
@export var count: int = 10
@export var label: String = "Wave"
