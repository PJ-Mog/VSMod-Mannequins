using System.IO;
using Mannequins.Client;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Mannequins {
  public class EntityMannequin : EntityHumanoid {
    protected static readonly int OpenInventoryPacketId = 1000;
    protected static readonly int CloseInventoryPacketId = 1001;
    protected static readonly string InventoryAttributeKey = "inventory";
    protected InventoryGeneric inv;
    protected float fireDamage = 0f;

    public override bool StoreWithChunk => true;
    public override IInventory GearInventory => inv;
    public override ItemSlot RightHandItemSlot => inv[15];
    public override ItemSlot LeftHandItemSlot => inv[16];
    protected InventoryDialog InventoryDialog { get; set; }
    protected virtual string InventoryId => "mannequingear-" + EntityId;

    public override void Initialize(EntityProperties properties, ICoreAPI api, long inChunkIndex3d) {
      base.Initialize(properties, api, inChunkIndex3d);
      InitializeInventory(api);
      if (api.Side == EnumAppSide.Client) {
        WatchedAttributes.RegisterModifiedListener(InventoryAttributeKey, ReadInventoryFromAttributes);
      }
      ReadInventoryFromAttributes();
    }

    protected virtual void InitializeInventory(ICoreAPI api) {
      inv = new InventoryGeneric(17, InventoryId, Api, onNewSlot: OnNewSlot);
      GearInventory.SlotModified += OnSlotModified;
    }

    protected virtual void ReadInventoryFromAttributes() {
      ITreeAttribute tree = WatchedAttributes[InventoryAttributeKey] as ITreeAttribute;
      if (inv != null && tree != null) {
        inv.FromTreeAttributes(tree);
      }
      (base.Properties.Client.Renderer as EntityShapeRenderer)?.MarkShapeModified();
    }

    protected virtual void OnSlotModified(int slotId) {
      var tree = WatchedAttributes.GetOrAddTreeAttribute(InventoryAttributeKey);
      inv.ToTreeAttributes(tree);
      WatchedAttributes.MarkPathDirty(InventoryAttributeKey);
    }

    protected virtual ItemSlot OnNewSlot(int slotId, InventoryGeneric self) {
      if (slotId >= 15) {
        return new ItemSlotSurvival(self);
      }
      return new ItemSlotCharacter((EnumCharacterDressType)slotId, self);
    }

    public override string GetName() {
      string key = Code.Domain + ":item-";
      if (!Alive) {
        key += "broken-";
      }
      key += Code.Path;
      return Lang.GetMatching(key);
    }

    public override void FromBytes(BinaryReader reader, bool forClient) {
      base.FromBytes(reader, forClient);
      var tree = WatchedAttributes.GetTreeAttribute(InventoryAttributeKey);
      if (tree == null) {
        return;
      }

      if (inv == null) {
        InitializeInventory(Api);
      }
      inv.FromTreeAttributes(tree);
    }

    public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode) {
      if (mode == EnumInteractMode.Attack) {
        return;
      }

      IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
      if (byPlayer == null) {
        return;
      }

      if (byEntity.Controls.Sneak && byPlayer.InventoryManager.ActiveHotbarSlot.Empty && TryPickUp(byPlayer)) {
        return;
      }

      ToggleInventoryDialog(byPlayer);
    }

    public override void OnReceivedClientPacket(IServerPlayer player, int packetid, byte[] data) {
      base.OnReceivedClientPacket(player, packetid, data);

      if (packetid == OpenInventoryPacketId) {
        player.InventoryManager.OpenInventory(GearInventory);
      }
      if (packetid == CloseInventoryPacketId) {
        player.InventoryManager.CloseInventory(GearInventory);
      }
    }

    public override void OnReceivedServerPacket(int packetid, byte[] data) {
      if (packetid == CloseInventoryPacketId) {
        (World as IClientWorldAccessor).Player.InventoryManager.CloseInventory(GearInventory);
        if (InventoryDialog?.IsOpened() ?? false) {
          InventoryDialog?.TryClose();
        }
      }
    }

    public override void OnEntityDespawn(EntityDespawnData despawn) {
      base.OnEntityDespawn(despawn);

      InventoryDialog?.TryClose();

      switch (despawn.Reason) {
        case EnumDespawnReason.PickedUp:
          DropInventory();
          break;
      }
    }

    protected virtual bool TryPickUp(IPlayer byPlayer) {
      if (!byPlayer.Entity.World.Claims.TryAccess(byPlayer, Pos.AsBlockPos, EnumBlockAccessFlags.BuildOrBreak)) {
        byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
        WatchedAttributes.MarkAllDirty();
        return false;
      }

      for (int i = 0; i < GearInventory.Count; i++) {
        if (!GearInventory[i].Empty) {
          return false;
        }
      }

      ItemStack stack = GetAsItemStack();
      if (stack == null) {
        return false;
      }

      if (!byPlayer.Entity.TryGiveItemStack(stack)) {
        World.SpawnItemEntity(stack, Pos.XYZ);
      }
      Die(EnumDespawnReason.PickedUp);
      return true;
    }

    protected virtual ItemStack GetAsItemStack() {
      AssetLocation collectibleMannequinLocation = Code.Clone();
      CollectibleObject collectibleMannequin = World.GetItem(collectibleMannequinLocation) as CollectibleObject ?? World.GetBlock(collectibleMannequinLocation);
      if (collectibleMannequin == null) {
        World.Logger.Error("[Mannequins] Could not pick up mannequin ({0}). No such collectible: {1}", EntityId, collectibleMannequinLocation);
        return null;
      }
      return new ItemStack(collectibleMannequin);
    }

    protected virtual void ToggleInventoryDialog(IPlayer player) {
      if (InventoryDialog?.IsOpened() ?? false) {
        InventoryDialog?.TryClose();
      }
      else {
        TryOpenInventory(player);
      }
    }

    protected virtual void TryOpenInventory(IPlayer player) {
      if (!World.Claims.TryAccess(player, Pos.AsBlockPos, EnumBlockAccessFlags.Use)) {
        return;
      }

      if (Api is ICoreClientAPI capi && InventoryDialog == null) {
        InventoryDialog = new InventoryDialog(inv, this, capi);
        if (InventoryDialog.TryOpen()) {
          player.InventoryManager.OpenInventory(GearInventory);
          capi.Network.SendEntityPacket(EntityId, OpenInventoryPacketId);
        }
        InventoryDialog.OnClosed += OnInventoryDialogClosed;
      }
    }

    protected virtual void OnInventoryDialogClosed() {
      var capi = Api as ICoreClientAPI;
      capi.World.Player.InventoryManager.CloseInventory(GearInventory);
      capi.Network.SendEntityPacket(EntityId, CloseInventoryPacketId);
      InventoryDialog.Dispose();
      InventoryDialog = null;
    }

    public virtual void OnTakeClothes(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition) {
      foreach (var mannequinSlot in GearInventory) {
        if (!mannequinSlot.Empty && mannequinSlot.TryPutInto(byEntity.World, byEntity.RightHandItemSlot) > 0) {
          return;
        }
      }
    }

    public virtual void OnPlaceClothes(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition) {
      var sinkSlot = GearInventory.GetBestSuitedSlot(byEntity.RightHandItemSlot);
      if (sinkSlot.slot == null || sinkSlot.weight <= 0) {
        return;
      }

      byEntity.RightHandItemSlot.TryPutInto(byEntity.World, sinkSlot.slot);
    }

    protected virtual void DropInventory() {
      foreach (var slot in GearInventory) {
        if (slot.Empty) {
          continue;
        }

        World.SpawnItemEntity(slot.TakeOutWhole(), SidedPos.XYZ);
      }
    }

    public override bool ReceiveDamage(DamageSource damageSource, float damage) {
      if (damageSource.Source == EnumDamageSource.Internal && damageSource.Type == EnumDamageType.Fire) {
        fireDamage += damage;
      }
      if (fireDamage > 4f) {
        Die(EnumDespawnReason.Combusted);
      }
      return base.ReceiveDamage(damageSource, damage);
    }
  }
}
