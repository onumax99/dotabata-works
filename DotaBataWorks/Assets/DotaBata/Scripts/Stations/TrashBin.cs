using UnityEngine;

namespace DotaBata
{
    /// <summary>ゴミ箱。まちがえて作ったグッズを捨てる。</summary>
    public class TrashBin : Interactable
    {
        public override ActionKind GetAction(PlayerController player)
        {
            return player.Held != null ? ActionKind.Trash : ActionKind.None;
        }

        public override void OnActionDown(PlayerController player)
        {
            Item item = player.Release();
            if (item == null) return;
            Destroy(item.gameObject);
            Sfx.Drop();
            if (GameManager.I != null) GameManager.I.ShowPopup(LabelPosition, "ポイッ", new Color32(0xB8, 0xBC, 0xC8, 0xFF));
        }
    }
}
