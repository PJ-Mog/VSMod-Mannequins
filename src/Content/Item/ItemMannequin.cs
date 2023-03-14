using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Mannequins {
  public class ItemMannequin : Item {
    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling) {
      base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);

      if (blockSel == null) {
        return;
      }

      var byPlayer = (byEntity as EntityPlayer)?.Player;
      if (!api.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak)) {
        slot.MarkDirty();
        return;
      }

      if (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative) {
        slot.TakeOut(1);
        slot.MarkDirty();
      }

      EntityProperties type = api.World.GetEntityType(Code);
      Entity entity = api.World.ClassRegistry.CreateEntity(type);
      api.Logger.Debug("[Mannequins] entity {0}", entity);
      if (entity == null) {
        return;
      }

      entity.ServerPos.X = (float)(blockSel.Position.X + ((!blockSel.DidOffset) ? blockSel.Face.Normali.X : 0)) + 0.5f;
      entity.ServerPos.Y = blockSel.Position.Y + ((!blockSel.DidOffset) ? blockSel.Face.Normali.Y : 0);
      entity.ServerPos.Z = (float)(blockSel.Position.Z + ((!blockSel.DidOffset) ? blockSel.Face.Normali.Z : 0)) + 0.5f;
      entity.ServerPos.Yaw = byEntity.SidedPos.Yaw - GameMath.PIHALF;
      if (byPlayer != null && byPlayer.PlayerUID != null) {
        entity.WatchedAttributes.SetString("ownerUid", byPlayer.PlayerUID);
      }
      entity.Pos.SetFrom(entity.ServerPos);
      byEntity.World.PlaySoundAt(new AssetLocation("game:sounds/block/torch"), entity, byPlayer);
      byEntity.World.SpawnEntity(entity);
      handling = EnumHandHandling.PreventDefaultAction;
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot) {
      return new WorldInteraction[1] {
        new WorldInteraction {
          ActionLangCode = "heldhelp-place",
          MouseButton = EnumMouseButton.Right
        }
      }.Append(base.GetHeldInteractionHelp(inSlot));
    }
  }
}
