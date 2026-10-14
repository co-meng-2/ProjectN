using Gamesmiths.Forge.Attributes;
using Godot;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Components;

/// <summary>
/// Domain view of an actor's vitals. It observes the entity's attributes and never stores hit points itself.
/// Bind it AFTER the actor's stats were applied so the initial values do not count as damage.
/// </summary>
public partial class Health : Node
{
	[Signal]
	public delegate void ChangedEventHandler(float current, float maximum);

	[Signal]
	public delegate void DamagedEventHandler(float amount);

	[Signal]
	public delegate void DiedEventHandler();

	private EntityAttribute? _health;
	private EntityAttribute? _maxHealth;

	public bool IsDead { get; private set; }

	public void Bind(ActorEntity entity)
	{
		Unbind();
		_health = entity.Attributes[Stats.Health];
		_maxHealth = entity.Attributes[Stats.MaxHealth];
		_health.OnValueChanged += OnHealthChanged;
		_maxHealth.OnValueChanged += OnMaxHealthChanged;
		EmitSignal(SignalName.Changed, Current(), Maximum());
	}

	public float Current() => _health?.DisplayValue ?? 0f;

	public float Maximum() => _maxHealth?.DisplayValue ?? 0f;

	public override void _ExitTree() => Unbind();

	private void Unbind()
	{
		if (_health is not null)
		{
			_health.OnValueChanged -= OnHealthChanged;
		}

		if (_maxHealth is not null)
		{
			_maxHealth.OnValueChanged -= OnMaxHealthChanged;
		}

		_health = null;
		_maxHealth = null;
	}

	private void OnHealthChanged(EntityAttribute attribute, int change)
	{
		if (change < 0)
		{
			EmitSignal(SignalName.Damaged, attribute.ToDisplayValue(-change));
		}

		EmitSignal(SignalName.Changed, Current(), Maximum());
		if (attribute.CurrentValue <= 0 && !IsDead)
		{
			IsDead = true;
			EmitSignal(SignalName.Died);
		}
	}

	private void OnMaxHealthChanged(EntityAttribute attribute, int change)
	{
		EmitSignal(SignalName.Changed, Current(), Maximum());
	}
}
