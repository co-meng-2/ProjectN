namespace VampireSurvivor.Defs;

/// <summary>One permanent bonus carried on a <see cref="PlayerProfile"/>: apply <see cref="Passive"/> at <see cref="Level"/>.</summary>
public sealed record SkillBonus(PassiveDef Passive, int Level);
