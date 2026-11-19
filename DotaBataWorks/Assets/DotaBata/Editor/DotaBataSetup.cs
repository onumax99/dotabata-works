using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DotaBata.EditorTools
{
    /// <summary>
    /// プロトタイプの素材一式（マテリアル・プレハブ・ステージデータ・シーン）をコードから作る。
    /// メニュー「DotaBata」から実行できる。作り直すと、同じ名前のアセットは上書きされる。
    /// 見た目はすべて仮（Unityのプリミティブ）。本制作ではプレハブの中身をモデルに差し替える。
    /// </summary>
    public static class DotaBataSetup
    {
        const string Root = "Assets/DotaBata";
        const string MatDir = Root + "/Materials";
        const string PrefabDir = Root + "/Prefabs";
        const string DataDir = Root + "/Data";
        const string SceneDir = Root + "/Scenes";
        const string TexDir = Root + "/Textures";
        public const string ScenePath = SceneDir + "/GoodsFactory.unity";
        public const string LevelPath = DataDir + "/GoodsFactory_01.asset";

        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
        static Shader shader;
        static bool usingToon;

        // ------------------------------------------------------------------ メニュー

        [MenuItem("DotaBata/プロトタイプを作り直す（アセットとシーンを上書き）")]
        public static void RebuildAllMenu() { Debug.Log("[DotaBata] " + RebuildAll()); }

        [MenuItem("DotaBata/グッズ工場シーンを開く")]
        public static void OpenSceneMenu() { Debug.Log("[DotaBata] " + OpenScene()); }

        [MenuItem("DotaBata/ゲームビューを縦画面（1080×1920）にする")]
        public static void PortraitMenu() { Debug.Log("[DotaBata] " + SetPortraitGameView()); }

        public static string OpenScene()
        {
            if (EditorApplication.isPlaying) return "再生を止めてから実行してください。";
            if (!File.Exists(ScenePath)) return "シーンがまだありません。先に「プロトタイプを作り直す」を実行してください。";
            if (SceneManager.GetActiveScene().isDirty) return "開いているシーンに未保存の変更があります。保存してから実行してください。";
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return "opened " + ScenePath;
        }

        public static string SetPortraitGameView()
        {
            try
            {
                PlayModeWindow.SetCustomRenderingResolution(1080, 1920, "DotaBata 1080x1920");
                return "game view = 1080x1920";
            }
            catch (System.Exception e)
            {
                return "ゲームビューの解像度を変えられませんでした: " + e.Message;
            }
        }

        public static string RebuildAll()
        {
            if (EditorApplication.isPlaying) return "再生を止めてから実行してください。";
            Scene active = SceneManager.GetActiveScene();
            if (active.isDirty && active.path != ScenePath)
                return "開いているシーン「" + active.name + "」に未保存の変更があります。保存してから実行してください。";

            foreach (string dir in new[] { Root, MatDir, PrefabDir, DataDir, SceneDir, TexDir })
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            BuildMaterials();
            GameObject item = BuildItemPrefab();
            GameObject player = BuildPlayerPrefab();
            Dictionary<string, GameObject> stations = BuildStationPrefabs();
            LevelData level = BuildLevel(stations);
            AssetDatabase.SaveAssets();

            BuildScene(level, player, item);
            AddSceneToBuildSettings();
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            string view = SetPortraitGameView();
            AssetDatabase.SaveAssets();

            return "done. shader=" + (usingToon ? "DotaBata/Toon" : "URP/Lit (フォールバック)") +
                   ", materials=" + Mats.Count + ", station prefabs=" + stations.Count +
                   ", level=" + LevelPath + ", scene=" + ScenePath + ", " + view;
        }

        // ------------------------------------------------------------------ マテリアル

        static void BuildMaterials()
        {
            Mats.Clear();
            shader = Shader.Find("DotaBata/Toon");
            usingToon = shader != null && !ShaderUtil.ShaderHasError(shader);
            if (!usingToon) shader = Shader.Find("Universal Render Pipeline/Lit");

            Mat("Floor", "#FFFFFF", 0f, CheckerTexture());
            Mat("Wall", "#8D86C9");
            Mat("Wood", "#E0A96D");
            Mat("WoodDark", "#B97A45");
            Mat("Teal", "#4DB6AC");
            Mat("TealDark", "#2F8F86");
            Mat("White", "#F6F3EE");
            Mat("Grey", "#B8BCC8");
            Mat("DarkGrey", "#5C6270");
            Mat("Metal", "#D5DBE6");
            Mat("Black", "#2B2630");
            Mat("Cardboard", "#C99A62");
            Mat("CardboardLight", "#E6C594");
            Mat("Green", "#5FC27E");
            Mat("GreenDark", "#3E9A5C");
            Mat("Pink", "#FF6FA8");
            Mat("Blue", "#4FC3F7");
            Mat("Yellow", "#FFD54A");
            Mat("Red", "#EF5350");
            Mat("Skin", "#FFE0C2");
            Mat("Mint", "#7FDBC8");
            Mat("Coral", "#FF8A80");
            Mat("Ink", "#3B2F45");
            Mat("Focus", "#FFEB3B", 0f);
            Mat("BeltBody", "#4A4F5C");
            Mat("BeltStripe", "#FFD54A", 0f);
            Mat("BeltStripeReverse", "#EF5350", 0f);
        }

        static Material Mat(string name, string hex, float outline = 0.02f, Texture2D texture = null)
        {
            string path = MatDir + "/M_" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            mat.SetColor("_BaseColor", Hex(hex));
            if (mat.HasProperty("_OutlineWidth")) mat.SetFloat("_OutlineWidth", outline);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
            if (texture != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            Mats[name] = mat;
            return mat;
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return color;
        }

        /// <summary>床の市松模様（2×2マス）。</summary>
        static Texture2D CheckerTexture()
        {
            string path = TexDir + "/T_FloorChecker.png";
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color a = Hex("#FFF2D8");
            Color b = Hex("#F8DFB4");
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, ((x < size / 2) ^ (y < size / 2)) ? a : b);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ 部品づくりの小道具

        static GameObject P(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, string material, Vector3 euler = default)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Mats[material];
            return go;
        }

        static Transform Empty(string name, Transform parent, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go.transform;
        }

        static GameObject Save(GameObject root, string name)
        {
            string path = PrefabDir + "/" + name + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>持ち場の土台：当たり判定と、近づいたときに光る足元の枠。</summary>
        static GameObject NewStation(string name, Vector3 size, out GameObject focus)
        {
            var root = new GameObject(name);
            var box = root.AddComponent<BoxCollider>();
            box.size = size;
            box.center = new Vector3(0f, size.y * 0.5f, 0f);
            focus = P(PrimitiveType.Cube, "Focus", root.transform, new Vector3(0f, 0.02f, 0f), new Vector3(size.x + 0.26f, 0.04f, size.z + 0.26f), "Focus");
            focus.SetActive(false);
            return root;
        }

        static void Describe(Interactable target, string displayName, float labelHeight, GameObject focus)
        {
            target.displayName = displayName;
            target.labelHeight = labelHeight;
            target.focusMarker = focus;
        }

        /// <summary>作業台の共通の見た目（箱＋天板）。</summary>
        static void TableBody(Transform parent, string bodyMaterial, string topMaterial)
        {
            P(PrimitiveType.Cube, "Body", parent, new Vector3(0f, 0.4f, 0f), new Vector3(0.96f, 0.8f, 0.96f), bodyMaterial);
            P(PrimitiveType.Cube, "Top", parent, new Vector3(0f, 0.83f, 0f), new Vector3(1.04f, 0.07f, 1.04f), topMaterial);
        }

        // ------------------------------------------------------------------ グッズ

        static GameObject BuildItemPrefab()
        {
            var root = new GameObject("Item");
            var body = root.AddComponent<Rigidbody>();
            body.mass = 0.6f;
            body.linearDamping = 1.5f;
            body.angularDamping = 3f;
            body.isKinematic = true;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(0.44f, 0.4f, 0.44f);
            box.center = new Vector3(0f, 0.2f, 0f);
            var item = root.AddComponent<Item>();
            Transform t = root.transform;
            var paintTargets = new List<Renderer>();

            // パーツ（アクスタ）：板と台座がばらばら
            Transform partStand = Empty("Part_Stand", t, Vector3.zero);
            P(PrimitiveType.Cube, "Plate", partStand, new Vector3(-0.06f, 0.03f, 0f), new Vector3(0.26f, 0.04f, 0.36f), "White", new Vector3(0f, 12f, 0f));
            P(PrimitiveType.Cylinder, "Base", partStand, new Vector3(0.15f, 0.03f, -0.1f), new Vector3(0.18f, 0.02f, 0.18f), "Grey");

            // パーツ（缶バッジ）：金具と表の板
            Transform partBadge = Empty("Part_Badge", t, Vector3.zero);
            P(PrimitiveType.Cylinder, "Back", partBadge, new Vector3(-0.07f, 0.03f, -0.02f), new Vector3(0.3f, 0.02f, 0.3f), "Metal");
            P(PrimitiveType.Cylinder, "Front", partBadge, new Vector3(0.1f, 0.07f, 0.07f), new Vector3(0.26f, 0.015f, 0.26f), "White");

            // 組み立て済みのアクスタ：台座に板が立つ
            Transform stand = Empty("Stand", t, Vector3.zero);
            P(PrimitiveType.Cylinder, "Base", stand, new Vector3(0f, 0.03f, 0f), new Vector3(0.38f, 0.03f, 0.38f), "Grey");
            paintTargets.Add(P(PrimitiveType.Cube, "Plate", stand, new Vector3(0f, 0.31f, 0f), new Vector3(0.3f, 0.48f, 0.06f), "White").GetComponent<Renderer>());

            // 組み立て済みの缶バッジ
            Transform badge = Empty("Badge", t, Vector3.zero);
            P(PrimitiveType.Cylinder, "Rim", badge, new Vector3(0f, 0.04f, 0f), new Vector3(0.46f, 0.04f, 0.46f), "Metal");
            paintTargets.Add(P(PrimitiveType.Cylinder, "Face", badge, new Vector3(0f, 0.09f, 0f), new Vector3(0.38f, 0.02f, 0.38f), "White").GetComponent<Renderer>());

            // 箱詰め済み：ふたの色と、上の小さな形で中身が分かる
            Transform boxed = Empty("Box", t, Vector3.zero);
            P(PrimitiveType.Cube, "Body", boxed, new Vector3(0f, 0.2f, 0f), new Vector3(0.46f, 0.4f, 0.46f), "Cardboard");
            P(PrimitiveType.Cube, "Tape", boxed, new Vector3(0f, 0.402f, 0f), new Vector3(0.1f, 0.012f, 0.47f), "CardboardLight");
            paintTargets.Add(P(PrimitiveType.Cube, "Label", boxed, new Vector3(0f, 0.41f, 0f), new Vector3(0.34f, 0.025f, 0.34f), "White").GetComponent<Renderer>());
            GameObject iconStand = P(PrimitiveType.Cube, "Icon_Stand", boxed, new Vector3(0f, 0.53f, 0f), new Vector3(0.16f, 0.22f, 0.05f), "White");
            GameObject iconBadge = P(PrimitiveType.Cylinder, "Icon_Badge", boxed, new Vector3(0f, 0.445f, 0f), new Vector3(0.22f, 0.02f, 0.22f), "White");

            item.partStand = partStand.gameObject;
            item.partBadge = partBadge.gameObject;
            item.stand = stand.gameObject;
            item.badge = badge.gameObject;
            item.box = boxed.gameObject;
            item.boxIconStand = iconStand;
            item.boxIconBadge = iconBadge;
            item.paintTargets = paintTargets.ToArray();
            item.colorMaterials = new[] { Mats["White"], Mats["Pink"], Mats["Blue"], Mats["Yellow"] };
            item.labelHeight = 0.9f;
            item.Setup(ProductType.AcrylicStand, ItemStage.Part, PaintColor.None);
            return Save(root, "Item");
        }

        // ------------------------------------------------------------------ 仮キャラ

        static GameObject BuildPlayerPrefab()
        {
            var root = new GameObject("Player");
            var controller = root.AddComponent<CharacterController>();
            controller.radius = 0.32f;
            controller.height = 1.5f;
            controller.center = new Vector3(0f, 0.75f, 0f);
            controller.stepOffset = 0.15f;
            controller.skinWidth = 0.04f;
            controller.minMoveDistance = 0f;
            var player = root.AddComponent<PlayerController>();

            // 2〜2.5頭身のデフォルメ体型。特定のタレントには似せていない仮キャラ
            Transform visual = Empty("Visual", root.transform, Vector3.zero);
            P(PrimitiveType.Capsule, "Body", visual, new Vector3(0f, 0.44f, 0f), new Vector3(0.62f, 0.42f, 0.58f), "Mint");
            P(PrimitiveType.Sphere, "Head", visual, new Vector3(0f, 1.1f, 0f), new Vector3(0.8f, 0.74f, 0.78f), "Skin");
            P(PrimitiveType.Sphere, "Hair", visual, new Vector3(0f, 1.2f, -0.07f), new Vector3(0.86f, 0.66f, 0.84f), "Coral");
            P(PrimitiveType.Sphere, "Eye_L", visual, new Vector3(-0.16f, 1.06f, 0.35f), new Vector3(0.1f, 0.14f, 0.06f), "Ink");
            P(PrimitiveType.Sphere, "Eye_R", visual, new Vector3(0.16f, 1.06f, 0.35f), new Vector3(0.1f, 0.14f, 0.06f), "Ink");
            GameObject handL = P(PrimitiveType.Sphere, "Hand_L", visual, new Vector3(-0.37f, 0.56f, 0.14f), new Vector3(0.2f, 0.2f, 0.2f), "Skin");
            GameObject handR = P(PrimitiveType.Sphere, "Hand_R", visual, new Vector3(0.37f, 0.56f, 0.14f), new Vector3(0.2f, 0.2f, 0.2f), "Skin");
            P(PrimitiveType.Sphere, "Foot_L", visual, new Vector3(-0.16f, 0.07f, 0.04f), new Vector3(0.24f, 0.14f, 0.32f), "White");
            P(PrimitiveType.Sphere, "Foot_R", visual, new Vector3(0.16f, 0.07f, 0.04f), new Vector3(0.24f, 0.14f, 0.32f), "White");

            player.visual = visual;
            player.handLeft = handL.transform;
            player.handRight = handR.transform;
            // 斜め上からのカメラでも隠れないように、グッズは頭の上にかかげて運ぶ
            player.holdAnchor = Empty("HoldAnchor", visual, new Vector3(0f, 1.6f, 0f));
            return Save(root, "Player");
        }

        // ------------------------------------------------------------------ 持ち場

        static Dictionary<string, GameObject> BuildStationPrefabs()
        {
            var result = new Dictionary<string, GameObject>();
            result["Shelf_Stand"] = BuildShelf(ProductType.AcrylicStand);
            result["Shelf_Badge"] = BuildShelf(ProductType.CanBadge);
            result["AssemblyTable"] = BuildAssemblyTable();
            result["Paint_Pink"] = BuildPaintMachine(PaintColor.Pink, "Pink");
            result["Paint_Blue"] = BuildPaintMachine(PaintColor.Blue, "Blue");
            result["Paint_Yellow"] = BuildPaintMachine(PaintColor.Yellow, "Yellow");
            result["PackingTable"] = BuildPackingTable();
            result["Counter"] = BuildCounter();
            result["ShippingGate"] = BuildShippingGate();
            result["TrashBin"] = BuildTrashBin();
            result["ConveyorBelt"] = BuildConveyorBelt();
            return result;
        }

        static GameObject BuildShelf(ProductType type)
        {
            bool isStand = type == ProductType.AcrylicStand;
            GameObject root = NewStation(isStand ? "Shelf_Stand" : "Shelf_Badge", new Vector3(1f, 0.95f, 1f), out GameObject focus);
            Transform t = root.transform;
            P(PrimitiveType.Cube, "Body", t, new Vector3(0f, 0.4f, 0f), new Vector3(0.96f, 0.8f, 0.96f), "Wood");
            P(PrimitiveType.Cube, "Tray", t, new Vector3(0f, 0.81f, 0f), new Vector3(0.9f, 0.03f, 0.9f), "CardboardLight");
            P(PrimitiveType.Cube, "Rim_Back", t, new Vector3(0f, 0.88f, 0.47f), new Vector3(1.04f, 0.16f, 0.1f), "WoodDark");
            P(PrimitiveType.Cube, "Rim_Front", t, new Vector3(0f, 0.88f, -0.47f), new Vector3(1.04f, 0.16f, 0.1f), "WoodDark");
            P(PrimitiveType.Cube, "Rim_Left", t, new Vector3(-0.47f, 0.88f, 0f), new Vector3(0.1f, 0.16f, 0.84f), "WoodDark");
            P(PrimitiveType.Cube, "Rim_Right", t, new Vector3(0.47f, 0.88f, 0f), new Vector3(0.1f, 0.16f, 0.84f), "WoodDark");

            if (isStand)
            {
                P(PrimitiveType.Cube, "Sample_1", t, new Vector3(-0.18f, 0.86f, 0.12f), new Vector3(0.26f, 0.04f, 0.36f), "White", new Vector3(0f, 18f, 0f));
                P(PrimitiveType.Cube, "Sample_2", t, new Vector3(0.16f, 0.9f, -0.1f), new Vector3(0.26f, 0.04f, 0.36f), "White", new Vector3(0f, -24f, 0f));
                P(PrimitiveType.Cylinder, "Sample_3", t, new Vector3(0.2f, 0.85f, 0.24f), new Vector3(0.18f, 0.02f, 0.18f), "Grey");
                // 目印：小さなアクスタ
                P(PrimitiveType.Cylinder, "Sign_Base", t, new Vector3(-0.3f, 0.98f, -0.3f), new Vector3(0.26f, 0.02f, 0.26f), "Grey");
                P(PrimitiveType.Cube, "Sign_Plate", t, new Vector3(-0.3f, 1.2f, -0.3f), new Vector3(0.22f, 0.36f, 0.05f), "White");
            }
            else
            {
                P(PrimitiveType.Cylinder, "Sample_1", t, new Vector3(-0.18f, 0.85f, 0.14f), new Vector3(0.3f, 0.02f, 0.3f), "Metal");
                P(PrimitiveType.Cylinder, "Sample_2", t, new Vector3(0.18f, 0.87f, -0.08f), new Vector3(0.3f, 0.02f, 0.3f), "White");
                P(PrimitiveType.Cylinder, "Sample_3", t, new Vector3(0.14f, 0.85f, 0.26f), new Vector3(0.26f, 0.02f, 0.26f), "Metal");
                // 目印：立てた缶バッジ
                P(PrimitiveType.Cylinder, "Sign_Badge", t, new Vector3(-0.3f, 1.16f, -0.3f), new Vector3(0.34f, 0.03f, 0.34f), "White", new Vector3(70f, 0f, 0f));
            }

            var shelf = root.AddComponent<PartsShelf>();
            shelf.productType = type;
            Describe(shelf, isStand ? "アクスタ材料" : "缶バッジ材料", 1.75f, focus);
            return Save(root, root.name);
        }

        static GameObject BuildAssemblyTable()
        {
            GameObject root = NewStation("AssemblyTable", new Vector3(1f, 0.9f, 1f), out GameObject focus);
            Transform t = root.transform;
            TableBody(t, "Teal", "White");
            P(PrimitiveType.Cube, "Mat", t, new Vector3(0f, 0.87f, 0f), new Vector3(0.72f, 0.02f, 0.72f), "TealDark");
            Transform spinner = Empty("Spinner", t, new Vector3(0.38f, 0.98f, 0.38f));
            P(PrimitiveType.Cylinder, "Gear", spinner, Vector3.zero, new Vector3(0.2f, 0.04f, 0.2f), "Yellow");
            P(PrimitiveType.Cube, "Tooth", spinner, new Vector3(0f, 0f, 0f), new Vector3(0.28f, 0.05f, 0.06f), "Yellow");
            P(PrimitiveType.Cylinder, "Post", t, new Vector3(0.38f, 0.92f, 0.38f), new Vector3(0.06f, 0.06f, 0.06f), "DarkGrey");

            var station = root.AddComponent<ProcessStation>();
            station.kind = ProcessKind.Assemble;
            station.workSeconds = 1.2f;
            station.autoWork = false;
            station.slot = Empty("Slot", t, new Vector3(0f, 0.88f, 0f));
            station.spinner = spinner;
            Describe(station, "組み立て台", 1.75f, focus);
            return Save(root, root.name);
        }

        static GameObject BuildPaintMachine(PaintColor color, string material)
        {
            GameObject root = NewStation("PaintMachine_" + material, new Vector3(1f, 0.9f, 1f), out GameObject focus);
            Transform t = root.transform;
            TableBody(t, "White", "Grey");
            P(PrimitiveType.Cube, "Stripe", t, new Vector3(0f, 0.42f, -0.485f), new Vector3(0.78f, 0.34f, 0.02f), material);
            P(PrimitiveType.Cube, "Pillar_L", t, new Vector3(-0.45f, 1.25f, 0.2f), new Vector3(0.1f, 0.78f, 0.4f), "Grey");
            P(PrimitiveType.Cube, "Pillar_R", t, new Vector3(0.45f, 1.25f, 0.2f), new Vector3(0.1f, 0.78f, 0.4f), "Grey");
            P(PrimitiveType.Cube, "Hood", t, new Vector3(0f, 1.7f, 0.2f), new Vector3(1.02f, 0.14f, 0.5f), material);
            P(PrimitiveType.Cylinder, "Tank", t, new Vector3(0f, 1.93f, 0.2f), new Vector3(0.42f, 0.16f, 0.42f), material);
            Transform spinner = Empty("Spinner", t, new Vector3(0f, 1.56f, 0.2f));
            P(PrimitiveType.Cylinder, "Nozzle", spinner, Vector3.zero, new Vector3(0.14f, 0.07f, 0.14f), "DarkGrey");
            P(PrimitiveType.Cube, "Blade", spinner, new Vector3(0f, -0.05f, 0f), new Vector3(0.4f, 0.03f, 0.06f), "DarkGrey");
            // ベルト側（左）への受け渡し口
            P(PrimitiveType.Cube, "Chute", t, new Vector3(-0.56f, 0.74f, 0f), new Vector3(0.2f, 0.05f, 0.46f), "Grey", new Vector3(0f, 0f, 18f));

            var station = root.AddComponent<ProcessStation>();
            station.kind = ProcessKind.Paint;
            station.paintColor = color;
            station.workSeconds = 4f;
            station.autoWork = true;
            station.slot = Empty("Slot", t, new Vector3(0f, 0.88f, 0f));
            station.spinner = spinner;
            Describe(station, GameText.Name(color), 2.45f, focus);
            return Save(root, root.name);
        }

        static GameObject BuildPackingTable()
        {
            GameObject root = NewStation("PackingTable", new Vector3(1f, 0.9f, 1f), out GameObject focus);
            Transform t = root.transform;
            TableBody(t, "WoodDark", "CardboardLight");
            P(PrimitiveType.Cube, "Flat_1", t, new Vector3(0.3f, 0.89f, 0.3f), new Vector3(0.36f, 0.05f, 0.36f), "Cardboard", new Vector3(0f, 8f, 0f));
            P(PrimitiveType.Cube, "Flat_2", t, new Vector3(0.3f, 0.94f, 0.3f), new Vector3(0.36f, 0.05f, 0.36f), "Cardboard", new Vector3(0f, -6f, 0f));
            Transform spinner = Empty("Spinner", t, new Vector3(-0.34f, 0.91f, 0.34f));
            P(PrimitiveType.Cylinder, "Tape", spinner, Vector3.zero, new Vector3(0.24f, 0.045f, 0.24f), "Yellow");
            P(PrimitiveType.Cube, "TapeEnd", spinner, new Vector3(0.12f, 0f, 0f), new Vector3(0.12f, 0.03f, 0.05f), "Yellow");

            var station = root.AddComponent<ProcessStation>();
            station.kind = ProcessKind.Pack;
            station.workSeconds = 0.9f;
            station.autoWork = false;
            station.slot = Empty("Slot", t, new Vector3(-0.05f, 0.88f, -0.08f));
            station.spinner = spinner;
            Describe(station, "梱包台", 1.75f, focus);
            return Save(root, root.name);
        }

        static GameObject BuildCounter()
        {
            GameObject root = NewStation("Counter", new Vector3(1f, 0.9f, 1f), out GameObject focus);
            Transform t = root.transform;
            TableBody(t, "Grey", "White");

            var station = root.AddComponent<ProcessStation>();
            station.kind = ProcessKind.Counter;
            station.slot = Empty("Slot", t, new Vector3(0f, 0.88f, 0f));
            Describe(station, "置き台", 1.6f, focus);
            return Save(root, root.name);
        }

        static GameObject BuildShippingGate()
        {
            GameObject root = NewStation("ShippingGate", new Vector3(1.8f, 0.9f, 1f), out GameObject focus);
            Transform t = root.transform;
            P(PrimitiveType.Cube, "Platform", t, new Vector3(0f, 0.3f, 0f), new Vector3(1.76f, 0.6f, 0.96f), "Green");
            for (int i = 0; i < 4; i++)
                P(PrimitiveType.Cylinder, "Roller_" + i, t, new Vector3(0f, 0.63f, -0.33f + i * 0.22f), new Vector3(0.12f, 0.8f, 0.12f), "Metal", new Vector3(0f, 0f, 90f));
            P(PrimitiveType.Cube, "Pillar_L", t, new Vector3(-0.8f, 1.15f, 0.36f), new Vector3(0.16f, 1.1f, 0.2f), "White");
            P(PrimitiveType.Cube, "Pillar_R", t, new Vector3(0.8f, 1.15f, 0.36f), new Vector3(0.16f, 1.1f, 0.2f), "White");
            P(PrimitiveType.Cube, "Beam", t, new Vector3(0f, 1.82f, 0.36f), new Vector3(1.8f, 0.3f, 0.24f), "GreenDark");
            P(PrimitiveType.Cube, "Opening", t, new Vector3(0f, 1.15f, 0.46f), new Vector3(1.46f, 1.06f, 0.04f), "Black");
            // 出荷の目印（上向きの矢印）
            P(PrimitiveType.Cube, "Arrow_Stem", t, new Vector3(0f, 1.05f, 0.42f), new Vector3(0.14f, 0.4f, 0.04f), "Yellow");
            P(PrimitiveType.Cube, "Arrow_Head", t, new Vector3(0f, 1.32f, 0.42f), new Vector3(0.3f, 0.3f, 0.04f), "Yellow", new Vector3(0f, 0f, 45f));

            var gate = root.AddComponent<ShippingGate>();
            gate.dropPoint = Empty("DropPoint", t, new Vector3(0f, 0.7f, -0.1f));
            Describe(gate, "出荷口", 2.45f, focus);
            return Save(root, root.name);
        }

        static GameObject BuildTrashBin()
        {
            GameObject root = NewStation("TrashBin", new Vector3(0.8f, 0.9f, 0.8f), out GameObject focus);
            Transform t = root.transform;
            P(PrimitiveType.Cylinder, "Body", t, new Vector3(0f, 0.4f, 0f), new Vector3(0.72f, 0.4f, 0.72f), "DarkGrey");
            P(PrimitiveType.Cylinder, "Rim", t, new Vector3(0f, 0.82f, 0f), new Vector3(0.8f, 0.03f, 0.8f), "Grey");
            P(PrimitiveType.Cylinder, "Inside", t, new Vector3(0f, 0.845f, 0f), new Vector3(0.62f, 0.01f, 0.62f), "Black");

            var bin = root.AddComponent<TrashBin>();
            Describe(bin, "ゴミ箱", 1.5f, focus);
            return Save(root, root.name);
        }

        static GameObject BuildConveyorBelt()
        {
            // 見た目は長さに合わせて起動時に作るので、プレハブは設定だけ持つ
            var root = new GameObject("ConveyorBelt");
            root.AddComponent<BoxCollider>();
            var belt = root.AddComponent<ConveyorBelt>();
            belt.length = 7f;
            belt.bodyMaterial = Mats["BeltBody"];
            belt.railMaterial = Mats["Grey"];
            belt.stripeMaterial = Mats["BeltStripe"];
            belt.stripeReverseMaterial = Mats["BeltStripeReverse"];
            belt.focusMaterial = Mats["Focus"];
            belt.displayName = "ベルト";
            belt.labelHeight = 1.2f;
            var box = root.GetComponent<BoxCollider>();
            box.size = new Vector3(belt.width, belt.height, belt.length);
            box.center = new Vector3(0f, belt.height * 0.5f, 0f);
            return Save(root, root.name);
        }

        // ------------------------------------------------------------------ ステージデータ

        static LevelData BuildLevel(Dictionary<string, GameObject> prefabs)
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(LevelPath);
            if (level == null)
            {
                level = ScriptableObject.CreateInstance<LevelData>();
                AssetDatabase.CreateAsset(level, LevelPath);
            }

            level.themeName = "グッズ工場";
            level.stageName = "ステージ1";
            level.managerName = "店長";
            level.timeLimit = 90f;
            level.starScores = new[] { 300, 600, 900 };
            level.maxActiveOrders = 4;
            level.defaultPatience = 40f;
            level.fillerInterval = 8f;
            level.floorSize = new Vector2(7f, 10f);
            level.playerStart = new Vector3(0.6f, 0f, -1f);
            level.floorMaterial = Mats["Floor"];
            level.wallMaterial = Mats["Wall"];

            // 部品の配置：下で材料を取り、右で組み、左で塗り、ベルトで上へ流し、上で箱づめして出荷
            level.stations = new List<StationPlacement>
            {
                Place(prefabs, "ConveyorBelt", "ベルトコンベア", -3.0f, 0.0f, 7.4f),
                Place(prefabs, "Paint_Pink", "色塗り台（ピンク）", -1.95f, -2.4f),
                Place(prefabs, "Paint_Blue", "色塗り台（みずいろ）", -1.95f, -1.2f),
                Place(prefabs, "Paint_Yellow", "色塗り台（きいろ）", -1.95f, 0.0f),
                Place(prefabs, "PackingTable", "梱包台", -1.75f, 4.5f),
                Place(prefabs, "Counter", "置き台（上）", -0.65f, 4.5f),
                Place(prefabs, "ShippingGate", "出荷口", 2.3f, 4.5f),
                Place(prefabs, "Shelf_Stand", "材料棚（アクスタ）", -0.9f, -4.5f),
                Place(prefabs, "Shelf_Badge", "材料棚（缶バッジ）", 0.3f, -4.5f),
                Place(prefabs, "AssemblyTable", "組み立て台A", 3.0f, -2.9f),
                Place(prefabs, "AssemblyTable", "組み立て台B", 3.0f, -1.7f),
                Place(prefabs, "Counter", "置き台（右）", 3.0f, 0.1f),
                Place(prefabs, "TrashBin", "ゴミ箱", 3.05f, 1.7f),
            };

            // 注文表
            level.orders = new List<OrderEntry>
            {
                Order(0f, ProductType.AcrylicStand, PaintColor.Pink),
                Order(3f, ProductType.CanBadge, PaintColor.Blue),
                Order(15f, ProductType.AcrylicStand, PaintColor.Yellow),
                Order(26f, ProductType.CanBadge, PaintColor.Pink),
                Order(36f, ProductType.AcrylicStand, PaintColor.Blue),
                Order(45f, ProductType.CanBadge, PaintColor.Yellow),
                Order(66f, ProductType.AcrylicStand, PaintColor.Pink),
                Order(76f, ProductType.CanBadge, PaintColor.Blue),
            };

            // ギミック設定：逆回転 → 注文ラッシュ → 応援タイム → もう一度逆回転
            level.gimmicks = new List<GimmickEvent>
            {
                new GimmickEvent { time = 28f, kind = GimmickKind.ConveyorReverse, duration = 8f },
                new GimmickEvent { time = 50f, kind = GimmickKind.OrderRush, count = 3 },
                new GimmickEvent { time = 62f, kind = GimmickKind.CheerTime, duration = 10f },
                new GimmickEvent { time = 72f, kind = GimmickKind.ConveyorReverse, duration = 7f },
            };

            EditorUtility.SetDirty(level);
            return level;
        }

        static StationPlacement Place(Dictionary<string, GameObject> prefabs, string key, string note, float x, float z, float length = 0f)
        {
            return new StationPlacement { note = note, prefab = prefabs[key], position = new Vector3(x, 0f, z), length = length };
        }

        static OrderEntry Order(float time, ProductType type, PaintColor color)
        {
            return new OrderEntry { time = time, type = type, color = color };
        }

        // ------------------------------------------------------------------ シーン

        static void BuildScene(LevelData level, GameObject playerPrefab, GameObject itemPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 新しいシーンを作ると使っていないアセットがメモリから外れるので、ここで読み直す。
            // 読み直さないと、参照が「中身のない抜け殻」のままシーンに残る。
            level = AssetDatabase.LoadAssetAtPath<LevelData>(LevelPath);
            playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Player.prefab");
            itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Item.prefab");

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(StageCamera));
            cameraObject.tag = "MainCamera";
            var cam = cameraObject.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("#BFE3F0");
            cam.allowHDR = false;
            UniversalAdditionalCameraData cameraData = cam.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false; // 後処理は最小限（企画書の方針）
            cameraData.renderShadows = true;
            cameraObject.GetComponent<StageCamera>().Configure(level.floorSize);

            var lightObject = new GameObject("Directional Light", typeof(Light));
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = Hex("#FFF6E6");
            light.intensity = 1f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.6f;
            lightObject.transform.rotation = Quaternion.Euler(52f, -32f, 0f);

            var game = new GameObject("Game");
            var audio = game.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            game.AddComponent<GameInput>();
            game.AddComponent<OrderManager>();
            var builder = game.AddComponent<StageBuilder>();
            builder.playerPrefab = playerPrefab;
            builder.itemPrefab = itemPrefab;
            var manager = game.AddComponent<GameManager>();
            manager.level = level;
            game.AddComponent<Hud>();
            game.AddComponent<DebugAutoPlayer>();

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Hex("#B9B4C8");
            RenderSettings.fog = false;

            EditorSceneManager.SaveScene(scene, ScenePath);
            // 保存した内容で開き直して、参照をすべて新しく読み込ませる
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        static void AddSceneToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
