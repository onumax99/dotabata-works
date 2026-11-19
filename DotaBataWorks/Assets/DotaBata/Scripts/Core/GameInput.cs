using UnityEngine;
using UnityEngine.InputSystem;

namespace DotaBata
{
    /// <summary>
    /// 入力のまとめ役。タッチUI・キーボード・ゲームパッド・自動プレイの入力を1つにして、
    /// プレイヤーは「移動」「アクション」「ダッシュ」だけを見る。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class GameInput : MonoBehaviour
    {
        public static GameInput I { get; private set; }

        /// <summary>タッチのスティック（VirtualStickArea が書き込む）。</summary>
        [HideInInspector] public Vector2 uiStick;
        /// <summary>アクションボタンを押している間 true（HoldButton が書き込む）。</summary>
        [HideInInspector] public bool uiActionHeld;

        public Vector2 Move { get; private set; }
        public bool ActionDown { get; private set; }
        public bool ActionHeld { get; private set; }
        public bool DashDown { get; private set; }

        bool actionPending;
        bool dashPending;
        Vector2 botMove;
        bool botHeld;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        void Awake() { I = this; }

        public void PressAction() { actionPending = true; }
        public void PressDash() { dashPending = true; }

        public void SetBot(Vector2 move, bool held) { botMove = move; botHeld = held; }

        void Update()
        {
            Vector2 move = uiStick + botMove;
            bool held = uiActionHeld || botHeld;

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.spaceKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame) actionPending = true;
                if (kb.spaceKey.isPressed || kb.jKey.isPressed) held = true;
                if (kb.leftShiftKey.wasPressedThisFrame || kb.kKey.wasPressedThisFrame) dashPending = true;
            }

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.04f) move += stick;
                if (pad.buttonSouth.wasPressedThisFrame) actionPending = true;
                if (pad.buttonSouth.isPressed) held = true;
                if (pad.buttonEast.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame) dashPending = true;
            }

            Move = Vector2.ClampMagnitude(move, 1f);
            ActionDown = actionPending;
            ActionHeld = held;
            DashDown = dashPending;
            actionPending = false;
            dashPending = false;
        }
    }
}
