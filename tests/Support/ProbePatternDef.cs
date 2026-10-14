using Gamesmiths.Forge.Abilities;
using VampireSurvivor.Boss;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Tests.Support;

/// <summary>
/// Test double pattern: counts starts, completes at once unless <see cref="Hold"/> (then <see cref="Release"/> ends it),
/// can refuse to start, and records aborts and stepped time.
/// </summary>
public sealed partial class ProbePatternDef : PatternDef
{
	private Behavior? _current;

	public ProbePatternDef()
	{
		Tag = GameplayTags.AbilityProbe;
	}

	public bool Hold { get; set; }

	public bool Refuse { get; set; }

	public int Starts { get; set; }

	public int Aborts { get; set; }

	public float Stepped { get; set; }

	public void Release() => _current?.Release();

	public override IAbilityBehavior CreateBehavior() => _current = new Behavior(this);

	private sealed class Behavior(ProbePatternDef owner) : PatternBehavior
	{
		public void Release() => Complete();

		protected override bool Begin()
		{
			if (owner.Refuse)
			{
				return false;
			}

			owner.Starts++;
			if (!owner.Hold)
			{
				Complete();
			}

			return true;
		}

		protected override void Advance(float delta) => owner.Stepped += delta;

		protected override void Abort() => owner.Aborts++;
	}
}
