using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Mannequins.Client {
  public class InventoryDialog : GuiDialog {
    protected InventoryGeneric inv;

    protected EntityMannequin owningEntity;

    protected Vec3d entityPos = new Vec3d();

    public override string DebugName => "mannequin-inventory-dialog";

    public override double DrawOrder => 0.2;

    public override bool UnregisterOnClose => true;

    public override bool PrefersUngrabbedMouse => false;

    public override bool DisableMouseGrab => false;

    public override string ToggleKeyCombinationCode => null;

    public InventoryDialog(InventoryGeneric inv, EntityMannequin entityMannequin, ICoreClientAPI capi) : base(capi) {
      this.inv = inv;
      this.owningEntity = entityMannequin;

      ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
      bgBounds.BothSizing = ElementSizing.FitToChildren;

      ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.LeftMiddle).WithFixedAlignmentOffset(GuiStyle.DialogToScreenPadding, 0.0);

      double pad = GuiElementItemSlotGridBase.unscaledSlotPadding;
      ElementBounds leftSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 20.0 + pad, 1, 6).FixedGrow(0.0, pad);
      ElementBounds leftArmorSlotBoundsHead = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 20.0 + pad, 1, 1).FixedGrow(0.0, pad);
      ElementBounds leftArmorSlotBoundsBody = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 20.0 + pad + 102.0, 1, 1).FixedGrow(0.0, pad);
      ElementBounds leftArmorSlotBoundsLegs = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 20.0 + pad + 204.0, 1, 1).FixedGrow(0.0, pad);
      leftSlotBounds.FixedRightOf(leftArmorSlotBoundsHead, 10.0).FixedRightOf(leftArmorSlotBoundsBody, 10.0).FixedRightOf(leftArmorSlotBoundsLegs, 10.0);
      ElementBounds rightSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0.0, 20.0 + pad, 1, 6).FixedGrow(0.0, pad);
      rightSlotBounds.FixedRightOf(leftSlotBounds, 10.0);
      leftSlotBounds.fixedHeight -= 6.0;
      rightSlotBounds.fixedHeight -= 6.0;

      SingleComposer = capi.Gui.CreateCompo("mannequincontents" + owningEntity.EntityId, dialogBounds).AddShadedDialogBG(bgBounds).AddDialogTitleBar(Lang.Get("mannequins:mannequin-contents"), OnClose: OnTitleBarClose);
      SingleComposer.BeginChildElements(bgBounds)
        .AddItemSlotGrid(inv, SendInvPacket, 1, new int[1] { 12 }, leftArmorSlotBoundsHead, "amorSlotsHead")
        .AddItemSlotGrid(inv, SendInvPacket, 1, new int[1] { 13 }, leftArmorSlotBoundsBody, "armorSlotsBody")
        .AddItemSlotGrid(inv, SendInvPacket, 1, new int[1] { 14 }, leftArmorSlotBoundsLegs, "armorSlotsLegs")
        .AddItemSlotGrid(inv, SendInvPacket, 1, new int[6] { 0, 1, 2, 11, 3, 4 }, leftSlotBounds, "leftSlots")
        .AddItemSlotGrid(inv, SendInvPacket, 1, new int[6] { 6, 7, 8, 10, 5, 9 }, rightSlotBounds, "rightSlots")
        .EndChildElements();

      SingleComposer.Compose();
    }

    public override void OnGuiClosed() {
      base.OnGuiClosed();
      capi.Network.SendPacketClient(capi.World.Player.InventoryManager.CloseInventory(inv));
      SingleComposer.GetSlotGrid("armorSlotsHead")?.OnGuiClosed(capi);
      SingleComposer.GetSlotGrid("armorSlotsBody")?.OnGuiClosed(capi);
      SingleComposer.GetSlotGrid("armorSlotsLegs")?.OnGuiClosed(capi);
      SingleComposer.GetSlotGrid("leftSlots")?.OnGuiClosed(capi);
      SingleComposer.GetSlotGrid("rightSlots")?.OnGuiClosed(capi);
    }

    protected void OnTitleBarClose() {
      TryClose();
    }

    protected void SendInvPacket(object packet) {
      capi.Network.SendPacketClient(packet);
    }
  }
}
