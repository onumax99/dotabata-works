using System.Collections.Generic;
using UnityEngine;

namespace DotaBata
{
    public enum ProcessKind
    {
        /// <summary>ただの置き台。</summary>
        Counter,
        /// <summary>組み立て台：パーツ → 組み立て済み（長押し）。</summary>
        Assemble,
        /// <summary>色塗り台：組み立て済み → 色塗り済み（機械が自動で作業）。</summary>
        Paint,
        /// <summary>梱包台：色塗り済み → 箱詰め済み（長押し）。</summary>
        Pack,
    }

    /// <summary>
    /// グッズを1つ置いて作業する台。組み立て台・色塗り台・梱包台・置き台を、設定だけで作り分ける。
    /// 色塗り台は自動で動くので、塗っている間に次の材料を取りに行ける。
    /// </summary>
    public class ProcessStation : Interactable
    {
        public static readonly List<ProcessStation> AllStations = new List<ProcessStation>();

        [Header("作業の設定")]
        public ProcessKind kind = ProcessKind.Counter;
        [Tooltip("色塗り台が塗る色")]
        public PaintColor paintColor = PaintColor.None;
        [Tooltip("作業にかかる秒数")]
        public float workSeconds = 1.2f;
        [Tooltip("true なら機械が自動で作業する。false なら長押し")]
        public bool autoWork;
        [Tooltip("グッズを置く位置")]
        public Transform slot;
        [Tooltip("色塗り台：塗り終わったらこの距離以内のベルトに自動で流す")]
        public float beltSearchRadius = 1.3f;
        [Tooltip("作業中に回す飾り（任意）")]
        public Transform spinner;

        public Item Current { get; private set; }
        public float Progress01 => workSeconds > 0f ? Mathf.Clamp01(progress / workSeconds) : 0f;
        public bool ShowProgress => Current != null && NeedsWork(Current) && progress > 0f;
        public bool IsFree => Current == null;
        /// <summary>塗り終わったものを自動でベルトへ流す台か。</summary>
        public bool HasOutputBelt => outputBelt != null;

        float progress;
        float ejectDelay;
        ConveyorBelt outputBelt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStationStatics() { AllStations.Clear(); }

        protected override void OnEnable()
        {
            base.OnEnable();
            AllStations.Add(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            AllStations.Remove(this);
        }

        void Start()
        {
            if (kind != ProcessKind.Paint) return;
            float best = beltSearchRadius;
            for (int i = 0; i < ConveyorBelt.AllBelts.Count; i++)
            {
                ConveyorBelt belt = ConveyorBelt.AllBelts[i];
                float distance = belt.DistanceTo(transform.position);
                if (distance <= best) { best = distance; outputBelt = belt; }
            }
        }

        /// <summary>この台に置けるか。</summary>
        public bool Accepts(Item item)
        {
            if (item == null) return false;
            switch (kind)
            {
                case ProcessKind.Assemble: return item.stage == ItemStage.Part;
                case ProcessKind.Paint: return item.stage == ItemStage.Assembled || (item.stage == ItemStage.Painted && item.color != paintColor);
                case ProcessKind.Pack: return item.stage == ItemStage.Painted;
                default: return true;
            }
        }

        /// <summary>置いてあるグッズに、まだ作業が残っているか。</summary>
        public bool NeedsWork(Item item)
        {
            if (item == null) return false;
            switch (kind)
            {
                case ProcessKind.Assemble: return item.stage == ItemStage.Part;
                case ProcessKind.Paint: return item.stage == ItemStage.Assembled || (item.stage == ItemStage.Painted && item.color != paintColor);
                case ProcessKind.Pack: return item.stage == ItemStage.Painted;
                default: return false;
            }
        }

        public override ActionKind GetAction(PlayerController player)
        {
            if (Current == null)
                return player.Held != null && Accepts(player.Held) ? ActionKind.Put : ActionKind.None;

            if (NeedsWork(Current))
            {
                if (autoWork) return ActionKind.None; // 機械が作業中。終わるまで待つ
                return player.Held == null ? ActionKind.Work : ActionKind.None;
            }
            return player.Held == null ? ActionKind.Take : ActionKind.None;
        }

        public override void OnActionDown(PlayerController player)
        {
            switch (GetAction(player))
            {
                case ActionKind.Put:
                    Place(player.Release());
                    Sfx.Put();
                    break;
                case ActionKind.Take:
                    Item item = Current;
                    Current = null;
                    progress = 0f;
                    player.Pickup(item);
                    break;
            }
        }

        public override void OnActionHold(PlayerController player, float dt)
        {
            if (autoWork || Current == null || player.Held != null || !NeedsWork(Current)) return;
            Advance(dt);
        }

        void Place(Item item)
        {
            if (item == null) return;
            Current = item;
            progress = 0f;
            item.AttachTo(slot != null ? slot : transform);
        }

        void Update()
        {
            if (GameManager.I != null && !GameManager.I.IsPlaying) return;

            if (autoWork && Current != null && NeedsWork(Current)) Advance(Time.deltaTime);

            // 色塗り台：塗り終わったら、少し見せてからベルトへ流す
            if (kind == ProcessKind.Paint && Current != null && !NeedsWork(Current) && outputBelt != null)
            {
                ejectDelay -= Time.deltaTime;
                if (ejectDelay <= 0f && outputBelt.TryAdd(Current, transform.position))
                {
                    Current = null;
                    progress = 0f;
                }
            }
        }

        void Advance(float dt)
        {
            progress += dt;
            if (spinner != null) spinner.Rotate(0f, 540f * dt, 0f, Space.Self);
            if (progress < workSeconds) return;

            progress = 0f;
            switch (kind)
            {
                case ProcessKind.Assemble:
                    Current.SetStage(ItemStage.Assembled);
                    break;
                case ProcessKind.Paint:
                    Current.color = paintColor;
                    Current.SetStage(ItemStage.Painted);
                    ejectDelay = 0.35f;
                    break;
                case ProcessKind.Pack:
                    Current.SetStage(ItemStage.Boxed);
                    break;
            }
            Sfx.Complete();
            if (GameManager.I != null)
                GameManager.I.ShowPopup(LabelPosition, kind == ProcessKind.Pack ? "箱づめOK！" : "できた！", new Color32(0xFF, 0xD5, 0x4A, 0xFF));
        }
    }
}
