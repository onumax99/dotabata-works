using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DotaBata
{
    /// <summary>
    /// 1ステージの進行役。タイトル → カウントダウン → プレイ → 結果 の流れ、
    /// スコア、ギミックのタイムライン、応援タイムを管理する。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        [Tooltip("このシーンで遊ぶステージのデータ")]
        public LevelData level;

        public GameState State { get; private set; } = GameState.Title;
        public bool EasyMode { get; private set; }
        public float TimeLimit { get; private set; }
        public float TimeLeft { get; private set; }
        public float Elapsed { get; private set; }
        public int Score { get; private set; }
        public int Delivered { get; private set; }
        public int Missed { get; private set; }
        public int Wrong { get; private set; }
        public int BestScore { get; private set; }
        public bool NewBest { get; private set; }
        public PlayerController Player { get; private set; }

        public bool IsPlaying => State == GameState.Playing;
        public bool CheerActive => cheerTimer > 0f;
        public bool ReverseActive => reverseTimer > 0f;
        public float SpeedMultiplier => cheerTimer > 0f ? level.cheerSpeedMultiplier : 1f;

        public int Stars
        {
            get
            {
                int stars = 0;
                for (int i = 0; i < level.starScores.Length; i++)
                    if (Score >= level.starScores[i]) stars = i + 1;
                return stars;
            }
        }

        public event Action<GameState> StateChanged;
        /// <summary>画面中央に出す大きな告知（文字, 色）。</summary>
        public event Action<string, Color> Banner;
        /// <summary>ボイスの代わりに出すセリフ。</summary>
        public event Action<string> Voice;
        /// <summary>ワールド座標から浮かび上がる小さな文字。</summary>
        public event Action<Vector3, string, Color> Popup;

        static readonly Color WarnColor = new Color32(0xFF, 0x70, 0x43, 0xFF);
        static readonly Color GoodColor = new Color32(0x4C, 0xC9, 0x7A, 0xFF);
        static readonly Color CheerColor = new Color32(0xFF, 0x6F, 0xA8, 0xFF);
        static readonly Color BadColor = new Color32(0xEF, 0x53, 0x50, 0xFF);
        static readonly string[] NiceLines = { "ナイス！", "いい感じ！", "その調子！", "やったー！" };

        OrderManager orders;
        bool[] warned;
        bool[] fired;
        float reverseTimer;
        float cheerTimer;
        bool hurryCalled;
        int lastTickSecond;

        string BestKey => "DotaBata.Best." + level.name + (EasyMode ? ".easy" : ".normal");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        void Awake()
        {
            I = this;
            orders = GetComponent<OrderManager>();
            Sfx.Init(GetComponent<AudioSource>());
            if (Application.isMobilePlatform) Application.targetFrameRate = 30; // 企画書の性能目標

            if (level == null)
            {
                Debug.LogError("[DotaBata] GameManager に LevelData が設定されていません。");
                enabled = false;
                return;
            }

            StageBuilder builder = GetComponent<StageBuilder>();
            Player = builder.Build(level);
            TimeLimit = level.timeLimit;
            TimeLeft = TimeLimit;
            BestScore = PlayerPrefs.GetInt(BestKey, 0);
        }

        void Start()
        {
            Camera cam = Camera.main;
            StageCamera stageCamera = cam != null ? cam.GetComponent<StageCamera>() : null;
            if (stageCamera != null) stageCamera.Configure(level.floorSize);
            orders.OrderMissed += OnOrderMissed;
        }

        public int GetBestScore(bool easy)
        {
            return PlayerPrefs.GetInt("DotaBata.Best." + level.name + (easy ? ".easy" : ".normal"), 0);
        }

        /// <summary>タイトルのボタンから呼ぶ。easy = やさしいモード。</summary>
        public void StartGame(bool easy)
        {
            if (State != GameState.Title) return;
            EasyMode = easy;
            TimeLimit = level.timeLimit * (easy ? level.easyTimeMultiplier : 1f);
            TimeLeft = TimeLimit;
            Elapsed = 0f;
            Score = 0;
            Delivered = Missed = Wrong = 0;
            BestScore = PlayerPrefs.GetInt(BestKey, 0);
            NewBest = false;
            warned = new bool[level.gimmicks.Count];
            fired = new bool[level.gimmicks.Count];
            reverseTimer = cheerTimer = 0f;
            hurryCalled = false;
            lastTickSecond = -1;
            orders.Begin(level, easy);
            StartCoroutine(CountdownRoutine());
        }

        IEnumerator CountdownRoutine()
        {
            SetState(GameState.Countdown);
            for (int n = 3; n >= 1; n--)
            {
                Banner?.Invoke(n.ToString(), Color.white);
                Sfx.Tick();
                yield return new WaitForSeconds(0.7f);
            }
            Banner?.Invoke("スタート！", GoodColor);
            Sfx.Start();
            SetState(GameState.Playing);
        }

        void Update()
        {
            if (State != GameState.Playing) return;
            float dt = Time.deltaTime;
            Elapsed += dt;
            TimeLeft -= dt;

            orders.Tick(Elapsed, dt);
            TickGimmicks(dt);

            if (!hurryCalled && TimeLeft <= 10f)
            {
                hurryCalled = true;
                Voice?.Invoke("「やばいやばい！あと10秒！」");
            }
            if (TimeLeft <= 10f)
            {
                int sec = Mathf.CeilToInt(TimeLeft);
                if (sec != lastTickSecond) { lastTickSecond = sec; Sfx.Tick(); }
            }

            if (TimeLeft <= 0f) Finish();
        }

        void TickGimmicks(float dt)
        {
            for (int i = 0; i < level.gimmicks.Count; i++)
            {
                GimmickEvent g = level.gimmicks[i];
                // ずっと先の予告は出さない。逆回転とラッシュだけ2.5秒前に予告する
                if (!warned[i] && g.kind != GimmickKind.CheerTime && Elapsed >= g.time - 2.5f)
                {
                    warned[i] = true;
                    if (Elapsed < g.time)
                    {
                        Banner?.Invoke(g.kind == GimmickKind.ConveyorReverse ? "ベルトがあやしい…！" : "注文がふえそう…！", WarnColor);
                        Sfx.Warn();
                    }
                }
                if (!fired[i] && Elapsed >= g.time)
                {
                    fired[i] = true;
                    Fire(g);
                }
            }

            if (reverseTimer > 0f)
            {
                reverseTimer -= dt;
                if (reverseTimer <= 0f)
                {
                    SetBeltDirection(1);
                    Banner?.Invoke("ベルト復旧！", GoodColor);
                    Sfx.Complete();
                }
            }
            if (cheerTimer > 0f) cheerTimer -= dt;
        }

        void Fire(GimmickEvent g)
        {
            switch (g.kind)
            {
                case GimmickKind.ConveyorReverse:
                    reverseTimer = Mathf.Max(0.1f, g.duration);
                    SetBeltDirection(-1);
                    Banner?.Invoke("ベルト逆回転！", WarnColor);
                    Voice?.Invoke("「うわっ、ベルトが逆だ！」");
                    Sfx.Warn();
                    break;
                case GimmickKind.OrderRush:
                    orders.Rush(Mathf.Max(1, g.count));
                    Banner?.Invoke("注文ラッシュ！", WarnColor);
                    Voice?.Invoke("「注文がいっぱい来た！」");
                    Sfx.Warn();
                    break;
                case GimmickKind.CheerTime:
                    cheerTimer = Mathf.Max(0.1f, g.duration);
                    Banner?.Invoke("応援タイム！", CheerColor);
                    Voice?.Invoke(level.managerName + "「ラストスパート！いけるいける！」");
                    Sfx.Cheer();
                    break;
            }
        }

        static void SetBeltDirection(int direction)
        {
            for (int i = 0; i < ConveyorBelt.AllBelts.Count; i++)
                ConveyorBelt.AllBelts[i].SetDirection(direction);
        }

        /// <summary>出荷口に箱を届けたとき。注文に合えばスコア、合わなければ減点。</summary>
        public void Deliver(Item item, Vector3 worldPos)
        {
            if (State != GameState.Playing) return;
            Order order = orders.FindMatch(item.type, item.color);
            if (order != null)
            {
                int bonus = Mathf.RoundToInt(level.speedBonusMax * order.Ratio / 10f) * 10;
                int points = level.deliverBaseScore + bonus;
                Score += points;
                Delivered++;
                orders.Complete(order);
                Popup?.Invoke(worldPos, "+" + points, GoodColor);
                Voice?.Invoke("「" + NiceLines[UnityEngine.Random.Range(0, NiceLines.Length)] + "」");
                Sfx.Deliver();
            }
            else
            {
                Wrong++;
                Score = Mathf.Max(0, Score - level.wrongPenalty);
                Popup?.Invoke(worldPos, "注文とちがう… -" + level.wrongPenalty, BadColor);
                Sfx.Fail();
            }
        }

        void OnOrderMissed(Order order)
        {
            Missed++;
            Score = Mathf.Max(0, Score - level.missPenalty);
            Voice?.Invoke("「やばい、間に合わなかった…」");
            Sfx.Fail();
        }

        public void ShowPopup(Vector3 worldPos, string text, Color color)
        {
            Popup?.Invoke(worldPos, text, color);
        }

        void Finish()
        {
            TimeLeft = 0f;
            reverseTimer = cheerTimer = 0f;
            SetBeltDirection(1);
            if (Score > BestScore && !DebugAutoPlayer.Enabled) // 自動プレイの点はベストに残さない
            {
                BestScore = Score;
                NewBest = true;
                PlayerPrefs.SetInt(BestKey, BestScore);
                PlayerPrefs.Save();
            }
            Banner?.Invoke("おしまい！", Color.white);
            Sfx.Finish();
            SetState(GameState.Finished);
        }

        /// <summary>店長役のリザルトコメント。</summary>
        public string ManagerComment()
        {
            switch (Stars)
            {
                case 3: return "カンペキ！キミ、天才かも！？";
                case 2: return "いい感じ！次は★3をねらおう！";
                case 1: return "おつかれさま！もう一回やってみよ！";
                default: return "ドンマイ！段取りを考えてみよう！";
            }
        }

        public void Retry()
        {
            Time.timeScale = 1f;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.name);
        }

        void SetState(GameState next)
        {
            State = next;
            StateChanged?.Invoke(next);
        }
    }
}
