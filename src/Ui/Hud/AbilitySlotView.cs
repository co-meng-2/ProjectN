using Godot;
using VampireSurvivor.Abilities;
using VampireSurvivor.Core;

namespace VampireSurvivor.Ui;

/// <summary>
/// One HUD slot: the ability's icon inside a grade-coloured frame and the cost of its next promotion. A view only: clicks
/// become request signals; the app decides what happens.
/// </summary>
public partial class AbilitySlotView : Control
{
	/// <summary>Label suffix per <see cref="CurrencyKind"/> (Gold, Stone, Ticket).</summary>
	private static readonly string[] KindSuffixes = ["g", "s", "T"];
	private static readonly Color EmptyFrameColor = new(0.3f, 0.3f, 0.33f);
	private static readonly Color BackgroundColor = new(0.08f, 0.08f, 0.1f, 0.85f);
	private static readonly Color RevealedTint = new(1f, 0.95f, 0.55f);
	private static readonly Color FlashColor = new(1f, 0.35f, 0.35f);
	private const float FrameWidth = 3f;
	private const float DimAlpha = 0.35f;
	private const float FlashSeconds = 0.25f;

	private Color _frameColor = EmptyFrameColor;

	[Signal]
	public delegate void PromoteRequestedEventHandler(int index);

	[Signal]
	public delegate void RerollRequestedEventHandler(int index);

	[Export]
	public TextureRect Icon { get; set; } = null!;

	[Export]
	public Label Cost { get; set; } = null!;

	public int SlotIndex { get; set; }

	public bool IsDimmed { get; private set; }

	public bool IsFlashing => Modulate != Colors.White;

	public static string CostText(Price? price) =>
		price is { } p ? $"{p.Amount}{KindSuffixes[(int)p.Kind]}" : "MAX";

	/// <summary>
	/// <paramref name="price"/> is the next promotion's price (null = nothing left to promote); <paramref name="affordable"/>
	/// whether the wallet covers it; <paramref name="rerollPrice"/> only feeds the tooltip.
	/// </summary>
	public void ShowSlot(AbilitySlot? slot, Price? price, bool affordable, Price? rerollPrice = null)
	{
		bool filled = slot is { IsEmpty: false };
		_frameColor = filled ? slot!.Grade.Tint() : EmptyFrameColor;
		Icon.Texture = filled ? slot!.Def!.IconFor(slot.Grade) : null;
		bool tintRevealed = filled && slot!.Grade == Grade.Revealed && slot.Def!.RevealedIcon is null;
		Icon.Modulate = tintRevealed ? RevealedTint : Colors.White;
		IsDimmed = price is not null && !affordable;
		var alpha = new Color(1f, 1f, 1f, IsDimmed ? DimAlpha : 1f);
		Icon.SelfModulate = alpha;
		Cost.Modulate = alpha; // visible even on an empty slot
		Cost.Text = CostText(price);
		TooltipText = Tooltip(slot, price, rerollPrice);
		QueueRedraw();
	}

	/// <summary>Brief red tint for a refused action.</summary>
	public void Flash()
	{
		Modulate = FlashColor;
		CreateTween().TweenProperty(this, "modulate", Colors.White, FlashSeconds);
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
		{
			return;
		}

		EmitSignal(click.CtrlPressed ? SignalName.RerollRequested : SignalName.PromoteRequested, SlotIndex);
		AcceptEvent();
	}

	public override void _Draw()
	{
		var rect = new Rect2(Vector2.Zero, Size);
		DrawRect(rect, BackgroundColor);
		Color frame = _frameColor;
		if (IsDimmed)
		{
			frame.A = DimAlpha;
		}

		DrawRect(rect, frame, false, FrameWidth);
	}

	private static string Tooltip(AbilitySlot? slot, Price? price, Price? rerollPrice)
	{
		if (slot is null || slot.IsEmpty)
		{
			return $"Empty slot\nClick: buy a Common ability ({CostText(price)})";
		}

		string text = $"{slot.Def!.DisplayName} ({slot.Grade.DisplayName()})";
		text += price is null ? "\nFully revealed" : $"\nClick: promote ({CostText(price)})";
		if (rerollPrice is not null)
		{
			text += $"\nCtrl+click: reroll ({CostText(rerollPrice)})";
		}

		return text;
	}
}
