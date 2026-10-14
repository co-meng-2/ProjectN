using Godot;
using VampireSurvivor.Core;
using VampireSurvivor.Defs;

namespace VampireSurvivor.Tests.Support;

/// <summary>Small, valid definitions for tests (the real ones are .tres files).</summary>
public static class DefFactory
{
	public static Texture2D Texture(int width = 60, int height = 10)
	{
		return ImageTexture.CreateFromImage(Image.CreateEmpty(width, height, false, Image.Format.Rgba8));
	}

	public static VisualDef Visual()
	{
		var strip = new AnimationStrip
		{
			Animation = AnimNames.Idle,
			Texture = Texture(60, 10),
			FrameCount = 6,
		};
		var def = new VisualDef();
		def.Strips.Add(strip);
		return def;
	}

	/// <summary>A curve with points at x = 1..n.</summary>
	public static Curve CurveOf(params float[] ys)
	{
		var curve = new Curve { MinDomain = 1f, MaxDomain = ys.Length, MaxValue = 1000f };
		for (int i = 0; i < ys.Length; i++)
		{
			curve.AddPoint(new Vector2(i + 1, ys[i]));
		}

		return curve;
	}

	public static PassiveDef Might() => new()
	{
		Stat = StatId.Attack,
		Operation = PassiveOperation.Percent,
		ByLevel = CurveOf(0.1f, 0.2f, 0.35f, 0.5f, 0.7f, 1.0f),
	};

	public static PassiveDef Vitality() => new()
	{
		Stat = StatId.MaxHealth,
		Operation = PassiveOperation.Flat,
		ByLevel = CurveOf(20f, 40f, 70f, 110f, 160f, 240f),
	};
}
