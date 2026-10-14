using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;

namespace VampireSurvivor.Defs;

/// <summary>One animation stored as a horizontal strip of equally sized frames.</summary>
public partial class AnimationStrip : Resource
{
	[Export]
	public StringName Animation { get; set; } = AnimNames.Idle;

	[Export]
	public Texture2D? Texture { get; set; }

	[Export(PropertyHint.Range, "1,64")]
	public int FrameCount { get; set; } = 1;

	[Export]
	public float Fps { get; set; } = 10f;

	[Export]
	public bool Loop { get; set; } = true;

	public List<string> Validate()
	{
		var errors = new List<string>();
		if (Texture is null)
		{
			errors.Add($"AnimationStrip '{Animation}': texture is null");
		}
		else if (FrameCount < 1)
		{
			errors.Add($"AnimationStrip '{Animation}': frame_count must be >= 1");
		}
		else if (Texture.GetWidth() % FrameCount != 0)
		{
			errors.Add($"AnimationStrip '{Animation}': width {Texture.GetWidth()} is not divisible by frame_count {FrameCount}");
		}

		if (Fps <= 0f)
		{
			errors.Add($"AnimationStrip '{Animation}': fps must be > 0");
		}

		return errors;
	}
}
