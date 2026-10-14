using Godot;

namespace VampireSurvivor.Meta.Hub;

/// <summary>One stage row in the hub: name, status and a Play button. It only reports the click.</summary>
public partial class StageCard : HBoxContainer
{
	private int _index = -1;

	[Signal]
	public delegate void PlayPressedEventHandler(int index);

	[Export]
	public Label NameLabel { get; set; } = null!;

	[Export]
	public Label StatusLabel { get; set; } = null!;

	[Export]
	public Button PlayButton { get; set; } = null!;

	public override void _Ready() => PlayButton.Pressed += () => EmitSignal(SignalName.PlayPressed, _index);

	/// <summary>Call after the card is in the tree.</summary>
	public void Setup(int index, string stageName, StageStatus status)
	{
		_index = index;
		NameLabel.Text = stageName;
		StatusLabel.Text = status switch
		{
			StageStatus.Locked => "Locked",
			StageStatus.Cleared => "Cleared",
			_ => string.Empty,
		};
		PlayButton.Disabled = status == StageStatus.Locked;
	}
}
