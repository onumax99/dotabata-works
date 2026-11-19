using System.Collections.Generic;
using UnityEngine;

namespace DotaBata
{
    /// <summary>
    /// アクションボタンで触れるもの（持ち場・床に落ちたグッズ）の共通の土台。
    /// 「今このプレイヤーが押したら何が起きるか」を GetAction で答える。
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        [Tooltip("画面に出す持ち場の名前。空なら出さない")]
        public string displayName;
        [Tooltip("プレイヤーが近づいたときに光らせる目印")]
        public GameObject focusMarker;
        [Tooltip("名前ラベルを出す高さ（m）")]
        public float labelHeight = 1.5f;

        protected Collider mainCollider;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }

        protected virtual void Awake()
        {
            mainCollider = GetComponent<Collider>();
            if (focusMarker != null) focusMarker.SetActive(false);
        }

        protected virtual void OnEnable() { All.Add(this); }
        protected virtual void OnDisable() { All.Remove(this); }

        /// <summary>プレイヤーから見て、いちばん近い「触れる点」（床の高さ）。</summary>
        public virtual Vector3 GetPoint(PlayerController player)
        {
            Vector3 from = player.transform.position;
            from.y = 0.3f;
            Vector3 point = (mainCollider != null && mainCollider.enabled) ? mainCollider.ClosestPoint(from) : transform.position;
            point.y = 0f;
            return point;
        }

        /// <summary>ガイドの矢印を出す位置。</summary>
        public virtual Vector3 GetGuidePosition(PlayerController player)
        {
            return transform.position + Vector3.up * labelHeight;
        }

        public Vector3 LabelPosition => transform.position + Vector3.up * labelHeight;

        public abstract ActionKind GetAction(PlayerController player);
        public virtual void OnActionDown(PlayerController player) { }
        public virtual void OnActionHold(PlayerController player, float dt) { }

        public virtual void SetFocused(bool on)
        {
            if (focusMarker != null) focusMarker.SetActive(on);
        }
    }
}
