using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Pass 1: gemeinsame Miniatur-Bodenhoehe (MiniFloorY) und einheitliches Modul-Design.
/// Arbeitet ausschliesslich auf der sichtbaren Ebene (VisualRoot/Visual_Prototype),
/// auf Snap-Ankern, Slot-Markern und der exakten Bodenplatzierung.
/// Mechanik, Slot-Typen, SnapRadius, Socket-Kompatibilitaet bleiben unberuehrt.
/// </summary>
public static class Pass1Heights
{
    // ------------------------------------------------------------------
    // Fachliche Konstanten
    // ------------------------------------------------------------------

    /// <summary>Ziel-Bodenoberkante in Board-lokalen Koordinaten (= Oberkante Hallway_Floor).</summary>
    public const float MiniFloorY = 0.0130f;

    /// <summary>Bodenstaerke aller Raeume (einheitlicher Sockel).</summary>
    public const float FloorThickness = 0.0120f;

    /// <summary>Wandstaerke aller Raeume.</summary>
    public const float WallThickness = 0.0120f;

    /// <summary>Wandhoehe ueber der Bodenoberkante.</summary>
    public const float WallHeight = 0.0360f;

    /// <summary>Hoehe der Turoeffnung ueber der Bodenoberkante.</summary>
    public const float DoorOpeningHeight = 0.0240f;

    /// <summary>Einheitliche Hoehe der Snap-Anker/Tuer-Sockets ueber der Bodenoberkante.</summary>
    public const float AnchorHeightAboveFloor = 0.0070f;

    /// <summary>Slot-Marker: Oberkante liegt knapp ueber MiniFloorY (kein Z-Fighting, kein Schweben).</summary>
    public const float SlotMarkerCenterY = 0.0155f;
    public const float SlotMarkerThickness = 0.0015f;

    private const string BoardRoot = "_DOLLHOUSE";

    private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
    private static readonly List<string> Log = new List<string>();

    // ------------------------------------------------------------------
    // Einstieg
    // ------------------------------------------------------------------

    public static string Main()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var dollhouse = GameObject.Find(BoardRoot);
        if (dollhouse == null)
            return "FEHLER: _DOLLHOUSE nicht gefunden.";

        Undo.RegisterFullObjectHierarchyUndo(dollhouse, "Miniaturhoehen vereinheitlichen");

        LoadMaterials();

