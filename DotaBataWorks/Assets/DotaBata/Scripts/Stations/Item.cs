using System.Collections.Generic;
using UnityEngine;

namespace DotaBata
{
    /// <summary>
    /// 運ぶグッズ1つ。パーツ → 組み立て済み → 色塗り済み → 箱詰め済み と進む。
    /// 手に持っている間・台の上では物理を止め、床に落としたときだけ物理で動かす。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Item : Interactable
    {
        public static readonly List<Item> AllItems = new List<Item>();

        [Header("状態")]
        public ProductType type;
        public ItemStage stage;
        public PaintColor color;

        [Header("見た目（状態ごとに出し分ける）")]
        public GameObject partStand;
        public GameObject partBadge;
        public GameObject stand;
        public GameObject badge;
        public GameObject box;
        public GameObject boxIconStand;
        public GameObject boxIconBadge;
        [Tooltip("色を塗り替える部分")]
        public Renderer[] paintTargets;
        [Tooltip("PaintColor の順（しろ・ピンク・みずいろ・きいろ）")]
        public Material[] colorMaterials;

        Rigidbody body;

        /// <summary>床に落ちていて、拾える状態か。</summary>
        public bool IsLoose { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetItemStatics() { AllItems.Clear(); }

        protected override void Awake()
        {
            base.Awake();
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            if (mainCollider != null) mainCollider.enabled = false;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            AllItems.Add(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            AllItems.Remove(this);
        }

        public void Setup(ProductType newType, ItemStage newStage, PaintColor newColor)
        {
            type = newType;
            stage = newStage;
            color = newColor;
            Refresh();
        }

        public void SetStage(ItemStage newStage)
        {
            stage = newStage;
            Refresh();
        }

        public void SetColor(PaintColor newColor)
        {
            color = newColor;
            Refresh();
        }

        public void Refresh()
        {
            bool isStand = type == ProductType.AcrylicStand;
            bool isPart = stage == ItemStage.Part;
            bool isBuilt = stage == ItemStage.Assembled || stage == ItemStage.Painted;
            bool isBoxed = stage == ItemStage.Boxed;

            SetActive(partStand, isPart && isStand);
            SetActive(partBadge, isPart && !isStand);
            SetActive(stand, isBuilt && isStand);
            SetActive(badge, isBuilt && !isStand);
            SetActive(box, isBoxed);
            SetActive(boxIconStand, isBoxed && isStand);
            SetActive(boxIconBadge, isBoxed && !isStand);

            int index = (int)color;
            if (colorMaterials != null && index >= 0 && index < colorMaterials.Length && colorMaterials[index] != null && paintTargets != null)
            {
                for (int i = 0; i < paintTargets.Length; i++)
                    if (paintTargets[i] != null) paintTargets[i].sharedMaterial = colorMaterials[index];
            }
        }

        static void SetActive(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }

        /// <summary>手や台に固定する。</summary>
        public void AttachTo(Transform anchor)
        {
            IsLoose = false;
            body.isKinematic = true;
            if (mainCollider != null) mainCollider.enabled = false;
            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>床に落とす（ここからは物理で動く）。</summary>
        public void DropLoose(Vector3 position, Vector3 velocity, Collider ignore)
        {
            Transform parent = StageBuilder.I != null ? StageBuilder.I.ItemsRoot : null;
            transform.SetParent(parent, true);
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            if (mainCollider != null)
            {
                mainCollider.enabled = true;
                if (ignore != null) Physics.IgnoreCollision(mainCollider, ignore, true);
            }
            body.isKinematic = false;
            body.linearVelocity = velocity;
            body.angularVelocity = Vector3.zero;
            IsLoose = true;
        }

        public override Vector3 GetPoint(PlayerController player)
        {
            Vector3 p = transform.position;
            p.y = 0f;
            return p;
        }

        public override Vector3 GetGuidePosition(PlayerController player)
        {
            return transform.position + Vector3.up * 0.9f;
        }

        public override ActionKind GetAction(PlayerController player)
        {
            return IsLoose && player.Held == null ? ActionKind.Take : ActionKind.None;
        }

        public override void OnActionDown(PlayerController player)
        {
            if (IsLoose && player.Held == null) player.Pickup(this);
        }

        void Update()
        {
            // ステージの外に落ちたものは片付ける
            if (IsLoose && transform.position.y < -4f) Destroy(gameObject);
        }
    }
}
