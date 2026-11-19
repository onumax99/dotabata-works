using UnityEngine;

namespace DotaBata
{
    /// <summary>
    /// 操作キャラ。移動＋2ボタン（アクション・ダッシュ）だけで動かす。
    /// アクションは、近くにあるものに合わせて「とる・おく・つくる・出荷・すてる」に自動で切り替わる。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("移動")]
        public float moveSpeed = 4.6f;
        [Tooltip("向きを変える速さ（度/秒）")]
        public float turnSpeed = 1080f;

        [Header("ダッシュ")]
        public float dashSpeed = 11f;
        public float dashSeconds = 0.18f;
        public float dashCooldown = 0.45f;

        [Header("アクション")]
        [Tooltip("持ち場に手が届く距離（m）")]
        public float reach = 0.85f;
        public Transform holdAnchor;
        [Tooltip("走るときに弾ませる見た目のルート")]
        public Transform visual;
        [Tooltip("グッズを持つと頭の上に上げる手（任意）")]
        public Transform handLeft;
        public Transform handRight;

        public Item Held { get; private set; }
        public Interactable Focused { get; private set; }
        public ActionKind CurrentAction { get; private set; }
        public bool DashReady => dashCooldownTimer <= 0f;

        CharacterController controller;
        float dashTimer;
        float dashCooldownTimer;
        Vector3 dashDirection;
        float bobTime;
        Vector3 handLeftRest;
        Vector3 handRightRest;
        static readonly Vector3 HandLeftRaised = new Vector3(-0.3f, 1.5f, 0f);
        static readonly Vector3 HandRightRaised = new Vector3(0.3f, 1.5f, 0f);

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (handLeft != null) handLeftRest = handLeft.localPosition;
            if (handRight != null) handRightRest = handRight.localPosition;
        }

        public void Pickup(Item item)
        {
            if (item == null || Held != null) return;
            Held = item;
            item.AttachTo(holdAnchor);
            Sfx.Pick();
        }

        /// <summary>手に持っているものを手放して返す（置く先は呼び出し側が決める）。</summary>
        public Item Release()
        {
            Item item = Held;
            Held = null;
            return item;
        }

        void Update()
        {
            GameManager game = GameManager.I;
            GameInput input = GameInput.I;
            float dt = Time.deltaTime;

            if (game == null || input == null || !game.IsPlaying)
            {
                SetFocus(null);
                CurrentAction = ActionKind.None;
                Animate(0f, dt);
                return;
            }

            Vector2 move = input.Move;
            Vector3 direction = new Vector3(move.x, 0f, move.y);
            float amount = direction.magnitude;

            dashCooldownTimer -= dt;
            if (input.DashDown && dashCooldownTimer <= 0f)
            {
                dashTimer = dashSeconds;
                dashCooldownTimer = dashCooldown;
                dashDirection = amount > 0.1f ? direction.normalized : transform.forward;
                Sfx.Dash();
            }

            Vector3 velocity;
            if (dashTimer > 0f)
            {
                dashTimer -= dt;
                velocity = dashDirection * dashSpeed;
            }
            else
            {
                velocity = direction * (moveSpeed * game.SpeedMultiplier);
            }
            velocity.y = -3f; // 床に押しつけておく
            controller.Move(velocity * dt);

            if (amount > 0.1f)
            {
                Quaternion look = Quaternion.LookRotation(direction.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * dt);
            }

            UpdateFocus();

            if (input.ActionDown)
            {
                if (Focused != null) Focused.OnActionDown(this);
                else if (Held != null) DropHeld();
            }
            if (input.ActionHeld && Focused != null) Focused.OnActionHold(this, dt);

            Animate(dashTimer > 0f ? 1.5f : amount, dt);
        }

        void UpdateFocus()
        {
            Vector3 position = transform.position;
            position.y = 0f;
            Vector3 forward = transform.forward;

            Interactable best = null;
            ActionKind bestAction = ActionKind.None;
            float bestScore = float.MaxValue;

            var all = Interactable.All;
            for (int i = 0; i < all.Count; i++)
            {
                Interactable candidate = all[i];
                ActionKind action = candidate.GetAction(this);
                if (action == ActionKind.None) continue;

                Vector3 offset = candidate.GetPoint(this) - position;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance > reach) continue;

                // 近いものを優先しつつ、向いている方向のものを少しひいきする
                float facing = distance > 0.01f ? Vector3.Dot(forward, offset / distance) : 1f;
                float score = distance - 0.3f * facing;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                    bestAction = action;
                }
            }

            SetFocus(best);
            CurrentAction = bestAction;
        }

        void SetFocus(Interactable target)
        {
            if (Focused == target) return;
            if (Focused != null) Focused.SetFocused(false);
            Focused = target;
            if (Focused != null) Focused.SetFocused(true);
        }

        void DropHeld()
        {
            Item item = Release();
            if (item == null) return;
            item.DropLoose(holdAnchor.position, transform.forward * 1.2f + Vector3.up * 1.5f, controller);
            Sfx.Drop();
        }

        void Animate(float amount, float dt)
        {
            // 持っている間は両手を上げる
            float handBlend = 1f - Mathf.Exp(-18f * dt);
            if (handLeft != null) handLeft.localPosition = Vector3.Lerp(handLeft.localPosition, Held != null ? HandLeftRaised : handLeftRest, handBlend);
            if (handRight != null) handRight.localPosition = Vector3.Lerp(handRight.localPosition, Held != null ? HandRightRaised : handRightRest, handBlend);

            if (visual == null) return;
            if (amount > 0.1f)
            {
                bobTime += dt * (9f + 5f * amount);
                float bounce = Mathf.Abs(Mathf.Sin(bobTime));
                visual.localPosition = new Vector3(0f, bounce * 0.07f, 0f);
                visual.localRotation = Quaternion.Euler(6f * Mathf.Min(amount, 1.5f), 0f, Mathf.Sin(bobTime) * 4f);
            }
            else
            {
                bobTime = 0f;
                visual.localPosition = Vector3.Lerp(visual.localPosition, Vector3.zero, dt * 14f);
                visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.identity, dt * 14f);
            }
        }
    }
}
