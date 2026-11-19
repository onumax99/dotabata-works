using UnityEngine;

namespace DotaBata
{
    /// <summary>
    /// 斜め上からの固定カメラ。縦画面でステージ全体が収まる距離を、画面の縦横比から計算する。
    /// 画面の上（注文・タイマー）と下（操作エリア）にUI用の余白を空ける。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class StageCamera : MonoBehaviour
    {
        [Range(30f, 80f)] public float pitch = 56f;
        [Range(15f, 60f)] public float fieldOfView = 32f;
        [Tooltip("ステージの左右に空ける余白（m）")]
        public float sideMargin = 0.55f;
        [Tooltip("画面上部のUIが使う割合")]
        [Range(0f, 0.4f)] public float topUiFraction = 0.19f;
        [Tooltip("画面下部の操作エリアが使う割合")]
        [Range(0f, 0.5f)] public float bottomUiFraction = 0.22f;
        [Tooltip("持ち場の高さの見込み（m）")]
        public float propHeight = 1.8f;

        Camera cam;
        Vector2 floorSize = new Vector2(7f, 10f);
        float lastAspect = -1f;

        void Awake() { cam = GetComponent<Camera>(); }

        public void Configure(Vector2 floor)
        {
            floorSize = floor;
            Apply();
        }

        void LateUpdate()
        {
            if (!Mathf.Approximately(cam.aspect, lastAspect)) Apply();
        }

        void Apply()
        {
            if (cam == null) cam = GetComponent<Camera>();
            lastAspect = cam.aspect;
            cam.fieldOfView = fieldOfView;

            float tanV = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * cam.aspect;
            float theta = pitch * Mathf.Deg2Rad;
            float sin = Mathf.Sin(theta);
            float cos = Mathf.Cos(theta);

            float halfW = floorSize.x * 0.5f + sideMargin;
            float halfD = floorSize.y * 0.5f;

            // 横：いちばん手前の辺が画面に収まる距離
            float distanceForWidth = halfW / tanH + halfD * cos;
            // 縦：床の奥行き＋持ち場の高さが、UIの余白を除いた範囲に収まる距離
            float usable = Mathf.Max(0.2f, 1f - topUiFraction - bottomUiFraction);
            float halfHeightOnView = halfD * sin + propHeight * 0.5f * cos;
            float distanceForHeight = halfHeightOnView / (tanV * usable);
            float distance = Mathf.Max(distanceForWidth, distanceForHeight);

            // UIの余白の差の分だけ、ステージを画面の上寄りに置く
            float shiftOnView = (bottomUiFraction - topUiFraction) * distance * tanV;
            Vector3 target = new Vector3(0f, 0f, -shiftOnView / sin);

            Quaternion rotation = Quaternion.Euler(pitch, 0f, 0f);
            transform.SetPositionAndRotation(target - rotation * Vector3.forward * distance, rotation);
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = distance + 60f;
        }
    }
}
