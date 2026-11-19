using System;
using System.Collections.Generic;
using UnityEngine;

namespace DotaBata
{
    /// <summary>
    /// 「次にやること」を1つ選ぶ。やさしいモードの光るガイドと、動作確認用の自動プレイが使う。
    /// 急ぎの注文から順に、作りかけのグッズを割り当てていき、足りない分だけ材料棚を勧める。
    /// </summary>
    public static class GuideSystem
    {
        static readonly List<Order> Orders = new List<Order>();
        static readonly List<Order> Uncovered = new List<Order>();
        static readonly List<Item> Colored = new List<Item>();
        static readonly List<PaintColor> ColoredColors = new List<PaintColor>();
        static readonly List<Item> Plain = new List<Item>();
        static readonly HashSet<Item> Matched = new HashSet<Item>();
        static readonly HashSet<Item> InPaint = new HashSet<Item>();
        static readonly Dictionary<Item, PaintColor> Assignment = new Dictionary<Item, PaintColor>();
        static readonly Comparison<Order> ByUrgency = (a, b) => a.remaining.CompareTo(b.remaining);

        static Interactable best;
        static int bestPriority;
        static float bestDistance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Orders.Clear(); Colored.Clear(); ColoredColors.Clear(); Plain.Clear();
            Matched.Clear(); InPaint.Clear(); Assignment.Clear();
            best = null;
        }

        public static Interactable Suggest(PlayerController player)
        {
            if (player == null || OrderManager.I == null) return null;

            Analyze(out bool needPart, out ProductType neededType);
            Item held = player.Held;

            if (held != null)
            {
                switch (held.stage)
                {
                    case ItemStage.Boxed:
                        if (Matched.Contains(held)) return Nearest<ShippingGate>(player);
                        return FindStation(ProcessKind.Counter, PaintColor.None, player);
                    case ItemStage.Painted:
                        if (Matched.Contains(held))
                            return FindStation(ProcessKind.Pack, PaintColor.None, player) ?? FindStation(ProcessKind.Counter, PaintColor.None, player);
                        break;
                    case ItemStage.Part:
                        return FindStation(ProcessKind.Assemble, PaintColor.None, player) ?? FindStation(ProcessKind.Counter, PaintColor.None, player);
                }

                // 組み立て済み、または注文に合わなくなった色塗り済み：割り当てられた色の色塗り台へ
                if (Assignment.TryGetValue(held, out PaintColor wanted))
                {
                    ProcessStation machine = FindStation(ProcessKind.Paint, wanted, player);
                    if (machine != null) return machine;
                }
                return FindStation(ProcessKind.Counter, PaintColor.None, player);
            }

            // ここから手が空いているとき。数字が小さいほど優先
            best = null;
            bestPriority = int.MaxValue;
            bestDistance = float.MaxValue;

            bool packFree = AnyFree(ProcessKind.Pack, PaintColor.None);
            bool assembleFree = AnyFree(ProcessKind.Assemble, PaintColor.None);

            var stations = ProcessStation.AllStations;
            for (int i = 0; i < stations.Count; i++)
            {
                ProcessStation station = stations[i];
                Item item = station.Current;
                if (item == null) continue;

                bool assigned = Assignment.TryGetValue(item, out PaintColor color) && AnyFree(ProcessKind.Paint, color);

                if (station.NeedsWork(item))
                {
                    if (station.autoWork) continue; // 機械が作業中
                    if (station.kind == ProcessKind.Pack)
                    {
                        if (Matched.Contains(item)) Consider(station, 2, player);
                        else if (assigned) Consider(station, 5, player);
                    }
                    else
                    {
                        Consider(station, 6, player);
                    }
                    continue;
                }

                // 塗り終わったものは自動でベルトへ流れるので、取りに行かせない
                if (station.kind == ProcessKind.Paint && station.HasOutputBelt) continue;

                switch (item.stage)
                {
                    case ItemStage.Boxed:
                        if (Matched.Contains(item)) Consider(station, 1, player);
                        break;
                    case ItemStage.Painted:
                        if (Matched.Contains(item)) { if (packFree) Consider(station, 4, player); }
                        else if (assigned) Consider(station, 5, player);
                        break;
                    case ItemStage.Assembled:
                        if (assigned) Consider(station, 5, player);
                        break;
                    case ItemStage.Part:
                        if (assembleFree && station.kind != ProcessKind.Assemble) Consider(station, 7, player);
                        break;
                }
            }

            if (packFree)
            {
                for (int i = 0; i < ConveyorBelt.AllBelts.Count; i++)
                    if (ConveyorBelt.AllBelts[i].HasWaitingItem) Consider(ConveyorBelt.AllBelts[i], 3, player);
            }

            for (int i = 0; i < Item.AllItems.Count; i++)
                if (Item.AllItems[i].IsLoose) Consider(Item.AllItems[i], 8, player);

            if (needPart)
            {
                var all = Interactable.All;
                for (int i = 0; i < all.Count; i++)
                    if (all[i] is PartsShelf shelf && shelf.productType == neededType) Consider(shelf, 9, player);
            }

            return best;
        }

