using System;
using System.Collections.Generic;
using UnityEngine;

namespace DotaBata
{
    /// <summary>部品（持ち場）の配置1つ分。</summary>
    [Serializable]
    public class StationPlacement
    {
        public string note;
        public GameObject prefab;
        public Vector3 position;
        public float rotationY;
        [Tooltip("ベルトコンベアの長さ。0ならプレハブの値を使う")]
        public float length;
    }

    /// <summary>注文表の1行。</summary>
    [Serializable]
    public class OrderEntry
    {
        [Tooltip("ステージ開始から何秒後に出すか")]
        public float time;
        public ProductType type;
        public PaintColor color = PaintColor.Pink;
        [Tooltip("待ってくれる秒数。0ならレベルの既定値")]
        public float patience;
    }

    /// <summary>ギミック設定の1行。</summary>
    [Serializable]
    public class GimmickEvent
    {
        [Tooltip("ステージ開始から何秒後に起きるか")]
        public float time;
        public GimmickKind kind;
        [Tooltip("逆回転・応援タイムの長さ（秒）")]
        public float duration = 8f;
        [Tooltip("注文ラッシュで増える注文の数")]
        public int count = 3;
    }

    /// <summary>
    /// 1ステージ分のデータ。企画書の「部品の配置＋注文表＋ギミック設定」をそのまま持つ。
    /// ステージを増やすときは、このアセットを複製して中身を変えるだけでよい。
    /// </summary>
    [CreateAssetMenu(fileName = "Level", menuName = "DotaBata/Level Data")]
    public class LevelData : ScriptableObject
    {
        [Header("ステージ情報")]
        public string themeName = "グッズ工場";
        public string stageName = "ステージ1";
        public string managerName = "店長";

        [Header("ルール")]
        [Tooltip("制限時間（秒）。企画書では60〜120秒")]
        public float timeLimit = 90f;
        [Tooltip("★1〜3に必要なスコア")]
        public int[] starScores = { 300, 600, 900 };
        public int maxActiveOrders = 4;
        [Tooltip("注文が待ってくれる秒数の既定値")]
        public float defaultPatience = 40f;
        public int deliverBaseScore = 100;
        [Tooltip("早く届けたときのボーナスの上限")]
        public int speedBonusMax = 50;
        public int missPenalty = 30;
        public int wrongPenalty = 20;
        [Tooltip("注文表を出し切ったあと、何秒おきにランダム注文を足すか。0で足さない")]
        public float fillerInterval = 8f;

        [Header("やさしいモード")]
        public float easyTimeMultiplier = 1.5f;
        public float easyPatienceMultiplier = 1.5f;

        [Header("応援タイム")]
        public float cheerSpeedMultiplier = 1.35f;

        [Header("部品の配置")]
        public Vector2 floorSize = new Vector2(7f, 10f);
        public Vector3 playerStart = new Vector3(0f, 0f, -1f);
        public Material floorMaterial;
        public Material wallMaterial;
        public List<StationPlacement> stations = new List<StationPlacement>();

        [Header("注文表")]
        public List<OrderEntry> orders = new List<OrderEntry>();

        [Header("ギミック設定")]
        public List<GimmickEvent> gimmicks = new List<GimmickEvent>();
    }
}
