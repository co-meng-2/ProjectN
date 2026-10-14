using System.Collections.Generic;
using Godot;

namespace VampireSurvivor.Defs;

/// <summary>
/// One kind of stage event: its name, the controller scene that runs it, its time limit and its reward. The controller
/// root must be a StageEvent; defs cannot name stage classes, so that check is EventDirector.ValidateDefs (run by the
/// stage). Here: the root is a Node2D and passes its own <see cref="IValidatable.Validate"/>.
/// </summary>
[GlobalClass]
public partial class EventDef : Resource
{
	[Export]
	public StringName Id { get; set; } = string.Empty;

	[Export]
	public string DisplayName { get; set; } = string.Empty;

	/// <summary>Optional; the HUD shows the name when there is no icon.</summary>
	[Export]
	public Texture2D? Icon { get; set; }

	[Export]
	public PackedScene? ControllerScene { get; set; }

	[Export]
	public float TimeLimit { get; set; }

	[Export]
	public RewardDef? Reward { get; set; }

	public List<string> Validate()
	{
		var errors = new List<string>();
		string id = Id.ToString();
		if (id.Length == 0)
		{
			errors.Add("EventDef: id is empty");
		}

		if (DisplayName.Length == 0)
		{
			errors.Add($"EventDef '{id}': display_name is empty");
		}

		if (TimeLimit <= 0f)
		{
			errors.Add($"EventDef '{id}': time_limit must be > 0");
		}

		if (Reward is null)
		{
			errors.Add($"EventDef '{id}': reward is null");
		}
		else
		{
			foreach (string error in Reward.Validate())
			{
				errors.Add($"EventDef '{id}': {error}");
			}
		}

		if (ControllerScene is null)
		{
			errors.Add($"EventDef '{id}': controller_scene is null");
		}
		else
		{
			errors.AddRange(ValidateScene(id));
		}

		return errors;
	}

	private List<string> ValidateScene(string id)
	{
		var errors = new List<string>();
		Node root = ControllerScene!.Instantiate();
		if (root is not Node2D)
		{
			errors.Add($"EventDef '{id}': controller_scene root must be a Node2D (a StageEvent)");
		}
		else if (root is IValidatable validatable)
		{
			errors.AddRange(validatable.Validate());
		}

		root.Free();
		return errors;
	}
}