        /// <summary>注文と作りかけのグッズを突き合わせる。</summary>
        static void Analyze(out bool needPart, out ProductType neededType)
        {
            needPart = false;
            neededType = ProductType.AcrylicStand;
            Orders.Clear(); Colored.Clear(); ColoredColors.Clear(); Plain.Clear();
            Matched.Clear(); InPaint.Clear(); Assignment.Clear();

            var stations = ProcessStation.AllStations;
            var items = Item.AllItems;
            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                if (item == null || !item.enabled) continue;

                // 色塗り台で作業中のものは、その台の色になる予定
                PaintColor futureColor = item.color;
                bool painting = false;
                for (int s = 0; s < stations.Count; s++)
                {
                    ProcessStation station = stations[s];
                    if (station.kind == ProcessKind.Paint && station.Current == item && station.NeedsWork(item))
                    {
                        painting = true;
                        futureColor = station.paintColor;
                        break;
                    }
                }
                if (painting) InPaint.Add(item);

                if (painting || item.stage == ItemStage.Painted || item.stage == ItemStage.Boxed)
                {
                    Colored.Add(item);
                    ColoredColors.Add(futureColor);
                }
                else
                {
                    Plain.Add(item);
                }
            }

            Orders.AddRange(OrderManager.I.Active);
            Orders.Sort(ByUrgency);

            // 1周目：色が決まっているグッズを、急ぎの注文から順に当てる
            List<Order> uncovered = Uncovered;
            uncovered.Clear();
            for (int o = 0; o < Orders.Count; o++)
            {
                Order order = Orders[o];
                bool covered = false;
                for (int c = 0; c < Colored.Count; c++)
                {
                    Item item = Colored[c];
                    if (Matched.Contains(item) || item.type != order.type || ColoredColors[c] != order.color) continue;
                    Matched.Add(item);
                    covered = true;
                    break;
                }
                if (!covered) uncovered.Add(order);
            }

            // 注文に合わなくなった色塗り済みは、塗り直せば使える
            for (int c = 0; c < Colored.Count; c++)
            {
                Item item = Colored[c];
                if (!Matched.Contains(item) && item.stage == ItemStage.Painted && !InPaint.Contains(item)) Plain.Add(item);
            }

            // 2周目：残った注文に、まだ色のないグッズを当てる（進んでいるものを優先）
            for (int o = 0; o < uncovered.Count; o++)
            {
                Order order = uncovered[o];
                Item pick = null;
                for (int p = 0; p < Plain.Count; p++)
                {
                    Item item = Plain[p];
                    if (item.type != order.type || Assignment.ContainsKey(item)) continue;
                    if (pick == null || (int)item.stage > (int)pick.stage) pick = item;
                }
                if (pick != null)
                {
                    Assignment[pick] = order.color;
                }
                else if (!needPart)
                {
                    needPart = true;
                    neededType = order.type;
                }
            }
        }

        static void Consider(Interactable target, int priority, PlayerController player)
        {
            if (priority > bestPriority) return;
            Vector3 offset = target.GetPoint(player) - player.transform.position;
            offset.y = 0f;
            float distance = offset.sqrMagnitude;
            if (priority < bestPriority || distance < bestDistance)
            {
                best = target;
                bestPriority = priority;
                bestDistance = distance;
            }
        }

        static bool AnyFree(ProcessKind kind, PaintColor color)
        {
            var stations = ProcessStation.AllStations;
            for (int i = 0; i < stations.Count; i++)
            {
                ProcessStation s = stations[i];
                if (s.kind == kind && s.IsFree && (kind != ProcessKind.Paint || s.paintColor == color)) return true;
            }
            return false;
        }

        /// <summary>空いている台のうち、いちばん近いもの。</summary>
        static ProcessStation FindStation(ProcessKind kind, PaintColor color, PlayerController player)
        {
            ProcessStation result = null;
            float resultDistance = float.MaxValue;
            var stations = ProcessStation.AllStations;
            for (int i = 0; i < stations.Count; i++)
            {
                ProcessStation s = stations[i];
                if (s.kind != kind || !s.IsFree) continue;
                if (kind == ProcessKind.Paint && s.paintColor != color) continue;
                float distance = (s.transform.position - player.transform.position).sqrMagnitude;
                if (distance < resultDistance) { resultDistance = distance; result = s; }
            }
            return result;
        }

        static T Nearest<T>(PlayerController player) where T : Interactable
        {
            T result = null;
            float resultDistance = float.MaxValue;
            var all = Interactable.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (!(all[i] is T candidate)) continue;
                float distance = (candidate.transform.position - player.transform.position).sqrMagnitude;
                if (distance < resultDistance) { resultDistance = distance; result = candidate; }
            }
            return result;
        }
    }
}
