using System.Collections.Generic;
using UnityEngine;

namespace DotaBata
{
    /// <summary>
    /// ベルトコンベア。乗せたグッズを自分の +Z 方向へ運ぶ。
    /// 邪魔ギミック「逆回転」で向きが反転し、塗った品が戻ってくる。
    /// 見た目は長さに合わせて起動時に組み立てる。
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ConveyorBelt : Interactable
    {
        public static readonly List<ConveyorBelt> AllBelts = new List<ConveyorBelt>();

        [Header("形")]
        public float length = 7f;
        public float width = 0.9f;
        public float height = 0.5f;

        [Header("動き")]
        [Tooltip("運ぶ速さ（m/秒）")]
        public float speed = 1.3f;
        [Tooltip("グッズ同士の間隔（m）")]
        public float spacing = 0.55f;

        [Header("見た目")]
        public Material bodyMaterial;
        public Material railMaterial;
        public Material stripeMaterial;
        public Material stripeReverseMaterial;
        public Material focusMaterial;

        public int Direction { get; private set; } = 1;
        public int ItemCount => entries.Count;

        class Entry
        {
            public Item item;
            public float t;
        }

        readonly List<Entry> entries = new List<Entry>();
        readonly List<Transform> stripes = new List<Transform>();
        readonly List<Renderer> stripeRenderers = new List<Renderer>();
        Transform itemsRoot;
        BoxCollider box;
        float stripeOffset;
        const float StripeGap = 0.6f;

        float HalfLength => Mathf.Max(0.1f, length * 0.5f - 0.35f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBeltStatics() { AllBelts.Clear(); }

        protected override void OnEnable()
        {
            base.OnEnable();
            AllBelts.Add(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            AllBelts.Remove(this);
        }

        void Start() { BuildVisual(); }

        void BuildVisual()
        {
            box = GetComponent<BoxCollider>();
            box.size = new Vector3(width, height, length);
            box.center = new Vector3(0f, height * 0.5f, 0f);

            Part(PrimitiveType.Cube, "Body", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, length), bodyMaterial);
            float railX = width * 0.5f - 0.04f;
            Part(PrimitiveType.Cube, "Rail_L", new Vector3(-railX, height + 0.04f, 0f), new Vector3(0.08f, 0.1f, length), railMaterial);
            Part(PrimitiveType.Cube, "Rail_R", new Vector3(railX, height + 0.04f, 0f), new Vector3(0.08f, 0.1f, length), railMaterial);

            int count = Mathf.Max(2, Mathf.FloorToInt(length / StripeGap));
            for (int i = 0; i < count; i++)
            {
                GameObject stripe = Part(PrimitiveType.Cube, "Stripe", Vector3.zero, new Vector3(width - 0.26f, 0.02f, 0.14f), stripeMaterial);
                stripes.Add(stripe.transform);
                stripeRenderers.Add(stripe.GetComponent<Renderer>());
            }
            UpdateStripes();

            itemsRoot = new GameObject("BeltItems").transform;
            itemsRoot.SetParent(transform, false);

            GameObject marker = Part(PrimitiveType.Cube, "Focus", new Vector3(0f, 0.02f, 0f), new Vector3(width + 0.24f, 0.04f, length + 0.24f), focusMaterial);
            focusMarker = marker;
            marker.SetActive(false);
        }

        GameObject Part(PrimitiveType primitive, string partName, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(primitive);
            go.name = partName;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        public void SetDirection(int direction)
        {
            Direction = direction >= 0 ? 1 : -1;
            Material m = Direction > 0 ? stripeMaterial : stripeReverseMaterial;
            if (m == null) return;
            for (int i = 0; i < stripeRenderers.Count; i++) stripeRenderers[i].sharedMaterial = m;
        }

        Vector3 LocalPoint(float t) { return new Vector3(0f, height + 0.02f, t); }

        float Project(Vector3 world)
        {
            return Mathf.Clamp(transform.InverseTransformPoint(world).z, -HalfLength, HalfLength);
        }

        /// <summary>ベルトの中心線までの水平距離。</summary>
        public float DistanceTo(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            float dz = Mathf.Max(0f, Mathf.Abs(local.z) - length * 0.5f);
            float dx = Mathf.Max(0f, Mathf.Abs(local.x) - width * 0.5f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public bool HasSpace(float t)
        {
            for (int i = 0; i < entries.Count; i++)
                if (Mathf.Abs(entries[i].t - t) < spacing * 0.9f) return false;
            return true;
        }

        /// <summary>worldPos にいちばん近い位置へ乗せる。場所が空いていなければ false。</summary>
        public bool TryAdd(Item item, Vector3 worldPos)
        {
            if (item == null || itemsRoot == null) return false;
            float t = Project(worldPos);
            if (!HasSpace(t)) return false;
            entries.Add(new Entry { item = item, t = t });
            item.AttachTo(itemsRoot);
            item.transform.localPosition = LocalPoint(t);
            return true;
        }

        Entry Nearest(Vector3 world)
        {
            float z = transform.InverseTransformPoint(world).z;
            Entry best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < entries.Count; i++)
            {
                float distance = Mathf.Abs(entries[i].t - z);
                if (distance < bestDistance) { bestDistance = distance; best = entries[i]; }
            }
            return best;
        }

        /// <summary>端で止まって待っているグッズがあるか（ガイド用）。</summary>
        public bool HasWaitingItem
        {
            get
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (Direction > 0 && entries[i].t >= HalfLength - 0.02f) return true;
                    if (Direction < 0 && entries[i].t <= -HalfLength + 0.02f) return true;
                }
                return false;
            }
        }

        public override Vector3 GetPoint(PlayerController player)
        {
            Vector3 local = transform.InverseTransformPoint(player.transform.position);
            float x = Mathf.Clamp(local.x, -width * 0.5f, width * 0.5f);
            float z;
            if (player.Held == null)
            {
                Entry e = Nearest(player.transform.position);
                z = e != null ? e.t : Mathf.Clamp(local.z, -HalfLength, HalfLength);
            }
            else
            {
                z = Mathf.Clamp(local.z, -length * 0.5f, length * 0.5f);
            }
            Vector3 world = transform.TransformPoint(new Vector3(x, 0f, z));
            world.y = 0f;
            return world;
        }

        public override Vector3 GetGuidePosition(PlayerController player)
        {
            Entry e = Nearest(player.transform.position);
            Vector3 local = e != null ? LocalPoint(e.t) : Vector3.zero;
            return transform.TransformPoint(local) + Vector3.up * 0.9f;
        }

        public override ActionKind GetAction(PlayerController player)
        {
            if (player.Held == null) return entries.Count > 0 ? ActionKind.Take : ActionKind.None;
            return HasSpace(Project(player.transform.position)) ? ActionKind.Put : ActionKind.None;
        }

        public override void OnActionDown(PlayerController player)
        {
            if (player.Held == null)
            {
                Entry e = Nearest(player.transform.position);
                if (e == null) return;
                entries.Remove(e);
                player.Pickup(e.item);
            }
            else if (HasSpace(Project(player.transform.position)))
            {
                Item item = player.Release();
                TryAdd(item, player.transform.position);
                Sfx.Put();
            }
        }

        void Update()
        {
            if (GameManager.I != null && !GameManager.I.IsPlaying) return;
            float step = speed * Time.deltaTime;

            entries.Sort((a, b) => a.t.CompareTo(b.t));
            if (Direction > 0)
            {
                float limit = HalfLength;
                for (int i = entries.Count - 1; i >= 0; i--)
                {
                    Entry e = entries[i];
                    e.t = Mathf.Min(e.t + step, Mathf.Max(limit, e.t));
                    limit = e.t - spacing;
                }
            }
            else
            {
                float limit = -HalfLength;
                for (int i = 0; i < entries.Count; i++)
                {
                    Entry e = entries[i];
                    e.t = Mathf.Max(e.t - step, Mathf.Min(limit, e.t));
                    limit = e.t + spacing;
                }
            }

            for (int i = 0; i < entries.Count; i++)
                if (entries[i].item != null) entries[i].item.transform.localPosition = LocalPoint(entries[i].t);

            stripeOffset = Mathf.Repeat(stripeOffset + step * Direction, StripeGap);
            UpdateStripes();
        }

        void UpdateStripes()
        {
            float half = length * 0.5f - 0.1f;
            float span = stripes.Count * StripeGap;
            for (int i = 0; i < stripes.Count; i++)
            {
                float z = Mathf.Repeat(i * StripeGap + stripeOffset, span) - span * 0.5f;
                bool visible = Mathf.Abs(z) <= half;
                if (stripes[i].gameObject.activeSelf != visible) stripes[i].gameObject.SetActive(visible);
                stripes[i].localPosition = new Vector3(0f, height + 0.005f, z);
            }
        }
    }
}
