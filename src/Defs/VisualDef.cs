using System.Collections.Generic;
using Godot;
using VampireSurvivor.Core;

namespace VampireSurvivor.Defs;

/// <summary>The set of animations an actor can play. <see cref="SpriteFrames"/> builds and memoises the frames.</summary>
[GlobalClass]
public partial class VisualDef : Resource
{
	private SpriteFrames? _frames;

	[Export]
	public Godot.Collections.Array<AnimationStrip> Strips { get; set; } = [];

	[Export]
	public Vector2 Offset { get; set; } = Vector2.Zero;

	public AnimationStrip? FindStrip(StringName animation)
	{
		foreach (AnimationStrip? strip in Strips)
		{
			if (strip is not null && strip.Animation == animation)
			{
				return strip;
			}
		}

		return null;
	}

	/// <summary>Built on first use and shared by every actor that plays this def. Call <see cref="Validate"/> first.</summary>
	public SpriteFrames SpriteFrames() => _frames ??= BuildFrames();

	public List<string> Validate()
	{
		var errors = new List<string>();
		if (FindStrip(AnimNames.Idle) is null)
		{
			errors.Add($"VisualDef: missing required '{AnimNames.Idle}' strip");
		}

		var seen = new HashSet<string>();
		var reported = new HashSet<string>();
		foreach (AnimationStrip? strip in Strips)
		{
			if (strip is null)
			{
				errors.Add("VisualDef: null strip");
				continue;
			}

			errors.AddRange(strip.Validate());
			string name = strip.Animation.ToString();
			if (seen.Contains(name) && reported.Add(name))
			{
				errors.Add($"VisualDef: duplicate strip for animation '{name}'");
			}

			seen.Add(name);
		}

		return errors;
	}

	private SpriteFrames BuildFrames()
	{
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");
		foreach (AnimationStrip strip in Strips)
		{
			frames.AddAnimation(strip.Animation);
			frames.SetAnimationLoop(strip.Animation, strip.Loop);
			frames.SetAnimationSpeed(strip.Animation, strip.Fps);
			float frameWidth = strip.Texture!.GetWidth() / (float)strip.FrameCount;
			float frameHeight = strip.Texture.GetHeight();
			for (int i = 0; i < strip.FrameCount; i++)
			{
				var frame = new AtlasTexture
				{
					Atlas = strip.Texture,
					Region = new Rect2(i * frameWidth, 0f, frameWidth, frameHeight),
				};
				frames.AddFrame(strip.Animation, frame);
			}
		}

		return frames;
	}
}
