using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Abilities;

public enum GradeOutcome
{
	Ok,
	NotEnough,
	NotApplicable,
	EmptyPool,
}

/// <summary>What a promote/reroll attempt produced. Only <see cref="GradeOutcome.Ok"/> changed anything.</summary>
public sealed record GradeResult(GradeOutcome Outcome, AbilityDef? Def = null, Grade Grade = Grade.None)
{
	public static GradeResult Failed(GradeOutcome outcome) => new(outcome);

	public static GradeResult Ok(AbilityDef def, Grade grade) => new(GradeOutcome.Ok, def, grade);
}
