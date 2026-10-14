using Godot;

namespace VampireSurvivor.Meta.SkillTree;

/// <summary>One skill-tree row: name, detail line and a Buy button. It only reports the click.</summary>
public partial class SkillNodeRow : HBoxContainer
{
	private int _index = -1;

	[Signal]
	public delegate void BuyPressedEventHandler(int index);

	[Export]
	public Label NameLabel { get; set; } = null!;

	[Export]
	public Label DetailLabel { get; set; } = null!;

	[Export]
	public Button BuyButton { get; set; } = null!;

	public override void _Ready() => BuyButton.Pressed += () => EmitSignal(SignalName.BuyPressed, _index);

	/// <summary>Call after the row is in the tree.</summary>
	public void Setup(int index, string title, string tooltip, string detail, string buyText, bool buyEnabled)
	{
		_index = index;
		NameLabel.Text = title;
		NameLabel.TooltipText = tooltip;
		DetailLabel.Text = detail;
		BuyButton.Text = buyText;
		BuyButton.Disabled = !buyEnabled;
	}
}
