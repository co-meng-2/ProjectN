using System.Collections.Generic;
using System.Linq;
using Godot;
using VampireSurvivor.App;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Meta.SkillTree;

/// <summary>
/// The skill-tree screen: gem balance, one row per node, Back. It owns no state: purchases go through
/// SkillTreeProgress, then the save is written once and the rows are rebuilt.
/// </summary>
public partial class SkillTreeScreen : Control
{
	public const string RowScene = "res://port/src/Meta/SkillTree/skill_node_row.tscn";
	public const string DefaultTree = "res://port/data/skill_tree/skill_tree.tres";

	private SkillTreeProgress? _progress;

	[Export]
	public Label GemLabel { get; set; } = null!;

	[Export]
	public VBoxContainer NodeList { get; set; } = null!;

	[Export]
	public Label EmptyLabel { get; set; } = null!;

	[Export]
	public Button BackButton { get; set; } = null!;

	/// <summary>Default to the shipped tree and the autoloads in _Ready; tests set them first.</summary>
	public SkillTreeDef? Tree { get; set; }

	public SaveService? SaveService { get; set; }

	public SceneRouter? Router { get; set; }

	public override void _Ready()
	{
		Tree ??= GD.Load<SkillTreeDef>(DefaultTree);
		SaveService ??= Autoloads.Save(this);
		Router ??= Autoloads.Router(this);
		BackButton.Pressed += () => Router.Go(SceneRouter.Route.Hub);
		List<string> errors = Tree.Validate();
		if (errors.Count > 0)
		{
			GD.PushError($"SkillTreeScreen: unusable skill tree: {string.Join(", ", errors)}");
			EmptyLabel.Visible = true;
			GemLabel.Text = $"Gem {SaveService.Data.Gem}";
			BackButton.GrabFocus();
			return;
		}

		_progress = new SkillTreeProgress(Tree, SaveService.Data);
		Rebuild();
	}

	/// <summary>Public so tests can prove the handler refuses what the disabled button already refuses.</summary>
	public void OnBuyPressed(int index)
	{
		if (_progress is null || !_progress.Buy(index))
		{
			return;
		}

		SaveService!.Save();
		Rebuild(index);
	}

	/// <summary>
	/// <paramref name="preferred"/> is the row to keep focused (the one just bought) while it can still be bought, so
	/// repeated Enter levels the same node instead of jumping to another one.
	/// </summary>
	private void Rebuild(int preferred = -1)
	{
		foreach (Node child in NodeList.GetChildren())
		{
			NodeList.RemoveChild(child);
			child.QueueFree();
		}

		GemLabel.Text = $"Gem {SaveService!.Data.Gem}";
		var rowScene = GD.Load<PackedScene>(RowScene);
		SkillNodeRow? focusRow = null;
		for (int index = 0; index < Tree!.Nodes.Count; index++)
		{
			SkillNodeDef node = Tree.Nodes[index];
			var row = rowScene.Instantiate<SkillNodeRow>();
			NodeList.AddChild(row);
			bool canBuy = _progress!.CanBuy(index);
			row.Setup(index, node.DisplayName, node.Description, DetailText(index), BuyText(index), canBuy);
			row.BuyPressed += OnBuyPressed;
			if (canBuy && (focusRow is null || index == preferred))
			{
				focusRow = row;
			}
		}

		if (focusRow is not null)
		{
			focusRow.BuyButton.GrabFocus();
		}
		else
		{
			BackButton.GrabFocus();
		}
	}

	private string DetailText(int index)
	{
		SkillNodeDef node = Tree!.Nodes[index];
		return _progress!.Status(index) switch
		{
			SkillStatus.Locked => "Requires: " + string.Join(", ", node.Requires.Select(id => Tree.NodeById(id)?.DisplayName ?? id.ToString())),
			SkillStatus.Maxed => $"Lv {node.MaxLevel}/{node.MaxLevel}   Max",
			_ => $"Lv {_progress.Level(index)}/{node.MaxLevel}   Cost {_progress.NextCost(index)}",
		};
	}

	private string BuyText(int index) => _progress!.Status(index) == SkillStatus.Maxed ? "Max" : "Buy";
}
