using System.Threading.Tasks;
using Gamesmiths.Forge.Abilities;
using Godot;
using VampireSurvivor.Abilities;
using VampireSurvivor.Actors;
using VampireSurvivor.Components;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;
using VampireSurvivor.Gas;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Actors;

public class PlayerTest : TestCase
{
	private static VolleyDef MakeArrow(bool autoFire) => new()
	{
		Tag = GameplayTags.AbilityArrow,
		Cooldown = DefFactory.CurveOf(0.7f, 0.7f, 0.7f, 0.7f, 0.7f, 0.7f),
		AutoFire = autoFire,
		ProjectileScene = GD.Load<PackedScene>("res://port/src/Actors/Projectile/projectile.tscn"),
		Projectile = new ProjectileDef { Texture = DefFactory.Texture(8, 8), Speed = 420f },
		ArrowCount = DefFactory.CurveOf(1f, 1f, 1f, 1f, 1f, 1f),
		Damage = DefFactory.CurveOf(12f, 24f, 36f, 48f, 60f, 72f),
		AttackRange = 260f,
	};

	private static DashDef MakeDash() => new()
	{
		Tag = GameplayTags.AbilityDash,
		Cooldown = DefFactory.CurveOf(1.2f, 1.2f, 1.2f, 1.2f, 1.2f, 1.2f),
	};

	private Node2D MakeEnemy(Vector2 at)
	{
		var enemy = Adopt(new Node2D { GlobalPosition = at });
		enemy.AddToGroup(Groups.Enemies);
		return enemy;
	}

	private static Node Layer(Player player) => player.Context!.ProjectileLayer;

	private bool Hit(Player player, float amount)
	{
		ActorEntity attacker = ActorFixture.BareActor(this, new StatBlock());
		double before = player.Health.Current();
		DamageEffects.Hit(attacker, player.Entity, amount);
		return player.Health.Current() < before;
	}

	public void TestSetupAppliesProfileStatsAndJoinsTheGroup()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.Stats = new StatBlock { MaxHealth = 77f, MoveSpeed = 140f };
		Player player = ActorFixture.Player(this, profile);
		Assert.AlmostEq(player.Health.Current(), 77.0);
		Assert.AlmostEq(player.Entity.Attributes[Stats.MoveSpeed].DisplayValue, 140.0);
		Assert.True(player.IsInGroup(Groups.Player));
		Assert.AlmostEq(player.GainMultiplier(CurrencyKind.Gold), 1.0);
	}

	public void TestContextCarriesTheActorAndTheRunsWallet()
	{
		var wallet = new Wallet();
		Player player = ActorFixture.Player(this, wallet: wallet);
		Assert.True(player.Context!.Actor == player);
		Assert.True(player.Context.Wallet == wallet);
	}

	public void TestInnateAbilitiesAreGrantedOnSetup()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.InnateAbilities.Add(MakeDash());
		Player player = ActorFixture.Player(this, profile);
		Assert.Eq(player.Entity.Abilities.GrantedAbilities.Count, 1);
	}

	public void TestVolleyWithoutATargetFiresNothingAndStartsNoCooldown()
	{
		Player player = ActorFixture.Player(this);
		AbilityGrant arrow = player.Grant(MakeArrow(autoFire: false), 1);
		for (int i = 0; i < 3; i++)
		{
			arrow.Ability!.Activate(out AbilityActivationFailures _);
		}

		Assert.False(player.Entity.Tags.AllTags.HasTag(GameplayTags.Request(GameplayTags.CooldownArrow)));
		Assert.Eq(Layer(player).GetChildCount(), 0);
	}

	public void TestVolleyFiresAtTheNearestEnemyThenCoolsDown()
	{
		Player player = ActorFixture.Player(this);
		MakeEnemy(new Vector2(400, 0));
		MakeEnemy(new Vector2(120, 0));
		AbilityGrant arrow = player.Grant(MakeArrow(autoFire: false), 1);
		Assert.True(arrow.Ability!.Activate(out AbilityActivationFailures _));
		Assert.Eq(Layer(player).GetChildCount(), 1, "only the enemy within 260 px");
		Assert.True(player.Entity.Tags.AllTags.HasTag(GameplayTags.Request(GameplayTags.CooldownArrow)));
		Assert.False(arrow.Ability.Activate(out AbilityActivationFailures _), "blocked by cooldown");
	}

	public async Task TestAutoFireShootsWhenAnEnemyComesIntoRange()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.InnateAbilities.Add(MakeArrow(autoFire: true));
		Player player = ActorFixture.Player(this, profile);
		await WaitSeconds(0.2);
		Assert.Eq(Layer(player).GetChildCount(), 0, "nothing to shoot yet");
		MakeEnemy(new Vector2(100, 0));
		await WaitSeconds(0.3);
		Assert.True(Layer(player).GetChildCount() >= 1);
		Assert.True(player.Entity.Tags.AllTags.HasTag(GameplayTags.Request(GameplayTags.CooldownArrow)));
	}

	public async Task TestDashKeyAbilityIsInvulnerableOnlyWhileActive()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.InnateAbilities.Add(MakeDash());
		Player player = ActorFixture.Player(this, profile);
		Assert.True(player.Entity.TryActivate(GameplayTags.AbilityDash));
		Assert.False(Hit(player, 10f), "damage refused while dashing");
		await WaitSeconds(0.5);
		Assert.True(Hit(player, 10f));
		Assert.AlmostEq(player.Health.Current(), 90.0);
	}

	public async Task TestLethalDamageEmitsDiedAndStopsTheShooting()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.InnateAbilities.Add(MakeArrow(autoFire: true));
		Player player = ActorFixture.Player(this, profile);
		int deaths = 0;
		player.Died += () => deaths++;
		Hit(player, 9999f);
		Assert.Eq(deaths, 1);
		MakeEnemy(new Vector2(100, 0));
		await WaitSeconds(0.3);
		Assert.Eq(Layer(player).GetChildCount(), 0, "a dead player no longer shoots");
	}

	public void TestGrantAndRevokeKeepTheAutoCasterInSync()
	{
		Player player = ActorFixture.Player(this);
		AbilityGrant grant = player.Grant(MakeArrow(autoFire: true), 1);
		Assert.Eq(player.AutoCaster.Count, 1);
		player.Revoke(grant);
		Assert.Eq(player.AutoCaster.Count, 0);
		Assert.Eq(player.Entity.Abilities.GrantedAbilities.Count, 0);
		player.Revoke(null);
	}

	public void TestPlayerAttackScalesWithTheAttackStat()
	{
		PlayerProfile profile = DefFactory.Profile();
		profile.Stats = new StatBlock { Attack = 2f };
		Player player = ActorFixture.Player(this, profile);
		var (victim, _) = ActorFixture.HurtTarget(this, new Vector2(10, 0));
		DamageEffects.Hit(player.Entity, victim, 10f);
		Assert.AlmostEq(victim.Attributes[Stats.Health].DisplayValue, 80.0);
	}
}
