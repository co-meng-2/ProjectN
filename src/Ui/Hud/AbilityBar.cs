using System.Collections.Generic;
using Godot;
using VampireSurvivor.Abilities;
using VampireSurvivor.Core;

namespace VampireSurvivor.Ui;

/// <summary>
/// The row of ability slots. A view: it reads the loadout, the wallet and the grade service through their events and only
/// emits requests; it never changes them.
/// </summary>
public partial class AbilityBar : HBoxContainer
{
	public const string SlotScene = "res://src/Ui/Hud/ability_slot_view.tscn";

	private readonly List<AbilitySlotView> _views = [];
	private Loadout? _loadout;
	private Wallet? _wallet;
	private GradeService? _service;

	[Signal]
	public delegate void PromoteRequestedEventHandler(int index);

	[Signal]
	public delegate void RerollRequestedEventHandler(int index);

	public void Bind(Loadout loadout, Wallet wallet, GradeService service)
	{
		Unbind();
		_loadout = loadout;
		_wallet = wallet;
		_service = service;
		var scene = GD.Load<PackedScene>(SlotScene);
		for (int index = 0; index < loadout.Size; index++)
		{
			var view = scene.Instantiate<AbilitySlotView>();
			view.SlotIndex = index;
			AddChild(view);
			view.PromoteRequested += OnPromoteRequested;
			view.RerollRequested += OnRerollRequested;
			_views.Add(view);
		}

		loadout.SlotChanged += OnSlotChanged;
		wallet.Changed += OnWalletChanged;
		service.Resolved += OnResolved;
		RefreshAll();
	}

	public AbilitySlotView? SlotView(int index) => index >= 0 && index < _views.Count ? _views[index] : null;

	public void RefreshAll()
	{
		for (int index = 0; index < _views.Count; index++)
		{
			Refresh(index);
		}
	}

	/// <summary>The models are plain C# objects that can outlive this node.</summary>
	public override void _ExitTree() => StopListening();

	private void Unbind()
	{
		StopListening();
		foreach (AbilitySlotView view in _views)
		{
			RemoveChild(view);
			view.QueueFree();
		}

		_views.Clear();
	}

	private void StopListening()
	{
		if (_loadout is not null)
		{
			_loadout.SlotChanged -= OnSlotChanged;
		}

		if (_wallet is not null)
		{
			_wallet.Changed -= OnWalletChanged;
		}

		if (_service is not null)
		{
			_service.Resolved -= OnResolved;
		}

		_loadout = null;
		_wallet = null;
		_service = null;
	}

	private void Refresh(int index)
	{
		if (_loadout is null || _wallet is null || _service is null)
		{
			return;
		}

		Price? price = _service.PromoteCost(index);
		bool affordable = price is { } p && p.IsAffordable(_wallet);
		_views[index].ShowSlot(_loadout.Slot(index), price, affordable, _service.RerollCost(index));
	}

	private void OnPromoteRequested(int index) => EmitSignal(SignalName.PromoteRequested, index);

	private void OnRerollRequested(int index) => EmitSignal(SignalName.RerollRequested, index);

	private void OnSlotChanged(int index, AbilitySlot slot)
	{
		if (index >= 0 && index < _views.Count)
		{
			Refresh(index);
		}
	}

	private void OnWalletChanged(int gold, int stone, int tickets) => RefreshAll();

	private void OnResolved(int index, GradeResult result)
	{
		if (result.Outcome != GradeOutcome.Ok)
		{
			SlotView(index)?.Flash();
		}
	}
}
