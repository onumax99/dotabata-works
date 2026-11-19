using System.Collections;
using UnityEngine;

namespace DotaBata
{
    /// <summary>出荷口。箱詰めしたグッズを届けると、注文と照らし合わせてスコアになる。</summary>
    public class ShippingGate : Interactable
    {
        [Tooltip("届けた箱が吸い込まれていく位置")]
        public Transform dropPoint;

        public override ActionKind GetAction(PlayerController player)
        {
            return player.Held != null && player.Held.stage == ItemStage.Boxed ? ActionKind.Ship : ActionKind.None;
        }

        public override void OnActionDown(PlayerController player)
        {
            if (GetAction(player) != ActionKind.Ship) return;
            Item item = player.Release();
            if (GameManager.I != null) GameManager.I.Deliver(item, LabelPosition);
            item.AttachTo(dropPoint != null ? dropPoint : transform);
            StartCoroutine(ShipRoutine(item));
        }

        IEnumerator ShipRoutine(Item item)
        {
            // 見た目だけ：奥へ流れながら小さくなって消える
            item.enabled = false;
            Transform t = item.transform;
            Vector3 start = t.localPosition;
            const float seconds = 0.3f;
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                if (t == null) yield break;
                float k = time / seconds;
                t.localPosition = start + new Vector3(0f, 0f, 0.7f * k);
                t.localScale = Vector3.one * (1f - 0.8f * k);
                yield return null;
            }
            if (item != null) Destroy(item.gameObject);
        }
    }
}
