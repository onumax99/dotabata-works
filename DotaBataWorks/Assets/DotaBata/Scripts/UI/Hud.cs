using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DotaBata
{
    /// <summary>
    /// 縦画面のHUD。上に注文カード・タイマー・スコア、下に操作エリア（どこでもスティック＋2ボタン）、
    /// あいだにステージが見える。タイトルとリザルトもここで出す。
    /// レイアウトは 1080×1920 を基準に、コードで組み立てている。
    /// </summary>
    public class Hud : MonoBehaviour
    {
        static readonly Color Ink = new Color32(0x3B, 0x2F, 0x45, 0xFF);
        static readonly Color Cream = new Color32(0xFF, 0xFA, 0xF0, 0xFF);
        static readonly Color Pink = new Color32(0xFF, 0x6F, 0xA8, 0xFF);
        static readonly Color Mint = new Color32(0x3D, 0xC5, 0xAA, 0xFF);
        static readonly Color Sky = new Color32(0x4F, 0xB6, 0xF0, 0xFF);
        static readonly Color Orange = new Color32(0xFF, 0x9F, 0x43, 0xFF);
        static readonly Color Green = new Color32(0x4C, 0xC9, 0x7A, 0xFF);
        static readonly Color Red = new Color32(0xEF, 0x53, 0x50, 0xFF);
        static readonly Color Grey = new Color32(0x8E, 0x93, 0xA3, 0xFF);
        static readonly Color StarOn = new Color32(0xFF, 0xC1, 0x07, 0xFF);
        static readonly Color StarOff = new Color32(0xCF, 0xCB, 0xD6, 0xFF);

        const float CardWidth = 250f;
        const float CardHeight = 200f;
        const float CardGap = 12f;

        class OrderCard
        {
            public Order order;
            public RectTransform rect;
            public Image background;
            public RectTransform barFill;
            public Image barImage;
            public float appear;
        }

        class Follower
        {
            public RectTransform rect;
            public Func<Vector3> position;
            public Func<bool> visible;
        }

        GameManager game;
        Camera cam;
        RectTransform canvasRect;
        RectTransform worldLayer;
        RectTransform safe;

        RectTransform ordersRoot;
        readonly List<OrderCard> cards = new List<OrderCard>();
        readonly List<Follower> followers = new List<Follower>();

        Text timerText;
        Image timerBackground;
        Text scoreText;
        Text[] hudStars;

        RectTransform bannerRect;
        Text bannerText;
        float bannerTimer;
        const float BannerSeconds = 1.5f;

        CanvasGroup voiceGroup;
        RectTransform voiceRect;
        Text voiceText;
        float voiceTimer;

        GameObject controls;
        Image actionImage;
        Text actionLabel;
        Image dashImage;
        Text moveHint;

        GameObject titlePanel;
        GameObject resultPanel;
        Text[] resultStars;
        Text resultScore;
        Text resultBest;
        Text resultStats;
        Text resultComment;

        RectTransform guideArrow;
        RectTransform guideHalo;
        Interactable guideTarget;

        void Start()
        {
            game = GameManager.I;
            cam = Camera.main;
            if (game == null || cam == null)
            {
                Debug.LogError("[DotaBata] Hud には GameManager と MainCamera が必要です。");
                enabled = false;
                return;
            }

            EnsureEventSystem();
            BuildCanvas();
            BuildWorldLayer();
            BuildTopBar();
            BuildVoice();
            BuildControls();
            BuildBanner();
            BuildTitle();
            BuildResult();

            game.StateChanged += ApplyState;
            game.Banner += ShowBanner;
            game.Voice += ShowVoice;
            game.Popup += ShowPopup;
            OrderManager.I.OrderAdded += AddCard;
            OrderManager.I.OrderRemoved += RemoveCard;

            ApplyState(game.State);
        }

        void OnDestroy()
        {
            if (game != null)
            {
                game.StateChanged -= ApplyState;
                game.Banner -= ShowBanner;
                game.Voice -= ShowVoice;
                game.Popup -= ShowPopup;
            }
            if (OrderManager.I != null)
            {
                OrderManager.I.OrderAdded -= AddCard;
                OrderManager.I.OrderRemoved -= RemoveCard;
            }
        }

        // ------------------------------------------------------------------ 組み立て

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.transform.SetAsLastSibling();
        }

        void BuildCanvas()
        {
            var go = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasRect = (RectTransform)go.transform;

            worldLayer = UiKit.Stretch(UiKit.Rect("World", canvasRect));
            safe = UiKit.Stretch(UiKit.Rect("Safe", canvasRect));
            safe.gameObject.AddComponent<SafeAreaFitter>();
        }

        void BuildWorldLayer()
        {
            // 持ち場の名前ラベル
            var all = Interactable.All;
            for (int i = 0; i < all.Count; i++)
            {
                Interactable target = all[i];
                if (target is Item || string.IsNullOrEmpty(target.displayName)) continue;

                float width = 30f + target.displayName.Length * 27f;
                Image pill = UiKit.Image("Label_" + target.name, worldLayer, UiKit.Rounded, new Color(Ink.r, Ink.g, Ink.b, 0.62f));
                UiKit.Place(pill.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 42f));
                Text text = UiKit.Label("Text", pill.transform, target.displayName, 26, Color.white);
                UiKit.Stretch(text.rectTransform);
                followers.Add(new Follower { rect = pill.rectTransform, position = () => target.LabelPosition, visible = () => true });
            }

            // 作業の進み具合バー
            var stations = ProcessStation.AllStations;
            for (int i = 0; i < stations.Count; i++)
            {
                ProcessStation station = stations[i];
                Image back = UiKit.Image("Progress_" + station.name, worldLayer, UiKit.Rounded, new Color(Ink.r, Ink.g, Ink.b, 0.75f));
                UiKit.Place(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(128f, 26f));
                Image fill = UiKit.Image("Fill", back.transform, null, StarOn);
                RectTransform fillRect = fill.rectTransform;
                fillRect.anchorMin = new Vector2(0f, 0.5f);
                fillRect.anchorMax = new Vector2(0f, 0.5f);
                fillRect.pivot = new Vector2(0f, 0.5f);
                fillRect.anchoredPosition = new Vector2(5f, 0f);
                followers.Add(new Follower
                {
                    rect = back.rectTransform,
                    position = () =>
                    {
                        fillRect.sizeDelta = new Vector2(118f * station.Progress01, 16f);
                        return station.LabelPosition - Vector3.up * 0.42f;
                    },
                    visible = () => station.ShowProgress,
                });
            }

            // やさしいモードのガイド（次にやることが光る）
            Image halo = UiKit.Image("GuideHalo", worldLayer, UiKit.Circle, new Color(1f, 0.92f, 0.3f, 0.4f));
            guideHalo = UiKit.Place(halo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 170f));
            Text arrow = UiKit.Label("GuideArrow", worldLayer, "▼", 84, StarOn);
            UiKit.AddOutline(arrow, Ink, 4f);
            guideArrow = UiKit.Place(arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));
            guideHalo.gameObject.SetActive(false);
            guideArrow.gameObject.SetActive(false);
        }

        void BuildTopBar()
        {
            ordersRoot = UiKit.Rect("Orders", safe);
            ordersRoot.anchorMin = new Vector2(0f, 1f);
            ordersRoot.anchorMax = new Vector2(1f, 1f);
            ordersRoot.pivot = new Vector2(0.5f, 1f);
            ordersRoot.offsetMin = new Vector2(20f, -16f - CardHeight);
            ordersRoot.offsetMax = new Vector2(-20f, -16f);

            timerBackground = UiKit.Image("Timer", safe, UiKit.Rounded, Ink);
            UiKit.Place(timerBackground.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -232f), new Vector2(330f, 86f));
            timerText = UiKit.Label("Text", timerBackground.transform, "", 46, Color.white);
            UiKit.Stretch(timerText.rectTransform);

            Image scoreBackground = UiKit.Image("Score", safe, UiKit.Rounded, Cream);
            UiKit.Place(scoreBackground.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -232f), new Vector2(430f, 86f));
            hudStars = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                hudStars[i] = UiKit.Label("Star" + i, scoreBackground.transform, "★", 46, StarOff);
                UiKit.Place(hudStars[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(44f + i * 50f, 2f), new Vector2(50f, 60f));
            }
            scoreText = UiKit.Label("Text", scoreBackground.transform, "0てん", 46, Ink, TextAnchor.MiddleRight);
            UiKit.Stretch(scoreText.rectTransform, 170f, 0f, 26f, 0f);
        }

        void BuildVoice()
        {
            Image bubble = UiKit.Image("Voice", safe, UiKit.Rounded, new Color(1f, 1f, 1f, 0.95f));
            voiceRect = UiKit.Place(bubble.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -330f), new Vector2(400f, 76f));
            voiceGroup = bubble.gameObject.AddComponent<CanvasGroup>();
            voiceGroup.alpha = 0f;
            voiceGroup.blocksRaycasts = false;
            voiceText = UiKit.Label("Text", bubble.transform, "", 38, Ink);
            UiKit.Stretch(voiceText.rectTransform);
        }

        void BuildControls()
        {
            RectTransform root = UiKit.Stretch(UiKit.Rect("Controls", safe));
            controls = root.gameObject;

            // 画面下半分：どこでもドラッグで移動
            Image area = UiKit.Image("StickArea", root, null, new Color(0f, 0f, 0f, 0f), true);
            RectTransform areaRect = area.rectTransform;
            areaRect.anchorMin = new Vector2(0f, 0f);
            areaRect.anchorMax = new Vector2(1f, 0.56f);
            areaRect.offsetMin = areaRect.offsetMax = Vector2.zero;

            Image stickBase = UiKit.Image("StickBase", areaRect, UiKit.Ring, new Color(1f, 1f, 1f, 0.7f));
            UiKit.Place(stickBase.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280f, 280f));
            Image stickKnob = UiKit.Image("StickKnob", areaRect, UiKit.Circle, new Color(1f, 1f, 1f, 0.9f));
            UiKit.Place(stickKnob.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));

            var stick = area.gameObject.AddComponent<VirtualStickArea>();
            stick.baseRect = stickBase.rectTransform;
            stick.knobRect = stickKnob.rectTransform;
            stick.radius = 120f;
            stickBase.gameObject.SetActive(false);
            stickKnob.gameObject.SetActive(false);

            moveHint = UiKit.Label("MoveHint", root, "このへんをドラッグで移動", 34, new Color(1f, 1f, 1f, 0.9f));
            UiKit.AddOutline(moveHint, new Color(Ink.r, Ink.g, Ink.b, 0.8f), 2f);
            UiKit.Place(moveHint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(60f, 210f), new Vector2(480f, 60f));
            moveHint.alignment = TextAnchor.MiddleLeft;

            // ダッシュボタン
            dashImage = UiKit.Image("DashButton", root, UiKit.Circle, Sky, true);
            UiKit.Place(dashImage.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-440f, 140f), new Vector2(190f, 190f));
            dashImage.gameObject.AddComponent<HoldButton>().isDash = true;
            Text dashLabel = UiKit.Label("Label", dashImage.transform, "ダッシュ", 36, Color.white);
            UiKit.AddOutline(dashLabel, new Color(Ink.r, Ink.g, Ink.b, 0.6f), 2f);
            UiKit.Stretch(dashLabel.rectTransform);

            // アクションボタン（近くのものに合わせて文字が変わる）
            actionImage = UiKit.Image("ActionButton", root, UiKit.Circle, Grey, true);
            UiKit.Place(actionImage.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-185f, 240f), new Vector2(290f, 290f));
            actionImage.gameObject.AddComponent<HoldButton>().isDash = false;
            actionLabel = UiKit.Label("Label", actionImage.transform, "", 66, Color.white);
            UiKit.AddOutline(actionLabel, new Color(Ink.r, Ink.g, Ink.b, 0.6f), 3f);
            UiKit.Stretch(actionLabel.rectTransform);
        }

        void BuildBanner()
        {
            bannerText = UiKit.Label("Banner", safe, "", 124, Color.white);
            UiKit.AddOutline(bannerText, Ink, 6f);
            bannerRect = UiKit.Place(bannerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1000f, 220f));
            bannerText.gameObject.SetActive(false);
        }

        void BuildTitle()
        {
            Image dim = UiKit.Image("Title", canvasRect, null, new Color(0.12f, 0.08f, 0.16f, 0.66f), true);
            UiKit.Stretch(dim.rectTransform);
            titlePanel = dim.gameObject;

            Image card = UiKit.Image("Card", dim.transform, UiKit.Rounded, Cream);
            card.pixelsPerUnitMultiplier = 0.5f;
            UiKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(960f, 1380f));
            Transform c = card.transform;

            Put(UiKit.Label("Name", c, "ドタバタワークス", 100, Pink), 0f, 560f, 900f, 130f);
            Put(UiKit.Label("Sub", c, "（仮称）プロトタイプ", 36, Grey), 0f, 468f, 900f, 50f);

            Image stage = UiKit.Image("Stage", c, UiKit.Rounded, StarOn);
            Put(stage, 0f, 370f, 640f, 92f);
            Text stageText = UiKit.Label("Text", stage.transform, game.level.themeName + "  " + game.level.stageName, 50, Ink);
            UiKit.Stretch(stageText.rectTransform);

            Text rules = UiKit.Label("Rules", c,
                "① 材料棚からパーツを取る\n② 組み立て台で長押しして組む\n③ 注文の色の色塗りマシンに入れる\n④ ベルトで届いたら梱包台で箱づめ\n⑤ 出荷口へ届ける！",
                40, Ink, TextAnchor.MiddleLeft);
            rules.lineSpacing = 1.2f;
            Put(rules, 0f, 110f, 760f, 340f);

            Text how = UiKit.Label("How", c,
                "画面の下半分をドラッグで移動\n右下のボタンでアクション／ダッシュ\n（PC：WASD・スペース・Shift）",
                30, Grey, TextAnchor.MiddleCenter, FontStyle.Normal);
            how.lineSpacing = 1.15f;
            Put(how, 0f, -170f, 860f, 150f);

            Button normal = UiKit.Button("Normal", c, "ふつう", 64, Pink, Color.white, out _);
            Put(normal.image, -225f, -370f, 410f, 160f);
            normal.onClick.AddListener(() => game.StartGame(false));
            Put(UiKit.Label("NormalBest", c, "ベスト " + game.GetBestScore(false) + "てん", 30, Grey), -225f, -480f, 400f, 44f);

            Button easy = UiKit.Button("Easy", c, "やさしい", 64, Mint, Color.white, out _);
            Put(easy.image, 225f, -370f, 410f, 160f);
            easy.onClick.AddListener(() => game.StartGame(true));
            Put(UiKit.Label("EasyBest", c, "ベスト " + game.GetBestScore(true) + "てん", 30, Grey), 225f, -480f, 400f, 44f);
            Put(UiKit.Label("EasyNote", c, "時間が長い＋次にやることが光る", 26, Mint, TextAnchor.MiddleCenter, FontStyle.Normal), 225f, -524f, 440f, 40f);

            Put(UiKit.Label("Time", c, "制限時間 " + Mathf.RoundToInt(game.level.timeLimit) + "秒", 30, Grey, TextAnchor.MiddleCenter, FontStyle.Normal), 0f, -610f, 600f, 44f);
        }

        void BuildResult()
        {
            Image dim = UiKit.Image("Result", canvasRect, null, new Color(0.12f, 0.08f, 0.16f, 0.66f), true);
            UiKit.Stretch(dim.rectTransform);
            resultPanel = dim.gameObject;

            Image card = UiKit.Image("Card", dim.transform, UiKit.Rounded, Cream);
            card.pixelsPerUnitMultiplier = 0.5f;
            UiKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(920f, 1240f));
            Transform c = card.transform;

            Put(UiKit.Label("Head", c, "けっか", 72, Ink), 0f, 520f, 800f, 100f);

            resultStars = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                resultStars[i] = UiKit.Label("Star" + i, c, "★", 200, StarOff);
                Put(resultStars[i], (i - 1) * 230f, i == 1 ? 350f : 320f, 220f, 220f);
            }

            resultScore = UiKit.Label("Score", c, "0てん", 130, Pink);
            Put(resultScore, 0f, 110f, 860f, 160f);
            resultBest = UiKit.Label("Best", c, "", 44, Orange);
            Put(resultBest, 0f, -5f, 860f, 60f);
            resultStats = UiKit.Label("Stats", c, "", 36, Grey, TextAnchor.MiddleCenter, FontStyle.Normal);
            Put(resultStats, 0f, -80f, 860f, 50f);

            Image bubble = UiKit.Image("Comment", c, UiKit.Rounded, new Color32(0xFF, 0xEE, 0xB8, 0xFF));
            Put(bubble, 0f, -240f, 800f, 190f);
            resultComment = UiKit.Label("Text", bubble.transform, "", 40, Ink);
            resultComment.lineSpacing = 1.15f;
            UiKit.Stretch(resultComment.rectTransform, 20f, 10f, 20f, 10f);

            Button retry = UiKit.Button("Retry", c, "もう一回", 64, Pink, Color.white, out _);
            Put(retry.image, 0f, -470f, 540f, 160f);
            retry.onClick.AddListener(() => game.Retry());

            resultPanel.SetActive(false);
        }

        /// <summary>カードの中心からの位置で置く。</summary>
        static void Put(Graphic graphic, float x, float y, float width, float height)
        {
            UiKit.Place(graphic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(width, height));
        }

        // ------------------------------------------------------------------ 状態の切り替え

        void ApplyState(GameState state)
        {
            titlePanel.SetActive(state == GameState.Title);
            controls.SetActive(state == GameState.Countdown || state == GameState.Playing);
            if (state == GameState.Finished) StartCoroutine(ShowResultRoutine());
        }

        IEnumerator ShowResultRoutine()
        {
            yield return new WaitForSecondsRealtime(1.3f);

            resultPanel.SetActive(true);
            int stars = game.Stars;
            resultScore.text = game.Score + "てん";
            resultBest.text = game.NewBest ? "ベスト更新！" : "ベスト " + game.BestScore + "てん";
            resultStats.text = "出荷 " + game.Delivered + "こ ／ 間に合わず " + game.Missed + " ／ まちがい " + game.Wrong;
            resultComment.text = game.level.managerName + "\n「" + game.ManagerComment() + "」";

            for (int i = 0; i < 3; i++) resultStars[i].color = StarOff;
            for (int i = 0; i < stars; i++)
            {
                yield return new WaitForSecondsRealtime(0.3f);
                resultStars[i].color = StarOn;
                Sfx.Complete();
            }
        }

        // ------------------------------------------------------------------ 注文カード

        void AddCard(Order order)
        {
            Image background = UiKit.Image("Order" + order.id, ordersRoot, UiKit.Rounded, Color.white);
            RectTransform rect = background.rectTransform;
            UiKit.Place(rect, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(cards.Count * (CardWidth + CardGap) + CardWidth * 0.5f + 300f, -CardHeight * 0.5f), new Vector2(CardWidth, CardHeight));

            Color paint = GameColors.Of(order.color);
            if (order.type == ProductType.AcrylicStand)
            {
                Put(UiKit.Image("Outline", rect, UiKit.Rounded, Ink), -62f, 28f, 76f, 106f);
                Put(UiKit.Image("Plate", rect, UiKit.Rounded, paint), -62f, 28f, 62f, 92f);
                Put(UiKit.Image("Base", rect, UiKit.Rounded, Ink), -62f, -34f, 104f, 24f);
            }
            else
            {
                Put(UiKit.Image("Outline", rect, UiKit.Circle, Ink), -62f, 16f, 112f, 112f);
                Put(UiKit.Image("Face", rect, UiKit.Circle, paint), -62f, 16f, 96f, 96f);
                Put(UiKit.Image("Shine", rect, UiKit.Circle, new Color(1f, 1f, 1f, 0.55f)), -80f, 36f, 24f, 24f);
            }

            Put(UiKit.Label("Type", rect, GameText.Name(order.type), 32, Ink), 60f, 40f, 130f, 44f);
            Image chip = UiKit.Image("Chip", rect, UiKit.Rounded, paint);
            Put(chip, 60f, -8f, 124f, 42f);
            Text chipText = UiKit.Label("Text", chip.transform, GameText.Name(order.color), 26, Ink);
            UiKit.Stretch(chipText.rectTransform);

            Image barBack = UiKit.Image("Bar", rect, UiKit.Rounded, new Color32(0xE4, 0xE0, 0xEA, 0xFF));
            Put(barBack, 0f, -76f, 222f, 22f);
            Image barFill = UiKit.Image("Fill", barBack.transform, null, Green);
            RectTransform fillRect = barFill.rectTransform;
            fillRect.anchorMin = fillRect.anchorMax = new Vector2(0f, 0.5f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = new Vector2(4f, 0f);
            fillRect.sizeDelta = new Vector2(214f, 14f);

            cards.Add(new OrderCard { order = order, rect = rect, background = background, barFill = fillRect, barImage = barFill });
        }

        void RemoveCard(Order order, bool delivered)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].order != order) continue;
                OrderCard card = cards[i];
                cards.RemoveAt(i);
                StartCoroutine(RemoveCardRoutine(card, delivered));
                return;
            }
        }

        IEnumerator RemoveCardRoutine(OrderCard card, bool delivered)
        {
            card.background.color = delivered ? new Color32(0xC8, 0xF2, 0xD4, 0xFF) : new Color32(0xFB, 0xC9, 0xC8, 0xFF);
            const float seconds = 0.35f;
            for (float time = 0f; time < seconds; time += Time.unscaledDeltaTime)
            {
                if (card.rect == null) yield break;
                float k = time / seconds;
                card.rect.localScale = Vector3.one * (delivered ? 1f + 0.25f * Mathf.Sin(k * Mathf.PI) - k * 0.9f : 1f - k);
                yield return null;
            }
            if (card.rect != null) Destroy(card.rect.gameObject);
        }

        void UpdateCards(float dt)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                OrderCard card = cards[i];
                float ratio = card.order.Ratio;
                card.barFill.sizeDelta = new Vector2(214f * ratio, 14f);
                card.barImage.color = ratio > 0.5f ? Green : (ratio > 0.25f ? Orange : Red);

                Vector2 target = new Vector2(i * (CardWidth + CardGap) + CardWidth * 0.5f, -CardHeight * 0.5f);
                if (ratio <= 0.25f) target.x += Mathf.Sin(Time.unscaledTime * 40f) * 4f; // もうすぐ時間切れ：ふるえる
                card.rect.anchoredPosition = Vector2.Lerp(card.rect.anchoredPosition, target, 1f - Mathf.Exp(-14f * dt));

                card.appear = Mathf.Min(1f, card.appear + dt * 6f);
                card.rect.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, card.appear);
            }
        }

        // ------------------------------------------------------------------ 告知・セリフ・ポップアップ

        void ShowBanner(string text, Color color)
        {
            bannerText.text = text;
            bannerText.fontSize = Mathf.Min(124, 940 / Mathf.Max(1, text.Length)); // 長い告知は画面幅に収まる大きさにする
            bannerText.color = color;
            bannerTimer = BannerSeconds;
            bannerText.gameObject.SetActive(true);
        }

        void ShowVoice(string text)
        {
            voiceText.text = text;
            voiceTimer = 2.4f;
            // ふきだしは文字数に合わせた幅にして、ステージを隠しすぎないようにする
            voiceRect.sizeDelta = new Vector2(Mathf.Clamp(70f + text.Length * 40f, 240f, 1040f), 76f);
        }

        void ShowPopup(Vector3 worldPosition, string text, Color color)
        {
            StartCoroutine(PopupRoutine(worldPosition, text, color));
        }

        IEnumerator PopupRoutine(Vector3 worldPosition, string text, Color color)
        {
            Text label = UiKit.Label("Popup", worldLayer, text, 48, color);
            UiKit.AddOutline(label, Ink, 3f);
            RectTransform rect = UiKit.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 70f));
            const float seconds = 0.95f;
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                float k = time / seconds;
                rect.anchoredPosition = WorldToCanvas(worldPosition) + new Vector2(0f, 30f + 90f * k);
                Color c = color;
                c.a = Mathf.Clamp01((1f - k) * 2.5f);
                label.color = c;
                yield return null;
            }
            Destroy(label.gameObject);
        }

        Vector2 WorldToCanvas(Vector3 world)
        {
            Vector3 screen = cam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(worldLayer, screen, null, out Vector2 local);
            return local;
        }

        // ------------------------------------------------------------------ 毎フレーム

        void Update()
        {
            if (game == null) return;
            float dt = Time.unscaledDeltaTime;

            // タイマーとスコア
            float timeLeft = Mathf.Max(0f, game.TimeLeft);
            int seconds = Mathf.CeilToInt(timeLeft);
            timerText.text = "のこり " + (seconds / 60) + ":" + (seconds % 60).ToString("00");
            bool hurry = game.IsPlaying && timeLeft <= 10f;
            timerBackground.color = game.CheerActive ? Pink : (hurry ? Red : Ink);
            scoreText.text = game.Score + "てん";
            int stars = game.Stars;
            for (int i = 0; i < hudStars.Length; i++) hudStars[i].color = i < stars ? StarOn : StarOff;

            UpdateCards(dt);
            UpdateActionButton();

            // 告知
            if (bannerTimer > 0f)
            {
                bannerTimer -= dt;
                float age = BannerSeconds - bannerTimer;
                float pop = 1f + 0.5f * Mathf.Clamp01(1f - age / 0.14f);
                bannerRect.localScale = Vector3.one * pop;
                Color c = bannerText.color;
                c.a = Mathf.Clamp01(bannerTimer / 0.3f);
                bannerText.color = c;
                if (bannerTimer <= 0f) bannerText.gameObject.SetActive(false);
            }

            // セリフ
            if (voiceTimer > 0f) voiceTimer -= dt;
            float voiceTarget = voiceTimer > 0f && game.State != GameState.Title ? 1f : 0f;
            voiceGroup.alpha = Mathf.MoveTowards(voiceGroup.alpha, voiceTarget, dt * 6f);

            // やさしいモードのガイド
            guideTarget = game.IsPlaying && game.EasyMode ? GuideSystem.Suggest(game.Player) : null;
        }

        void UpdateActionButton()
        {
            PlayerController player = game.Player;
            if (player == null || !controls.activeSelf) return;

            ActionKind action = player.CurrentAction;
            string label = GameText.Label(action);
            Color color;
            switch (action)
            {
                case ActionKind.Take: color = Sky; break;
                case ActionKind.Put: color = Green; break;
                case ActionKind.Work: color = Orange; break;
                case ActionKind.Ship: color = Pink; break;
                case ActionKind.Trash: color = Grey; break;
                default:
                    if (player.Held != null) { label = "おとす"; color = Grey; }
                    else { label = ""; color = new Color(Grey.r, Grey.g, Grey.b, 0.55f); }
                    break;
            }
            actionLabel.text = label;
            actionImage.color = color;
            dashImage.color = player.DashReady ? Sky : new Color(Sky.r, Sky.g, Sky.b, 0.5f);

            if (moveHint.gameObject.activeSelf && GameInput.I != null && GameInput.I.uiStick.sqrMagnitude > 0f)
                moveHint.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (game == null || cam == null) return;

            for (int i = 0; i < followers.Count; i++)
            {
                Follower f = followers[i];
                bool visible = f.visible();
                if (f.rect.gameObject.activeSelf != visible) f.rect.gameObject.SetActive(visible);
                if (visible) f.rect.anchoredPosition = WorldToCanvas(f.position());
            }

            bool showGuide = guideTarget != null;
            if (guideArrow.gameObject.activeSelf != showGuide)
            {
                guideArrow.gameObject.SetActive(showGuide);
                guideHalo.gameObject.SetActive(showGuide);
            }
            if (showGuide)
            {
                Vector2 point = WorldToCanvas(guideTarget.GetGuidePosition(game.Player));
                float wave = Mathf.Sin(Time.unscaledTime * 7f);
                guideArrow.anchoredPosition = point + new Vector2(0f, 62f + wave * 12f);
                guideHalo.anchoredPosition = point + new Vector2(0f, -30f);
                guideHalo.localScale = Vector3.one * (1f + 0.12f * wave);
            }
        }
    }
}
