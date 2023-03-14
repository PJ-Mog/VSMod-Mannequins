using Vintagestory.API.Common;

namespace Mannequins {
  public class MannequinsMod : ModSystem {
    public override void Start(ICoreAPI api) {
      base.Start(api);

      api.RegisterEntity("EntityMannequin", typeof(EntityMannequin));
    }
  }
}
