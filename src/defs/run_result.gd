class_name RunResult
extends RefCounted
## What a finished stage run reports to its caller.

enum Outcome { CLEARED, FAILED }

var outcome: Outcome = Outcome.FAILED
var elapsed: float = 0.0
var kills: int = 0
var gold: int = 0
var stone: int = 0
