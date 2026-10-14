using Godot;
using VampireSurvivor.Gas;

namespace VampireSurvivor.Components;

/// <summary>Drives a CharacterBody2D at the entity's move speed, with a dash override.</summary>
public partial class Mover : Node
{
	[Signal]
	public delegate void DashStartedEventHandler();

	[Signal]
	public delegate void DashFinishedEventHandler();

	private CharacterBody2D? _body;
	private ActorEntity? _entity;
	private Vector2 _direction = Vector2.Zero;
	private Vector2 _dashVelocity = Vector2.Zero;
	private double _dashTimeLeft;

	public Vector2 LastDirection { get; private set; } = Vector2.Right;

	public Vector2 Direction => _direction;

	public bool IsDashing => _dashTimeLeft > 0.0;

	public bool IsMoving => _direction != Vector2.Zero || IsDashing;

	public void Bind(CharacterBody2D body, ActorEntity entity)
	{
		_body = body;
		_entity = entity;
	}

	public void SetDirection(Vector2 heading)
	{
		_direction = heading.LimitLength(1f);
		if (_direction != Vector2.Zero)
		{
			LastDirection = _direction.Normalized();
		}
	}

	public void Dash(Vector2 dashDirection, float speed, float duration)
	{
		Vector2 heading = dashDirection != Vector2.Zero ? dashDirection.Normalized() : LastDirection;
		_dashVelocity = heading * speed;
		_dashTimeLeft = duration;
		EmitSignal(SignalName.DashStarted);
	}

	/// <summary>Ends a running dash at once; normal movement resumes next step.</summary>
	public void CancelDash()
	{
		if (_dashTimeLeft <= 0.0)
		{
			return;
		}

		_dashTimeLeft = 0.0;
		EmitSignal(SignalName.DashFinished);
	}

	public void Step(double delta)
	{
		if (!IsInstanceValid(_body) || !IsInstanceValid(_entity))
		{
			return;
		}

		if (_dashTimeLeft > 0.0)
		{
			_dashTimeLeft -= delta;
			_body!.Velocity = _dashVelocity;
			if (_dashTimeLeft <= 0.0)
			{
				EmitSignal(SignalName.DashFinished);
			}
		}
		else
		{
			_body!.Velocity = _direction * _entity!.Attributes[Stats.MoveSpeed].DisplayValue;
		}

		_body.MoveAndSlide();
	}

	public override void _PhysicsProcess(double delta) => Step(delta);
}
