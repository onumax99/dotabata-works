using UnityEngine;

namespace DotaBata
{
    /// <summary>材料棚。手が空いていれば、何度でもパーツを取り出せる。</summary>
    public class PartsShelf : Interactable
    {
        public ProductType productType;

        public override ActionKind GetAction(PlayerController player)
        {
            return player.Held == null ? ActionKind.Take : ActionKind.None;
        }

        public override void OnActionDown(PlayerController player)
        {
            if (player.Held != null || StageBuilder.I == null) return;
            Item item = StageBuilder.I.SpawnItem(productType, ItemStage.Part, PaintColor.None);
            player.Pickup(item);
        }
    }
}
