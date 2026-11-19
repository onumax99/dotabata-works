using UnityEngine;
using UnityEngine.EventSystems;

namespace DotaBata
{
    /// <summary>
    /// 画面下半分のどこでもドラッグで移動。触った場所がスティックの中心になる。
    /// </summary>
    public class VirtualStickArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        public RectTransform baseRect;
        public RectTransform knobRect;
        [Tooltip("スティックを倒しきるまでの距離（キャンバス上の長さ）")]
        public float radius = 120f;
        [Range(0f, 0.5f)] public float deadZone = 0.15f;

        const int NoPointer = int.MinValue;
        RectTransform area;
        int pointerId = NoPointer;
        Vector2 origin;

        void Awake()
        {
            area = (RectTransform)transform;
            Show(false);
        }

        void OnDisable() { ReleaseStick(); }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            eventData.useDragThreshold = false; // 触った瞬間から動けるようにする
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerId != NoPointer) return;
            if (!ToLocal(eventData, out origin)) return;
            pointerId = eventData.pointerId;
            baseRect.anchoredPosition = origin;
            knobRect.anchoredPosition = origin;
            Show(true);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != pointerId) return;
            if (!ToLocal(eventData, out Vector2 point)) return;

            Vector2 delta = point - origin;
            if (delta.magnitude > radius)
            {
                // 指が遠くへ行ったら、中心も引っぱっていく
                origin = point - delta.normalized * radius;
                baseRect.anchoredPosition = origin;
                delta = point - origin;
            }
            knobRect.anchoredPosition = origin + delta;

            Vector2 value = delta / radius;
            if (GameInput.I != null) GameInput.I.uiStick = value.magnitude < deadZone ? Vector2.zero : value;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != pointerId) return;
            ReleaseStick();
        }

        void ReleaseStick()
        {
            pointerId = NoPointer;
            if (GameInput.I != null) GameInput.I.uiStick = Vector2.zero;
            Show(false);
        }

        void Show(bool on)
        {
            if (baseRect != null) baseRect.gameObject.SetActive(on);
            if (knobRect != null) knobRect.gameObject.SetActive(on);
        }

        bool ToLocal(PointerEventData eventData, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, eventData.pressEventCamera, out local);
        }
    }

    /// <summary>アクションボタン・ダッシュボタン。押した瞬間と、押している間を GameInput に伝える。</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public bool isDash;

        const int NoPointer = int.MinValue;
        int pointerId = NoPointer;

        void OnDisable() { Release(); }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerId != NoPointer || GameInput.I == null) return;
            pointerId = eventData.pointerId;
            if (isDash)
            {
                GameInput.I.PressDash();
            }
            else
            {
                GameInput.I.PressAction();
                GameInput.I.uiActionHeld = true;
            }
            transform.localScale = Vector3.one * 0.92f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != pointerId) return;
            Release();
        }

        void Release()
        {
            pointerId = NoPointer;
            if (!isDash && GameInput.I != null) GameInput.I.uiActionHeld = false;
            transform.localScale = Vector3.one;
        }
    }

    /// <summary>ノッチやホームバーを避けて、端末の安全な範囲に収める。</summary>
    public class SafeAreaFitter : MonoBehaviour
    {
        Rect applied;

        void Update()
        {
            Rect safe = Screen.safeArea;
            if (safe == applied || Screen.width <= 0 || Screen.height <= 0) return;
            applied = safe;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
