using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Mannequins {
  public class EntityMannequin : EntityHumanoid {
    protected static readonly string InventoryAttributeKey = "inventory";

    protected InventoryGeneric inv;
    protected float fireDamage = 0f;

    public override bool StoreWithChunk => true;
    public override IInventory GearInventory => inv;
    public override ItemSlot RightHandItemSlot => inv[15];
    public override ItemSlot LeftHandItemSlot => inv[16];
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
      IPlayer player = (byEntity as EntityPlayer)?.Player;
      if (player == null) {
        return;
      }

      if (!byEntity.World.Claims.TryAccess(player, Pos.AsBlockPos, EnumBlockAccessFlags.Use)) {
        player.InventoryManager.ActiveHotbarSlot.MarkDirty();
        WatchedAttributes.MarkAllDirty();
        return;
      }

      if (mode != EnumInteractMode.Interact || byEntity.RightHandItemSlot == null) {
        return;
      }

      if (byEntity.RightHandItemSlot.Empty) {
        OnTakeClothes(byEntity, slot, hitPosition);
      }
      else {
        OnPlaceClothes(byEntity, slot, hitPosition);
      }
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

    public override bool ReceiveDamage(DamageSource damageSource, float damage) {
      if (damageSource.Source == EnumDamageSource.Internal && damageSource.Type == EnumDamageType.Fire) {
        fireDamage += damage;
      }
      if (fireDamage > 4f) {
        Die();
      }
      return base.ReceiveDamage(damageSource, damage);
    }
  }
}