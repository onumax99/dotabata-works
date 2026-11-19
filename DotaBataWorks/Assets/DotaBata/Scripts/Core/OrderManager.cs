using System;
using System.Collections.Generic;
using UnityEngine;

namespace DotaBata
{
    /// <summary>画面上に出ている注文1つ。</summary>
    public class Order
    {
        public int id;
        public ProductType type;
        public PaintColor color;
        public float patience;
        public float remaining;
        public float Ratio => patience > 0f ? Mathf.Clamp01(remaining / patience) : 0f;
    }

    /// <summary>注文表（LevelData）に沿って注文を出し、期限切れを数える。</summary>
    public class OrderManager : MonoBehaviour
    {
        public static OrderManager I { get; private set; }

        public readonly List<Order> Active = new List<Order>();
        public event Action<Order> OrderAdded;
        /// <summary>第2引数は「届けて完了したか」。false は期限切れ。</summary>
        public event Action<Order, bool> OrderRemoved;
        public event Action<Order> OrderMissed;

        readonly Queue<OrderEntry> backlog = new Queue<OrderEntry>();
        LevelData level;
        int nextIndex;
        int nextId;
        float fillerTimer;
        float patienceMultiplier = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        void Awake() { I = this; }

        public void Begin(LevelData data, bool easy)
        {
            level = data;
            Active.Clear();
            backlog.Clear();
            nextIndex = 0;
            fillerTimer = 0f;
            patienceMultiplier = easy ? data.easyPatienceMultiplier : 1f;
        }

        public void Tick(float elapsed, float dt)
        {
            if (level == null) return;

            // 注文表から、時間になったものを出す
            while (nextIndex < level.orders.Count && level.orders[nextIndex].time <= elapsed)
                backlog.Enqueue(level.orders[nextIndex++]);

            // 手が空いているプレイヤーを待たせない：注文が0なら次を前倒しで出す
            if (Active.Count == 0 && backlog.Count == 0 && nextIndex < level.orders.Count)
                backlog.Enqueue(level.orders[nextIndex++]);

            while (backlog.Count > 0 && Active.Count < level.maxActiveOrders)
                Add(backlog.Dequeue());

            // 注文表を出し切ったら、ランダム注文で埋める
            if (nextIndex >= level.orders.Count && backlog.Count == 0 && level.fillerInterval > 0f)
            {
                fillerTimer += dt;
                if (Active.Count == 0 || (fillerTimer >= level.fillerInterval && Active.Count < level.maxActiveOrders))
                {
                    fillerTimer = 0f;
                    Add(RandomEntry());
                }
            }

            for (int i = Active.Count - 1; i >= 0; i--)
            {
                Order o = Active[i];
                o.remaining -= dt;
                if (o.remaining <= 0f)
                {
                    Active.RemoveAt(i);
                    OrderRemoved?.Invoke(o, false);
                    OrderMissed?.Invoke(o);
                }
            }
        }

        /// <summary>種類と色が合う注文のうち、残り時間がいちばん少ないもの。</summary>
        public Order FindMatch(ProductType type, PaintColor color)
        {
            Order best = null;
            for (int i = 0; i < Active.Count; i++)
            {
                Order o = Active[i];
                if (o.type != type || o.color != color) continue;
                if (best == null || o.remaining < best.remaining) best = o;
            }
            return best;
        }

        public void Complete(Order order)
        {
            if (Active.Remove(order)) OrderRemoved?.Invoke(order, true);
        }

        /// <summary>邪魔ギミック「注文の急増」。</summary>
        public void Rush(int count)
        {
            for (int i = 0; i < count; i++) backlog.Enqueue(RandomEntry());
        }

        void Add(OrderEntry e)
        {
            float patience = (e.patience > 0f ? e.patience : level.defaultPatience) * patienceMultiplier;
            var o = new Order
            {
                id = nextId++,
                type = e.type,
                color = e.color == PaintColor.None ? PaintColor.Pink : e.color,
                patience = patience,
                remaining = patience,
            };
            Active.Add(o);
            OrderAdded?.Invoke(o);
        }

        static OrderEntry RandomEntry()
        {
            return new OrderEntry
            {
                type = UnityEngine.Random.value < 0.5f ? ProductType.AcrylicStand : ProductType.CanBadge,
                color = (PaintColor)UnityEngine.Random.Range(1, 4),
            };
        }
    }
}
