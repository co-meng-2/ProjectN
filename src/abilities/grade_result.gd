class_name GradeResult
extends RefCounted
## What a promote/reroll attempt produced. Only OK changed anything.

enum Outcome { OK, NOT_ENOUGH, NOT_APPLICABLE, EMPTY_POOL }

var outcome: Outcome = Outcome.NOT_APPLICABLE
var def: AbilityDef
var grade: Grade.Value = Grade.Value.NONE


static func failed(p_outcome: Outcome) -> GradeResult:
	var result := GradeResult.new()
	result.outcome = p_outcome
	return result


static func ok(p_def: AbilityDef, p_grade: Grade.Value) -> GradeResult:
	var result := GradeResult.new()
	result.outcome = Outcome.OK
	result.def = p_def
	result.grade = p_grade
	return result
