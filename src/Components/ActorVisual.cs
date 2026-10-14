using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Components;

/// <summary>Plays an actor's animations from a <see cref="VisualDef"/> (whose SpriteFrames are shared between actors).</summary>
public partial class ActorVisual : AnimatedSprite2D
{
	private const float HitFlashSeconds = 0.15f;
	private static readonly Color HitFlashColor = new(1f, 0.45f, 0.45f);

	private Color _baseTint = Colors.White;
	private bool _moving;
	private bool _oneShotActive;
	private bool _dead;

	[Signal]
	public delegate void OneShotFinishedEventHandler(StringName animationName);

	public override void _Ready() => AnimationFinished += OnAnimationFinished;

	public void Apply(VisualDef def, float scaleFactor = 1f, Color? tint = null)
	{
		SpriteFrames = def.SpriteFrames();
		Position = def.Offset;
		Scale = Vector2.One * scaleFactor;
		_baseTint = tint ?? Colors.White;
		Modulate = _baseTint;
		_moving = false;
		_oneShotActive = false;
		_dead = false;
		PlayLocomotion();
	}

	public void SetMoving(bool moving)
	{
		_moving = moving;
		if (!_oneShotActive)
		{
			PlayLocomotion();
		}
	}

	public void Face(float directionX)
	{
		if (Mathf.Abs(directionX) > 0.01f)
		{
			FlipH = directionX < 0f;
		}
	}

	/// <summary>Plays a non-looping animation, then returns to idle/walk (death stays on its last frame).</summary>
	public void PlayOnce(StringName animationName)
	{
		if (_dead || SpriteFrames is null || !SpriteFrames.HasAnimation(animationName))
		{
			return;
		}

		_dead = animationName == AnimNames.Death;
		_oneShotActive = true;
		Play(animationName);
	}

	public void FlashHit()
	{
		if (_dead)
		{
			return;
		}

		Modulate = HitFlashColor;
		CreateTween().TweenProperty(this, "modulate", _baseTint, HitFlashSeconds);
	}

	private void PlayLocomotion()
	{
		if (SpriteFrames is null)
		{
			return;
		}

		StringName wanted = _moving ? AnimNames.Walk : AnimNames.Idle;
		if (!SpriteFrames.HasAnimation(wanted))
		{
			wanted = AnimNames.Idle;
		}

		if (Animation != wanted || !IsPlaying())
		{
			Play(wanted);
		}
	}

	private void OnAnimationFinished()
	{
		StringName finished = Animation;
		EmitSignal(SignalName.OneShotFinished, finished);
		if (finished == AnimNames.Death)
		{
			return;
		}

		_oneShotActive = false;
		PlayLocomotion();
	}
}
