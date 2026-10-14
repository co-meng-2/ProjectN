using Gamesmiths.Forge.Abilities;
using Gamesmiths.Forge.Effects;

namespace VampireSurvivor.Components;

/// <summary>What <see cref="ActorEntity.Grant"/> returns: keep it to activate or revoke the ability.</summary>
public sealed record AbilityGrant(ActiveEffectHandle Effect, AbilityHandle? Ability, AbilityData Data);
