using UnityEngine;

namespace DotaBata
{
    /// <summary>
    /// LevelData の「部品の配置」からステージを組み立てる。
    /// 床と壁はここで作り、持ち場はプレハブを並べるだけ。
    /// </summary>
    public class StageBuilder : MonoBehaviour
    {
        public static StageBuilder I { get; private set; }

        public GameObject playerPrefab;
        public GameObject itemPrefab;
        public float wallHeight = 0.7f;
        public float frontWallHeight = 0.3f;
        public float wallThickness = 0.3f;

        Transform root;
        Transform itemsRoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        public PlayerController Build(LevelData level)
        {
            I = this;
            root = new GameObject("Stage").transform;
            itemsRoot = new GameObject("Items").transform;
            itemsRoot.SetParent(root, false);

            float w = level.floorSize.x;
            float d = level.floorSize.y;
            float t = wallThickness;

            // 床（1マス1mの市松模様になるようにタイリングする）
            GameObject floor = Box("Floor", new Vector3(0f, -0.2f, 0f), new Vector3(w, 0.4f, d), level.floorMaterial);
            if (level.floorMaterial != null && level.floorMaterial.HasProperty("_BaseMap"))
            {
                var material = new Material(level.floorMaterial);
                material.SetTextureScale("_BaseMap", new Vector2(w * 0.5f, d * 0.5f));
                floor.GetComponent<Renderer>().sharedMaterial = material;
            }

            // 壁。手前（カメラ側）だけ低くして、持ち場が隠れないようにする
            Box("Wall_Left", new Vector3(-w * 0.5f - t * 0.5f, wallHeight * 0.5f, 0f), new Vector3(t, wallHeight, d + t * 2f), level.wallMaterial);
            Box("Wall_Right", new Vector3(w * 0.5f + t * 0.5f, wallHeight * 0.5f, 0f), new Vector3(t, wallHeight, d + t * 2f), level.wallMaterial);
            Box("Wall_Back", new Vector3(0f, wallHeight * 0.5f, d * 0.5f + t * 0.5f), new Vector3(w, wallHeight, t), level.wallMaterial);
            Box("Wall_Front", new Vector3(0f, frontWallHeight * 0.5f, -d * 0.5f - t * 0.5f), new Vector3(w, frontWallHeight, t), level.wallMaterial);

            for (int i = 0; i < level.stations.Count; i++)
            {
                StationPlacement p = level.stations[i];
                if (p.prefab == null) continue;
                GameObject go = Instantiate(p.prefab, p.position, Quaternion.Euler(0f, p.rotationY, 0f), root);
                go.name = string.IsNullOrEmpty(p.note) ? p.prefab.name : p.note;
                if (p.length > 0f)
                {
                    ConveyorBelt belt = go.GetComponent<ConveyorBelt>();
                    if (belt != null) belt.length = p.length;
                }
            }

            GameObject playerObject = Instantiate(playerPrefab, level.playerStart, Quaternion.Euler(0f, 180f, 0f));
            playerObject.name = "Player";
            return playerObject.GetComponent<PlayerController>();
        }

        public Item SpawnItem(ProductType type, ItemStage stage, PaintColor color)
        {
            GameObject go = Instantiate(itemPrefab, itemsRoot);
            Item item = go.GetComponent<Item>();
            item.Setup(type, stage, color);
            return item;
        }

        public Transform ItemsRoot => itemsRoot;

        GameObject Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
    }
}
