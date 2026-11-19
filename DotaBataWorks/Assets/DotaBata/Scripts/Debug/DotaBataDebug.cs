using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DotaBata
{
    /// <summary>動作確認用の入口。再生中にエディタから呼ぶ。</summary>
    public static class DotaBataDebug
    {
        static Touchscreen virtualTouch;
        static int nextTouchId = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { virtualTouch = null; nextTouchId = 1; }

        /// <summary>
        /// 指のタッチをまねる（画面のピクセル座標、左下が原点）。
        /// (x0,y0) に触れ、dragSeconds かけて (x1,y1) へ動かし、holdSeconds そのまま押さえてから離す。
        /// 実機と同じ経路（Input System → EventSystem → ボタンやスティック）を通る。
        /// </summary>
        public static string Touch(float x0, float y0, float x1, float y1, float dragSeconds, float holdSeconds)
        {
            if (GameManager.I == null) return "GameManager がありません（再生中に呼んでください）";
            GameManager.I.StartCoroutine(TouchRoutine(new Vector2(x0, y0), new Vector2(x1, y1), dragSeconds, holdSeconds));
            return "touch queued";
        }

        static IEnumerator TouchRoutine(Vector2 from, Vector2 to, float dragSeconds, float holdSeconds)
        {
            if (virtualTouch == null || !virtualTouch.added) virtualTouch = InputSystem.AddDevice<Touchscreen>("DotaBataDebugTouch");
            int id = nextTouchId++;

            InputSystem.QueueStateEvent(virtualTouch, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Began, position = from });
            yield return null;

            Vector2 last = from;
            for (float time = 0f; time < dragSeconds; time += Time.unscaledDeltaTime)
            {
                Vector2 point = Vector2.Lerp(from, to, time / dragSeconds);
                InputSystem.QueueStateEvent(virtualTouch, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Moved, position = point, delta = point - last });
                last = point;
                yield return null;
            }
            InputSystem.QueueStateEvent(virtualTouch, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Moved, position = to, delta = to - last });
            yield return null;

            for (float time = 0f; time < holdSeconds; time += Time.unscaledDeltaTime)
            {
                InputSystem.QueueStateEvent(virtualTouch, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Stationary, position = to });
                yield return null;
            }

            InputSystem.QueueStateEvent(virtualTouch, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = to });
        }

        /// <summary>エディタが裏に回っていても再生を止めない（確認用。再生を止めると元に戻す）。</summary>
        public static string KeepRunning()
        {
            Application.runInBackground = true;
            return "runInBackground=" + Application.runInBackground + " focused=" + Application.isFocused;
        }

        /// <summary>タイトルを飛ばして、自動プレイで始める。</summary>
        public static string StartAuto(bool easy, float timeScale)
        {
            if (GameManager.I == null) return "GameManager がありません（再生中に呼んでください）";
            DebugAutoPlayer.Enabled = true;
            Time.timeScale = Mathf.Clamp(timeScale, 0.1f, 8f);
            GameManager.I.StartGame(easy);
            return "auto started: easy=" + easy + " timeScale=" + Time.timeScale;
        }

        /// <summary>人が操作する状態で始める（自動プレイなし）。</summary>
        public static string StartManual(bool easy)
        {
            if (GameManager.I == null) return "GameManager がありません（再生中に呼んでください）";
            DebugAutoPlayer.Enabled = false;
            Time.timeScale = 1f;
            GameManager.I.StartGame(easy);
            return "manual started: easy=" + easy;
        }

        public static string SetTimeScale(float timeScale)
        {
            Time.timeScale = Mathf.Clamp(timeScale, 0f, 8f);
            return "timeScale=" + Time.timeScale;
        }

        /// <summary>今の状況を1行で返す。</summary>
        public static string Report()
        {
            GameManager g = GameManager.I;
            if (g == null) return "GameManager がありません";
            var sb = new StringBuilder();
            sb.Append("state=").Append(g.State)
              .Append(" easy=").Append(g.EasyMode)
              .Append(" elapsed=").Append(g.Elapsed.ToString("F1"))
              .Append(" left=").Append(g.TimeLeft.ToString("F1"))
              .Append(" score=").Append(g.Score)
              .Append(" stars=").Append(g.Stars)
              .Append(" delivered=").Append(g.Delivered)
              .Append(" missed=").Append(g.Missed)
              .Append(" wrong=").Append(g.Wrong)
              .Append(" cheer=").Append(g.CheerActive)
              .Append(" reverse=").Append(g.ReverseActive);
            if (OrderManager.I != null)
            {
                sb.Append(" orders=[");
                for (int i = 0; i < OrderManager.I.Active.Count; i++)
                {
                    Order o = OrderManager.I.Active[i];
                    if (i > 0) sb.Append(", ");
                    sb.Append(o.type).Append('/').Append(o.color).Append(' ').Append(o.remaining.ToString("F0")).Append('s');
                }
                sb.Append(']');
            }
            sb.Append(" items=").Append(Item.AllItems.Count);
            PlayerController p = g.Player;
            if (p != null)
            {
                sb.Append(" pos=").Append(p.transform.position.ToString("F1"));
                sb.Append(" held=").Append(p.Held != null ? p.Held.type + "/" + p.Held.stage + "/" + p.Held.color : "none");
                sb.Append(" focus=").Append(p.Focused != null ? p.Focused.name : "none");
                sb.Append(" action=").Append(p.CurrentAction);
                Interactable next = g.IsPlaying ? GuideSystem.Suggest(p) : null;
                sb.Append(" guide=").Append(next != null ? next.name : "none");
            }
            sb.Append(" screen=").Append(Screen.width).Append('x').Append(Screen.height);
            return sb.ToString();
        }

        /// <summary>ゲーム画面（UI込み）をPNGで保存する。</summary>
        public static string Capture(string path)
        {
            ScreenCapture.CaptureScreenshot(path);
            return path;
        }
    }
}
