using Gamesmiths.Forge.Abilities;
using Gamesmiths.Forge.Effects;

namespace VampireSurvivor.Components;

/// <summary>
/// What <see cref="ActorEntity.Grant"/> returns: keep it to activate or revoke the ability. <see cref="CooldownTag"/> is the
/// tag its cooldown grants ("cooldown.volley", or a slot's own tag such as "cooldown.slot.a").
/// </summary>
public sealed record AbilityGrant(ActiveEffectHandle Effect, AbilityHandle? Ability, AbilityData Data, string CooldownTag);