        FixHallwayParapet();
        FixRoomA();
        foreach (string module in new[] { "Office_Module", "ChildrenRoom_Module", "Kitchen_Module", "Bedroom_Module" })
            FixModule(module);
        FixSlots();
        FixHallwayDoorAnchors();
        FixPuppetSocket();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        return string.Join("\n", Log);
    }

    // ------------------------------------------------------------------
    // Materialien
    // ------------------------------------------------------------------

    private static Material Mat(string name)
    {
        if (Materials.TryGetValue(name, out Material cached))
            return cached;

        string path = $"Assets/Project/Materials/PrototypeVisuals/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
            Debug.LogWarning($"[Pass1] Material fehlt: {path}");
        Materials[name] = material;
        return material;
    }

    private static void LoadMaterials()
    {
        foreach (string name in new[] { "Wood", "Ivory", "PrototypeDark", "OfficeNavy", "KitchenTerracotta", "ChildSage", "ChildYellow", "BedroomBlue" })
            Mat(name);
    }

    // ------------------------------------------------------------------
    // Primitive Helfer
    // ------------------------------------------------------------------

    /// <summary>
    /// Holt ein vorhandenes Kind oder erzeugt ein Wuerfel-Primitive ohne Collider.
    /// Visual_Prototype darf ausschliesslich MeshFilter und MeshRenderer enthalten.
    /// </summary>
    private static Transform EnsurePiece(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        Transform existing = parent.Find(name);
        GameObject go;

        if (existing != null)
        {
            go = existing.gameObject;
            // Vorhandene Teile ggf. in die gewuenschte Gruppe umhaengen,
            // damit keine doppelten Prototypteile entstehen.
            if (go.transform.parent != parent)
                go.transform.SetParent(parent, false);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Undo.RegisterCreatedObjectUndo(go, "Prototyp-Teil erzeugen");
            go.transform.SetParent(parent, false);
        }

        // Collider gehoeren nie auf die reine Darstellungsebene.
        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider);

        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;
        go.SetActive(true);

        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        return go.transform;
    }

    /// <summary>Rekursive Suche nach einem Kind – Transform.Find sieht nur direkte Kinder.</summary>
    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;

            Transform found = FindDeep(child, name);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Transform EnsureGroup(Transform parent, string name)    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Gruppe erzeugen");
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static void SetPiece(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        EnsurePiece(parent, name, localPosition, localScale, material);
    }

    private static void LogSet(string category, string message)
    {
        Log.Add($"[{category}] {message}");
    }

    /// <summary>
    /// Hoechste Bodenoberkante unterhalb eines Visual_Prototype, gemessen in
    /// Board-lokalen Koordinaten (Board-Y = Hochachse des Boards).
    /// Nutzt echte Mesh-Vertices, weil die Welt-AABB geneigter, duenner Platten
    /// deutlich zu gross ist.
    /// </summary>
    private static float MeasureMaxFloorTop(Transform prototype)
    {
        Transform board = GameObject.Find("DollhouseBoard")?.transform;
        if (board == null)
            return float.NaN;

        float maxTop = float.MinValue;
        foreach (Renderer renderer in prototype.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.name.ToLowerInvariant().Contains("floor"))
                continue;

            var meshFilter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
            if (mesh == null)
            {
                maxTop = Mathf.Max(maxTop, board.InverseTransformPoint(renderer.bounds.center).y);
                continue;
            }

            Matrix4x4 matrix = renderer.transform.localToWorldMatrix;
            foreach (Vector3 vertex in mesh.vertices)
            {
                float y = board.InverseTransformPoint(matrix.MultiplyPoint3x4(vertex)).y;
                if (y > maxTop)
                    maxTop = y;
            }
        }

        return maxTop;
    }

    // ------------------------------------------------------------------
    // Flur: niedrige Randsegmente mit klaren Tuerdurchgaengen
    // ------------------------------------------------------------------

    /// <summary>
    /// Hallway_Module bleibt Hoehenreferenz. Boden bleibt unveraendert
    /// (Oberkante = MiniFloorY). Die Randsegmente werden auf einheitliche
    /// Wandstaerke/-hoehe gebracht und exakt auf die Anschlussstellen ausgerichtet.
    /// </summary>
    private static void FixHallwayParapet()
    {
        var hallway = GameObject.Find("Hallway_Module");
        Transform prototype = hallway.transform.Find("VisualRoot/Visual_Prototype");
        if (prototype == null)
        {
            LogSet("Flur", "FEHLER: VisualRoot/Visual_Prototype fehlt.");
            return;
        }

        // Flur-lokale Lage des Bodens (unveraendert):
        //   lokales Y: 0.0000 .. 0.0120  -> Oberkante = MiniFloorY
        //   lokales X: -0.1075 .. +0.1075
        //   lokales Z: -0.4500 .. +0.4500
        float floorTopLocal = 0.0120f;
        float parapetY = floorTopLocal + WallHeight * 0.5f;          // 0.0300
        float lintelY = floorTopLocal + DoorOpeningHeight + WallThickness * 0.5f; // 0.0420
        float thresholdY = floorTopLocal + 0.0010f;                  // 0.0130
        const float westX = -0.1015f;   // Board X 0.1280..0.1400
        const float eastX = 0.1015f;    // Board X 0.3310..0.3430
        const float openWidth = 0.1000f;

        Material wood = Mat("Wood");
        Material ivory = Mat("Ivory");

        // Anschlussstellen (Flur-lokales Z, identisch mit den ConnectionPoints)
        float bedroomZ = -0.3300f;
        float kitchenZ = -0.0300f;
        float officeZ = 0.2950f;
        float childrenZ = -0.2700f;
        float livingZ = 0.3000f;

        var west = new (string Name, float Center, float Length)[]
        {
            ("Hallway_Edge_Left",   -0.4150f, 0.0700f),
            ("Hallway_Rail_Left_1", -0.1800f, 0.2000f),
            ("Hallway_Rail_Left_2",  0.1325f, 0.2250f),
            ("Hallway_Rail_Left_3",  0.3975f, 0.1050f),
        };
        var east = new (string Name, float Center, float Length)[]
        {
            ("Hallway_Edge_Right",  -0.3850f, 0.1300f),
            ("Hallway_Rail_Right_1", 0.0150f, 0.4700f),
            ("Hallway_Rail_Right_2", 0.4000f, 0.1000f),
        };

        foreach (var piece in west)
            SetPiece(prototype, piece.Name, new Vector3(westX, parapetY, piece.Center), new Vector3(WallThickness, WallHeight, piece.Length), ivory);

        foreach (var piece in east)
            SetPiece(prototype, piece.Name, new Vector3(eastX, parapetY, piece.Center), new Vector3(WallThickness, WallHeight, piece.Length), ivory);

        // Reststueck aus dem alten Aufbau: sichtbar deaktivieren statt loeschen.
        Transform leftover = prototype.Find("Hallway_Rail_Right_3");
        if (leftover != null)
        {
            leftover.gameObject.SetActive(false);
            LogSet("Flur", "Hallway_Rail_Right_3 deaktiviert (Altbestand).");
        }

        // Endbegrenzungen, damit der Flur als Korridor lesbar bleibt.
        SetPiece(prototype, "Hallway_End_North", new Vector3(0f, parapetY, -0.4440f), new Vector3(0.2150f, WallHeight, WallThickness), ivory);
        SetPiece(prototype, "Hallway_End_South", new Vector3(0f, parapetY, 0.4440f), new Vector3(0.2150f, WallHeight, WallThickness), ivory);

        // Tuerstuerze und Schwellen an jeder Anschlussstelle.
        var anchors = new (string Name, float X, float Z)[]
        {
            ("Bedroom", westX, bedroomZ),
            ("Kitchen", westX, kitchenZ),
            ("Office", westX, officeZ),
            ("ChildrenRoom", eastX, childrenZ),
            ("LivingRoom", eastX, livingZ),
        };

        foreach (var anchor in anchors)
        {
            SetPiece(prototype, $"Hallway_Lintel_{anchor.Name}",
                new Vector3(anchor.X, lintelY, anchor.Z),
                new Vector3(WallThickness, WallThickness, openWidth), ivory);

            SetPiece(prototype, $"Hallway_Threshold_{anchor.Name}",
                new Vector3(anchor.X, thresholdY, anchor.Z),
                new Vector3(WallThickness, 0.0020f, openWidth), ivory);
        }

        LogSet("Flur", $"Randsegmente auf Hoehe {parapetY:F4} (Boden+{WallHeight:F4}), Staerke {WallThickness:F4}; 5 Tuerdurchgaenge + 5 Schwellen an den Snap-Ankern.");
    }

    // ------------------------------------------------------------------
    // Room_A / Wohnzimmer (fest, nicht greifbar)
    // ------------------------------------------------------------------

    private static void FixRoomA()
    {
        var roomA = GameObject.Find("Room_A");
        if (roomA == null)
        {
            LogSet("Room_A", "FEHLER: Room_A nicht gefunden.");
            return;
        }

        Transform root = roomA.transform;

        // 1) Technisches Root exakt auf MiniFloorY. Damit stimmt Edit- und Playmode ueberein,
        //    weil GetRoomBottomOffset() jetzt exakt rechnet (Collider-Unterkante = -0.0120).
        Vector3 rootLocal = root.localPosition;
        rootLocal.y = MiniFloorY;
        root.localPosition = rootLocal;

        // 2) Collider: Unterkante = Bodenunterkante -> Bodenoberkante landet exakt auf MiniFloorY.
        var collider = roomA.GetComponent<BoxCollider>();
        if (collider != null)
        {
            collider.center = new Vector3(-0.0984f, 0.0117f, -0.0789f);
            collider.size = new Vector3(0.2911f, 0.0474f, 0.3041f);
        }

        // 3) VisualRoot/Visual_Prototype ergaenzen (bisher fehlte die Trennung).
        Transform visualRoot = EnsureGroup(root, "VisualRoot");
        Transform prototype = EnsureGroup(visualRoot, "Visual_Prototype");
        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;
        prototype.localPosition = Vector3.zero;
        prototype.localRotation = Quaternion.identity;
        prototype.localScale = Vector3.one;

        // 4) Vorhandenes LowPoly-Wohnzimmer unter Visual_Prototype haengen (Weltpose bleibt).
        //    Achtung: Transform.Find sucht nur direkte Kinder, deshalb rekursiv suchen.
        Transform livingRoom = FindDeep(root, "Living_Room (1)");
        if (livingRoom != null && livingRoom.parent != prototype)
        {
            livingRoom.SetParent(prototype, true);
            LogSet("Room_A", "Living_Room (1) unter VisualRoot/Visual_Prototype verschoben (Weltpose erhalten).");
        }

        // 5) Sichtbare Bodenoberkante auf MiniFloorY: LowPoly-Boden um -0.0382 absenken.
        //    (Die LowPoly-Bodenplatten liegen 0.2 mm auseinander; -0.0382 zentriert sie
        //     symmetrisch um MiniFloorY statt sie einseitig darueber zu setzen.)
        if (livingRoom != null)
        {
            Vector3 livingLocal = livingRoom.localPosition;
            livingLocal.y = -0.0382f;
            livingRoom.localPosition = livingLocal;
        }

        // 5b) Reine Darstellungsebene: Collider des LowPoly-Modells entfernen.
        //     Die Physik des Raums liefert ausschliesslich der BoxCollider am TechnicalRoot.
        int removedColliders = 0;
        foreach (Collider childCollider in prototype.GetComponentsInChildren<Collider>(true))
        {
            UnityEngine.Object.DestroyImmediate(childCollider);
            removedColliders++;
        }

        if (removedColliders > 0)
            LogSet("Room_A", $"{removedColliders} Collider aus Visual_Prototype entfernt (Physik liegt jetzt nur noch am TechnicalRoot).");

        // 5c) Feinjustierung: die hoechste sichtbare Bodenkante exakt auf MiniFloorY legen.
        //     Das LowPoly-Modell hat minimal unterschiedliche Bodenplatten; gemessen wird
        //     deshalb die tatsaechliche Oberkante, nicht ein angenommener Offset.
        if (livingRoom != null)
        {
            Physics.SyncTransforms();
            float delta = MiniFloorY - MeasureMaxFloorTop(prototype);
            if (Mathf.Abs(delta) > 0.000001f)
            {
                Vector3 fine = livingRoom.localPosition;
                fine.y += delta;
                livingRoom.localPosition = fine;
                Physics.SyncTransforms();
                LogSet("Room_A", $"Bodenfeinjustierung {delta:+0.000000;-0.000000} m -> Oberkante exakt {MeasureMaxFloorTop(prototype):F6}");
            }
        }

        // 6) Einheitlicher Sockel unter dem Wohnzimmer (gleiche Bodenstaerke wie alle Module).
        SetPiece(prototype, "Base_Slab",
            new Vector3(-0.09835f, -0.00680f, -0.07885f),
            new Vector3(0.2911f, 0.0104f, 0.3041f),
            Mat("Wood"));

        // 7) Tuer-Socket bleibt am sichtbaren Tueranker, jetzt 0.0070 ueber der Bodenebene.
        Transform eastSocket = root.Find("DoorSocket_East");
        if (eastSocket != null)
        {
            Vector3 socketLocal = eastSocket.localPosition;
            socketLocal.y = AnchorHeightAboveFloor;
            eastSocket.localPosition = socketLocal;
        }

        // 8) Fester Ausgangsraum: nicht greifbar.
        var room = roomA.GetComponent<DollhouseRoom>();
        if (room != null)
        {
            var so = new SerializedObject(room);
            SerializedProperty flag = so.FindProperty("isPlayerCurrentRoom");
            if (flag != null && !flag.boolValue)
            {
                flag.boolValue = true; // sperrt das Greifen ueber lockWhenPlayerInside
                so.ApplyModifiedPropertiesWithoutUndo();
                LogSet("Room_A", "isPlayerCurrentRoom = true gesetzt -> fest auf dem Board, nicht greifbar.");
            }
        }

        LogSet("Room_A", $"Root-Bodenhoehe = {MiniFloorY:F4}, Collider-Unterkante = -0.0120 (Board-lokal {MiniFloorY - FloorThickness:F4}), DoorSocket_East auf {AnchorHeightAboveFloor:F4} ueber Boden.");
    }

    // ------------------------------------------------------------------
    // Einsetzbare Module
    // ------------------------------------------------------------------

    private static void FixModule(string moduleName)
    {
        var module = GameObject.Find(moduleName);
        if (module == null)
        {
            LogSet(moduleName, "FEHLER: nicht gefunden.");
            return;
        }

        Transform root = module.transform;
        var room = module.GetComponent<DollhouseRoom>();
        Transform prototype = root.Find("VisualRoot/Visual_Prototype");
        if (prototype == null)
        {
            LogSet(moduleName, "FEHLER: VisualRoot/Visual_Prototype fehlt.");
            return;
        }

        Material wall = ModuleWallMaterial(moduleName);

        // Etwaige Zwischengruppe aus einem frueheren Lauf entfernen: die Moebelstuecke
        // liegen – wie urspruenglich – direkt unter Visual_Prototype.
        Transform staleGroup = prototype.Find("Furniture");
        if (staleGroup != null)
            UnityEngine.Object.DestroyImmediate(staleGroup.gameObject);

        BuildShell(prototype, wall);
        BuildFurniture(moduleName, prototype, wall);

        // Snap-Anker und Tuer-Socket auf die einheitliche Ankerhoehe.
        if (room != null && room.ModuleSnapAnchor != null)
        {
            Vector3 anchorLocal = room.ModuleSnapAnchor.localPosition;
            anchorLocal.y = AnchorHeightAboveFloor;
            room.ModuleSnapAnchor.localPosition = anchorLocal;
        }

        foreach (var socket in root.GetComponentsInChildren<DoorSocket>(true))
        {
            if (socket.transform.parent == root)
            {
                Vector3 socketLocal = socket.transform.localPosition;
                socketLocal.y = AnchorHeightAboveFloor;
                socket.transform.localPosition = socketLocal;
            }
        }

        // Collider folgt exakt der sichtbaren Geometrie:
        // Unterkante = Bodenunterkante (-0.0120), Oberkante = Wandoberkante (+0.0360).
        var collider = module.GetComponent<BoxCollider>();
        if (collider != null)
        {
            collider.center = new Vector3(0.0300f, 0.0120f, 0.2000f);
            collider.size = new Vector3(0.2350f, 0.0480f, 0.3300f);
        }

        // Modul steht mit der Bodenunterkante auf seinem Fundort-Sockel (Oberkante y = 0.30).
        Vector3 resting = root.position;
        resting.y = 0.3000f + FloorThickness;
        root.position = resting;

        LogSet(moduleName, $"Bodenoberkante nach Snap = {MiniFloorY:F4} (Board-lokal) | Ruhehoehe auf Sockel = {resting.y:F4}");
    }

    private static Material ModuleWallMaterial(string moduleName)
    {
        switch (moduleName)
        {
            case "Office_Module": return Mat("OfficeNavy");
            case "Kitchen_Module": return Mat("KitchenTerracotta");
            case "ChildrenRoom_Module": return Mat("ChildSage");
            case "Bedroom_Module": return Mat("BedroomBlue");
            default: return Mat("Ivory");
        }
    }

    /// <summary>
    /// Einheitliche Huelle: 12 mm Boden, 12 mm Waende, 36 mm Wandhoehe,
    /// offene Vorderseite (+Z) mit klarer Turoeffnung genau am Snap-Anker.
    /// </summary>
    private static void BuildShell(Transform prototype, Material wall)
    {
        Material wood = Mat("Wood");
        Material ivory = Mat("Ivory");

        // Boden: Oberkante exakt auf Modul-lokal 0.0 (== MiniFloorY nach dem Snap)
        SetPiece(prototype, "Floor", new Vector3(0.0300f, -FloorThickness * 0.5f, 0.2000f), new Vector3(0.2350f, FloorThickness, 0.3300f), wood);

        float wallY = WallHeight * 0.5f;
        SetPiece(prototype, "Wall_Left", new Vector3(-0.0815f, wallY, 0.2000f), new Vector3(WallThickness, WallHeight, 0.3300f), wall);
        SetPiece(prototype, "Wall_Right", new Vector3(0.1415f, wallY, 0.2000f), new Vector3(WallThickness, WallHeight, 0.3300f), wall);
        SetPiece(prototype, "Wall_Back", new Vector3(0.0300f, wallY, 0.0410f), new Vector3(0.2350f, WallHeight, WallThickness), wall);

        // Tuerrahmen in der offenen Vorderseite, zentriert auf den Snap-Anker (X = 0.0300).
        SetPiece(prototype, "Door_Jamb_Left", new Vector3(-0.0160f, wallY, 0.3590f), new Vector3(WallThickness, WallHeight, WallThickness), ivory);
        SetPiece(prototype, "Door_Jamb_Right", new Vector3(0.0760f, wallY, 0.3590f), new Vector3(WallThickness, WallHeight, WallThickness), ivory);
        SetPiece(prototype, "Door_Lintel", new Vector3(0.0300f, DoorOpeningHeight + WallThickness * 0.5f, 0.3590f), new Vector3(0.1040f, WallThickness, WallThickness), ivory);
    }

    private static void BuildFurniture(string moduleName, Transform furniture, Material wall)
    {
        Material wood = Mat("Wood");
        Material ivory = Mat("Ivory");
        Material dark = Mat("PrototypeDark");

        switch (moduleName)
        {
            case "Office_Module":
                // Schreibtisch, Stuhl, Aktenblock, Regal
                SetPiece(furniture, "Desk_Top", new Vector3(-0.0250f, 0.0170f, 0.1150f), new Vector3(0.0900f, 0.0060f, 0.0700f), wood);
                SetPiece(furniture, "Desk_Pedestal", new Vector3(0.0000f, 0.0070f, 0.1150f), new Vector3(0.0180f, 0.0140f, 0.0550f), dark);
                SetPiece(furniture, "Desk_Leg_Left", new Vector3(-0.0600f, 0.0070f, 0.1150f), new Vector3(0.0100f, 0.0140f, 0.0600f), wood);
                SetPiece(furniture, "Desk_Tray", new Vector3(-0.0400f, 0.0230f, 0.1150f), new Vector3(0.0300f, 0.0060f, 0.0450f), dark);
                SetPiece(furniture, "Chair_Seat", new Vector3(0.0750f, 0.0110f, 0.1500f), new Vector3(0.0450f, 0.0060f, 0.0400f), wood);
                SetPiece(furniture, "Chair_Back", new Vector3(0.0750f, 0.0235f, 0.1690f), new Vector3(0.0450f, 0.0190f, 0.0080f), wall);
                SetPiece(furniture, "Chair_Leg", new Vector3(0.0750f, 0.0040f, 0.1500f), new Vector3(0.0300f, 0.0080f, 0.0300f), dark);
                SetPiece(furniture, "Shelf_Block", new Vector3(0.1100f, 0.0150f, 0.1200f), new Vector3(0.0450f, 0.0300f, 0.0700f), wall);
                break;

            case "Kitchen_Module":
                // Arbeitsflaeche, Herdblock, zwei sichtbare Herdplatten
                SetPiece(furniture, "Counter_Cabinet", new Vector3(0.0250f, 0.0090f, 0.0920f), new Vector3(0.1700f, 0.0180f, 0.0550f), wall);
                SetPiece(furniture, "Counter_Top", new Vector3(0.0250f, 0.0200f, 0.0920f), new Vector3(0.1800f, 0.0040f, 0.0610f), ivory);
                SetPiece(furniture, "Hob_Left", new Vector3(-0.0200f, 0.0235f, 0.0920f), new Vector3(0.0450f, 0.0030f, 0.0450f), dark);
                SetPiece(furniture, "Hob_Right", new Vector3(0.0650f, 0.0235f, 0.0920f), new Vector3(0.0450f, 0.0030f, 0.0450f), dark);
                SetPiece(furniture, "Burner_Left", new Vector3(-0.0200f, 0.0260f, 0.0920f), new Vector3(0.0210f, 0.0020f, 0.0210f), wall);
                SetPiece(furniture, "Burner_Right", new Vector3(0.0650f, 0.0260f, 0.0920f), new Vector3(0.0210f, 0.0020f, 0.0210f), wall);
                break;

            case "ChildrenRoom_Module":
                // Bett, Spielzeugwuerfel, Schaukelpferd-Silhouette
                SetPiece(furniture, "Bed_Frame", new Vector3(-0.0320f, 0.0060f, 0.1350f), new Vector3(0.1200f, 0.0120f, 0.1500f), wood);
                SetPiece(furniture, "Bed_Mattress", new Vector3(-0.0320f, 0.0170f, 0.1350f), new Vector3(0.1120f, 0.0100f, 0.1420f), ivory);
                SetPiece(furniture, "Bed_Pillow", new Vector3(-0.0320f, 0.0250f, 0.0800f), new Vector3(0.0650f, 0.0060f, 0.0350f), wall);
                SetPiece(furniture, "Toy_Block", new Vector3(0.0300f, 0.0090f, 0.2650f), new Vector3(0.0180f, 0.0180f, 0.0180f), Mat("ChildYellow"));
                SetPiece(furniture, "Toy_Horse_Rocker", new Vector3(0.0760f, 0.0025f, 0.1250f), new Vector3(0.0780f, 0.0050f, 0.0400f), wall);
                SetPiece(furniture, "Toy_Horse_Body", new Vector3(0.0800f, 0.0155f, 0.1250f), new Vector3(0.0600f, 0.0190f, 0.0260f), wood);
                SetPiece(furniture, "Toy_Horse_Head", new Vector3(0.1060f, 0.0265f, 0.1250f), new Vector3(0.0160f, 0.0170f, 0.0160f), wall);
                SetPiece(furniture, "Toy_Horse_Leg_A", new Vector3(0.0580f, 0.0030f, 0.1150f), new Vector3(0.0080f, 0.0060f, 0.0100f), wood);
                SetPiece(furniture, "Toy_Horse_Leg_B", new Vector3(0.0940f, 0.0030f, 0.1150f), new Vector3(0.0080f, 0.0060f, 0.0100f), wood);
                break;

            case "Bedroom_Module":
                // Doppelbett und Alkoven-Trennwand
                SetPiece(furniture, "DoubleBed_Frame", new Vector3(0.0250f, 0.0070f, 0.1500f), new Vector3(0.1700f, 0.0140f, 0.1600f), wood);
                SetPiece(furniture, "DoubleBed_Mattress", new Vector3(0.0250f, 0.0190f, 0.1500f), new Vector3(0.1620f, 0.0100f, 0.1500f), ivory);
                SetPiece(furniture, "DoubleBed_Pillows", new Vector3(0.0250f, 0.0270f, 0.0880f), new Vector3(0.1400f, 0.0060f, 0.0350f), wall);
                SetPiece(furniture, "Alcove_Partition", new Vector3(0.1100f, 0.0180f, 0.2550f), new Vector3(WallThickness, WallHeight, 0.0850f), wall);
                break;
        }
    }

    // ------------------------------------------------------------------
    // Slotmarker
    // ------------------------------------------------------------------

    /// <summary>
    /// Slotmarker liegen knapp ueber der gemeinsamen Bodenebene.
    /// SlotType, FloorIndex, gridX/gridZ/gridYRotation und SnapRadius bleiben unveraendert.
    /// </summary>
    private static void FixSlots()
    {
        foreach (var slot in UnityEngine.Object.FindObjectsByType<DollhouseRoomSlot>())
        {
            Vector3 slotLocal = slot.transform.localPosition;
            slotLocal.y = MiniFloorY + AnchorHeightAboveFloor; // 0.0200 = Ankerhoehe
            slot.transform.localPosition = slotLocal;

            Transform marker = slot.transform.Find("SlotMarker");
            if (marker != null)
            {
                Vector3 markerLocal = marker.localPosition;
                markerLocal.y = SlotMarkerCenterY - (MiniFloorY + AnchorHeightAboveFloor);
                marker.localPosition = markerLocal;
            }

            LogSet("Slot", $"{slot.name}: Marker-Oberkante = {SlotMarkerCenterY + SlotMarkerThickness * 0.5f:F4} (Boden +{(SlotMarkerCenterY + SlotMarkerThickness * 0.5f) - MiniFloorY:F4}), SnapRadius unveraendert {slot.SnapRadius:F2}.");
        }
    }

    // ------------------------------------------------------------------
    // Flur-Anschluesse
    // ------------------------------------------------------------------

    /// <summary>
    /// Verbindungs- und Socket-Transforms des Flurs auf die gemeinsame Ankerhoehe.
    /// X/Z bleiben unveraendert -> Gridkoordinaten und Slot-Kompatibilitaet unberuehrt.
    /// </summary>
    private static void FixHallwayDoorAnchors()
    {
        var hallway = GameObject.Find("Hallway_Module");
        if (hallway == null)
            return;

        // Flur-Root liegt Board-lokal auf y = 0.0010.
        float rootBoardY = 0.0010f;
        float localY = (MiniFloorY + AnchorHeightAboveFloor) - rootBoardY; // 0.0190

        Transform connections = hallway.transform.Find("ConnectionPoints");
        if (connections != null)
        {
            foreach (Transform connection in connections.Cast<Transform>())
            {
                Vector3 local = connection.localPosition;
                local.y = localY;
                connection.localPosition = local;
            }
        }

        Transform sockets = hallway.transform.Find("DoorSockets");
        if (sockets != null)
        {
            foreach (Transform socket in sockets.Cast<Transform>())
            {
                Vector3 local = socket.localPosition;
                local.y = localY;
                socket.localPosition = local;
            }
        }

        LogSet("Flur-Anschluesse", $"ConnectionPoints und DoorSockets auf Board-Y {MiniFloorY + AnchorHeightAboveFloor:F4} gesetzt (lokal {localY:F4}). X/Z unveraendert.");
    }

    // ------------------------------------------------------------------
    // Torvald-Puppensocket
    // ------------------------------------------------------------------

    /// <summary>
    /// Der Marker des Puppensockets gehoert knapp ueber die Bodenebene.
    /// Die Lage des Sockets relativ zum Boden bleibt so, wie sie war.
    /// </summary>
    private static void FixPuppetSocket()
    {
        var socket = UnityEngine.Object.FindObjectsByType<DollhousePuppetSocket>(FindObjectsInactive.Include).FirstOrDefault();
        if (socket == null)
        {
            LogSet("Puppensocket", "Kein DollhousePuppetSocket gefunden.");
            return;
        }

        // Bisher: Socket 0.1160 ueber der sichtbaren Bodenoberkante (Marker-Marke knapp darueber).
        Vector3 local = socket.transform.localPosition;
        local.y = 0.1160f;
        socket.transform.localPosition = local;

        LogSet("Puppensocket", $"PuppetSocket_Torvald auf Modul-lokal {local.y:F4} (Marker damit 0.0040 ueber dem Boden).");
    }
}
