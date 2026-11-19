using UnityEngine;

namespace DotaBata
{
    /// <summary>グッズの種類。</summary>
    public enum ProductType { AcrylicStand = 0, CanBadge = 1 }

    /// <summary>塗る色。None は未塗装。</summary>
    public enum PaintColor { None = 0, Pink = 1, Blue = 2, Yellow = 3 }

    /// <summary>グッズの進み具合。パーツ → 組み立て済み → 色塗り済み → 箱詰め済み。</summary>
    public enum ItemStage { Part = 0, Assembled = 1, Painted = 2, Boxed = 3 }

    /// <summary>アクションボタンが今なにをするか（近くのものに合わせて自動で切り替わる）。</summary>
    public enum ActionKind { None, Take, Put, Work, Ship, Trash }

    /// <summary>邪魔ギミックと応援タイム。</summary>
    public enum GimmickKind { ConveyorReverse, OrderRush, CheerTime }

    public enum GameState { Title, Countdown, Playing, Finished }

    public static class GameText
    {
        public static string Name(ProductType t) => t == ProductType.AcrylicStand ? "アクスタ" : "缶バッジ";

        public static string Name(PaintColor c)
        {
            switch (c)
            {
                case PaintColor.Pink: return "ピンク";
                case PaintColor.Blue: return "みずいろ";
                case PaintColor.Yellow: return "きいろ";
                default: return "しろ";
            }
        }

        public static string Label(ActionKind a)
        {
            switch (a)
            {
                case ActionKind.Take: return "とる";
                case ActionKind.Put: return "おく";
                case ActionKind.Work: return "つくる";
                case ActionKind.Ship: return "出荷！";
                case ActionKind.Trash: return "すてる";
                default: return "";
            }
        }
    }

    public static class GameColors
    {
        public static readonly Color Pink = new Color32(0xFF, 0x6F, 0xA8, 0xFF);
        public static readonly Color Blue = new Color32(0x4F, 0xC3, 0xF7, 0xFF);
        public static readonly Color Yellow = new Color32(0xFF, 0xD5, 0x4A, 0xFF);
        public static readonly Color White = new Color32(0xF6, 0xF3, 0xEE, 0xFF);

        public static Color Of(PaintColor c)
        {
            switch (c)
            {
                case PaintColor.Pink: return Pink;
                case PaintColor.Blue: return Blue;
                case PaintColor.Yellow: return Yellow;
                default: return White;
            }
        }
    }
}
