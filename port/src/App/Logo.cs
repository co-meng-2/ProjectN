using Godot;

namespace VampireSurvivor.App;

/// <summary>Boot screen: shows the title briefly, then goes to the main menu. Any key press skips ahead.</summary>
public partial class Logo : Control
{
	public const double HoldSeconds = 1.5;

	private bool _done;

	/// <summary>Defaults to the SceneRouter autoload in _Ready; tests set it first.</summary>
	public SceneRouter? Router { get; set; }

	public override void _Ready()
	{
		Router ??= Autoloads.Router(this);
		GetTree().CreateTimer(HoldSeconds).Timeout += Proceed;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsPressed())
		{
			Proceed();
		}
	}

	private void Proceed()
	{
		if (_done || !IsInstanceValid(this))
		{
			return;
		}

		_done = true;
		Router!.Go(SceneRouter.Route.MainMenu);
	}
}
