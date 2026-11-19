using UnityEngine;

namespace DotaBata
{
    /// <summary>
    /// 動作確認用の自動プレイ。GuideSystem が勧める「次にやること」へ歩いて、アクションを押すだけ。
    /// 人の代わりにステージを最後まで回して、作る → 塗る → 箱づめ → 出荷 の流れが通るかを確かめる。
    /// ふだんは止まっている（DotaBataDebug.StartAuto で動かす）。
    /// </summary>
    public class DebugAutoPlayer : MonoBehaviour
    {
        public static bool Enabled;

        float pressTimer;
        float stuckTimer;
        Vector3 lastPosition;
        float sidestepTimer;
        float sidestepSign = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Enabled = false; }

        void Update()
        {
            GameInput input = GameInput.I;
            GameManager game = GameManager.I;
            if (input == null) return;
            if (!Enabled || game == null || !game.IsPlaying || game.Player == null)
            {
                input.SetBot(Vector2.zero, false);
                return;
            }

            PlayerController player = game.Player;
            Interactable target = GuideSystem.Suggest(player);
            if (target == null)
            {
                input.SetBot(Vector2.zero, false);
                return;
            }

            float dt = Time.deltaTime;
            Vector3 offset = target.GetPoint(player) - player.transform.position;
            offset.y = 0f;
            bool focused = player.Focused == target;

            Vector2 move = Vector2.zero;
            if (!focused)
            {
                Vector3 direction = offset.sqrMagnitude > 0.0004f ? offset.normalized : player.transform.forward;

                // 何かに引っかかって進めないときは、少し横へよける
                float moved = (player.transform.position - lastPosition).magnitude;
                stuckTimer = moved < 1.2f * dt ? stuckTimer + dt : 0f;
                if (stuckTimer > 0.5f)
                {
                    stuckTimer = 0f;
                    sidestepTimer = 0.45f;
                    sidestepSign = -sidestepSign;
                }
                if (sidestepTimer > 0f)
                {
                    sidestepTimer -= dt;
                    direction = (direction + Vector3.Cross(Vector3.up, direction) * (1.6f * sidestepSign)).normalized;
                }
                move = new Vector2(direction.x, direction.z);
            }
            else
            {
                stuckTimer = 0f;
            }
            lastPosition = player.transform.position;

            pressTimer -= dt;
            if (focused && pressTimer <= 0f && player.CurrentAction != ActionKind.Work)
            {
                input.PressAction();
                pressTimer = 0.25f;
            }
            input.SetBot(move, focused && player.CurrentAction == ActionKind.Work);
        }
    }
}
