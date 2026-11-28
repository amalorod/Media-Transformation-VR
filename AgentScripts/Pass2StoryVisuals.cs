using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Pass 2: Tag-1-Storyobjekte visuell lesbarer machen.
///
/// Regeln:
/// - TechnicalRoot, Collider, Interactable und alle Storybedingungen bleiben unveraendert.
/// - Sichtbares kommt ausschliesslich unter VisualRoot/Visual_Prototype.
/// - Das alte sichtbare Primitive wird nur deaktiviert, nie geloescht.
/// - Nur Primitive, wenige gemeinsame Materialien, keine Texturen, keine Downloads.
/// </summary>
public static class Pass2StoryVisuals
{
    private sealed class P
    {
        public string Name;
        public PrimitiveType Kind = PrimitiveType.Cube;
        public Vector3 Pos;
        public Vector3 Scale = Vector3.one;
        public Vector3 Rot;
        public string Mat = "Ivory";
    }

    private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
    private static readonly List<string> Log = new List<string>();
    private static int created;
    private static int reused;

    // Kurzschreiber fuer die Teile-Tabelle
    private static P B(string name, float x, float y, float z, float sx, float sy, float sz, string mat = "Ivory")
        => new P { Name = name, Kind = PrimitiveType.Cube, Pos = new Vector3(x, y, z), Scale = new Vector3(sx, sy, sz), Mat = mat };

    private static P S(string name, float x, float y, float z, float sx, float sy, float sz, string mat = "Ivory")
        => new P { Name = name, Kind = PrimitiveType.Sphere, Pos = new Vector3(x, y, z), Scale = new Vector3(sx, sy, sz), Mat = mat };

    private static P C(string name, float x, float y, float z, float sx, float sy, float sz, string mat = "Ivory")
        => new P { Name = name, Kind = PrimitiveType.Cylinder, Pos = new Vector3(x, y, z), Scale = new Vector3(sx, sy, sz), Mat = mat };

    // ------------------------------------------------------------------

    public static string Main()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var tag1 = GameObject.Find("_TAG1");
        if (tag1 == null)
            return "FEHLER: _TAG1 nicht gefunden.";

        Undo.RegisterFullObjectHierarchyUndo(tag1, "Tag-1-Visuals aufwerten");

        foreach (string name in new[] { "Wood", "Ivory", "PrototypeDark", "OfficeNavy", "KitchenTerracotta", "ChildSage", "ChildYellow", "BedroomBlue" })
            Mat(name);

        BuildTable();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Log.Insert(0, $"Tag-1-Visuals: {created} neue Teile erzeugt, {reused} vorhandene Teile aktualisiert.");
        return string.Join("\n", Log);
    }

    private static Material Mat(string name)
    {
        if (Materials.TryGetValue(name, out Material cached))
            return cached;

        Material material = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Project/Materials/PrototypeVisuals/{name}.mat");
        if (material == null)
            Debug.LogWarning($"[Pass2] Material fehlt: {name}");
        Materials[name] = material;
        return material;
    }

    // ------------------------------------------------------------------

    private static void Build(string path, params P[] pieces)
    {
        Transform target = FindPath(path);
        if (target == null)
        {
            Log.Add($"[FEHLT] {path}");
            return;
        }

        GameObject go = target.gameObject;
        int rootRenderersDeactivated = 0;

        // Altes sichtbares Primitive am TechnicalRoot nur deaktivieren.
        var ownRenderer = go.GetComponent<MeshRenderer>();
        if (ownRenderer != null && ownRenderer.enabled)
        {
            ownRenderer.enabled = false;
            rootRenderersDeactivated++;
        }

        Transform visualRoot = EnsureChild(target, "VisualRoot");
        Transform prototype = EnsureChild(visualRoot, "Visual_Prototype");

        foreach (P piece in pieces)
        {
            Transform existing = prototype.Find(piece.Name);
            GameObject pieceObject;

            if (existing != null)
            {
                pieceObject = existing.gameObject;
                reused++;
            }
            else
            {
                pieceObject = CreatePrimitive(piece.Kind);
                pieceObject.name = piece.Name;
                Undo.RegisterCreatedObjectUndo(pieceObject, "Storyobjekt-Teil erzeugen");
                pieceObject.transform.SetParent(prototype, false);
                created++;
            }

            Collider collider = pieceObject.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);

            pieceObject.transform.localPosition = piece.Pos;
            pieceObject.transform.localRotation = Quaternion.Euler(piece.Rot);
            pieceObject.transform.localScale = piece.Scale;
            pieceObject.SetActive(true);

            var renderer = pieceObject.GetComponent<MeshRenderer>();
            if (renderer == null)
                renderer = pieceObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Mat(piece.Mat);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        Log.Add($"[OK] {path}: {pieces.Length} Teile, Root-Primitive deaktiviert: {rootRenderersDeactivated}");
    }

    private static GameObject CreatePrimitive(PrimitiveType kind)
    {
        return GameObject.CreatePrimitive(kind);
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            return existing;
        }

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "VisualRoot erzeugen");
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static Transform FindPath(string path)
    {
        string[] segments = path.Split('/');
        GameObject current = GameObject.Find(segments[0]);
        if (current == null)
            return null;

        Transform t = current.transform;
        for (int i = 1; i < segments.Length; i++)
        {
            t = t.Find(segments[i]);
            if (t == null)
                return null;
        }

        return t;
    }

    // ------------------------------------------------------------------
    // Figuren-Silhouette (gleiche Bauteile wie die vorhandene Torvald-Puppe)
    // ------------------------------------------------------------------

    private static List<P> Humanoid(string torso, string legs, string shoes, string skin, bool stand)
    {
        var list = new List<P>();

        if (stand)
            list.Add(C("Stand_Base", 0f, -0.89f, 0f, 0.68f, 0.04f, 0.62f, "Wood"));

        list.Add(B("Leg_Left", -0.13f, -0.49f, 0f, 0.17f, 0.48f, 0.21f, legs));
        list.Add(B("Leg_Right", 0.13f, -0.49f, 0f, 0.17f, 0.48f, 0.21f, legs));
        list.Add(B("Shoe_Left", -0.13f, -0.79f, -0.035f, 0.22f, 0.10f, 0.31f, shoes));
        list.Add(B("Shoe_Right", 0.13f, -0.79f, -0.035f, 0.22f, 0.10f, 0.31f, shoes));
        list.Add(B("Torso", 0f, 0.015f, 0f, 0.43f, 0.62f, 0.29f, torso));
        list.Add(B("Neck", 0f, 0.36f, 0f, 0.13f, 0.12f, 0.14f, skin));
        list.Add(S("Head", 0f, 0.60f, 0f, 0.38f, 0.40f, 0.36f, skin));
        list.Add(B("Arm_Left", -0.31f, 0.07f, 0f, 0.17f, 0.48f, 0.19f, torso));
        list.Add(B("Arm_Right", 0.31f, 0.07f, 0f, 0.17f, 0.48f, 0.19f, torso));
        list.Add(B("Hand_Left", -0.31f, -0.20f, 0f, 0.14f, 0.12f, 0.16f, skin));
        list.Add(B("Hand_Right", 0.31f, -0.20f, 0f, 0.14f, 0.12f, 0.16f, skin));

        return list;
    }

    /// <summary>Kleine Haltungsfigur fuer Auswahlpunkte (Kopf, Oberkoerper, zwei Arme).</summary>
    private static List<P> Posture(string cloth, string skin, float armY, float armZ, float armSpread, string armMat = null)
    {
        var list = new List<P>
        {
            B("Body", 0f, -0.12f, 0f, 0.34f, 0.62f, 0.26f, cloth),
            S("Head", 0f, 0.34f, 0f, 0.30f, 0.30f, 0.30f, skin),
            B("Arm_Left", -armSpread, armY, armZ, 0.13f, 0.44f, 0.13f, armMat ?? cloth),
            B("Arm_Right", armSpread, armY, armZ, 0.13f, 0.44f, 0.13f, armMat ?? cloth),
        };
        return list;
    }

    // ------------------------------------------------------------------

    private static void BuildTable()
    {
        // ---------------- Bereits ausgearbeitete Objekte ----------------
        // Diese besitzen schon einen VisualRoot; hier wird nur noch das grobe
        // Platzhalter-Primitive am TechnicalRoot deaktiviert, damit die
        // ausgearbeitete Silhouette sichtbar wird.
        Build("_TAG1/PUPPETS/Torvald_Puppe");
        Build("_TAG1/STORY_ITEMS/Shopping_Bag_A");
        Build("_TAG1/STORY_ITEMS/Shopping_Bag_B");

        // ---------------- Puppen ----------------
        Build("_TAG1/PUPPETS/Bob_Puppe", Humanoid("Wood", "PrototypeDark", "PrototypeDark", "Wood", true).ToArray());
        Build("_TAG1/PUPPETS/Linde_Puppe", Humanoid("ChildSage", "BedroomBlue", "PrototypeDark", "Wood", true).ToArray());
        Build("_TAG1/PUPPETS/Puppet_Nora_Complete", Humanoid("Ivory", "BedroomBlue", "PrototypeDark", "Wood", true).ToArray());

        // ---------------- Nora-Puppenteile ----------------
        Build("_TAG1/PUPPETS/Torso_Teil",
            B("Torso_Body", 0f, 0f, 0f, 0.80f, 0.90f, 0.62f, "Ivory"),
            B("Collar", 0f, 0.44f, 0f, 0.52f, 0.12f, 0.44f, "ChildSage"),
            B("Waist_Sash", 0f, -0.34f, 0f, 0.84f, 0.14f, 0.66f, "BedroomBlue"));

        Build("_TAG1/PUPPETS/Kopf_Puppe_Teil",
            S("Head", 0f, 0f, 0f, 0.95f, 0.95f, 0.95f, "Wood"),
            B("Hair", 0f, 0.28f, 0.05f, 0.92f, 0.46f, 0.92f, "PrototypeDark"),
            B("Face", 0f, -0.10f, -0.44f, 0.52f, 0.34f, 0.12f, "Ivory"));

        Build("_TAG1/PUPPETS/Arm_Puppe_Teil",
            B("Upper_Arm", -0.24f, 0f, 0f, 0.52f, 0.90f, 0.90f, "Ivory"),
            B("Sleeve", -0.46f, 0f, 0f, 0.14f, 0.96f, 0.96f, "BedroomBlue"),
            B("Hand", 0.28f, 0f, 0f, 0.34f, 0.86f, 0.86f, "Wood"));

        Build("_TAG1/PUPPETS/Beine_Teil",
            B("Leg_Left", -0.24f, -0.10f, 0f, 0.40f, 0.82f, 0.70f, "BedroomBlue"),
            B("Leg_Right", 0.24f, -0.10f, 0f, 0.40f, 0.82f, 0.70f, "BedroomBlue"),
            B("Skirt", 0f, 0.26f, 0f, 0.90f, 0.46f, 0.88f, "Ivory"));

        // ---------------- Figuren (Storyboard-Repraesentation) ----------------
        Build("_TAG1/CHARACTER_PLACEHOLDERS/Figur_AnneMarie", Humanoid("Ivory", "BedroomBlue", "PrototypeDark", "Wood", false).ToArray());
        Build("_TAG1/CHARACTER_PLACEHOLDERS/Figur_Bob", Humanoid("Wood", "PrototypeDark", "PrototypeDark", "Wood", false).ToArray());
        Build("_TAG1/CHARACTER_PLACEHOLDERS/Figur_Krogstad", Humanoid("PrototypeDark", "OfficeNavy", "PrototypeDark", "Wood", false).ToArray());
        Build("_TAG1/CHARACTER_PLACEHOLDERS/Figur_Linde", Humanoid("ChildSage", "BedroomBlue", "PrototypeDark", "Wood", false).ToArray());
        Build("_TAG1/CHARACTER_PLACEHOLDERS/Figur_Rank", Humanoid("OfficeNavy", "PrototypeDark", "PrototypeDark", "Wood", false).ToArray());

        // ---------------- Story-Objekte ----------------
        Build("_TAG1/STORY_ITEMS/Schuldschein",
            B("Paper", 0f, 0f, 0f, 0.96f, 0.90f, 0.96f, "Ivory"),
            B("Ink_Line_1", 0f, 0.45f, -0.18f, 0.72f, 0.10f, 0.10f, "PrototypeDark"),
            B("Ink_Line_2", 0f, 0.45f, 0.06f, 0.72f, 0.10f, 0.10f, "PrototypeDark"),
            B("Seal", 0f, 0.45f, 0.34f, 0.20f, 0.10f, 0.18f, "KitchenTerracotta"));

        Build("_TAG1/STORY_ITEMS/Papas_Taschenuhr",
            C("Watch_Case", 0f, 0f, 0f, 0.90f, 0.16f, 0.90f, "ChildYellow"),
            C("Watch_Face", 0f, 0.10f, 0f, 0.62f, 0.03f, 0.62f, "Ivory"),
            B("Watch_Hand", 0f, 0.14f, 0.08f, 0.06f, 0.04f, 0.40f, "PrototypeDark"),
            C("Watch_Bow", 0f, 0.10f, 0.48f, 0.20f, 0.10f, 0.20f, "ChildYellow"));

        Build("_TAG1/STORY_ITEMS/Ehering",
            C("Ring_Band", 0f, 0f, 0f, 0.95f, 0.28f, 0.95f, "ChildYellow"),
            C("Ring_Opening", 0f, 0.06f, 0f, 0.52f, 0.30f, 0.52f, "PrototypeDark"));

        // ---------------- Auswahlobjekte (Optionen) ----------------
        Build("_TAG1/CHOICES/Tag1_Choice_Essen",
            C("Plate", 0f, -0.28f, 0f, 0.80f, 0.12f, 0.80f, "Ivory"),
            C("Cake", 0f, 0.02f, 0f, 0.54f, 0.20f, 0.54f, "KitchenTerracotta"),
            S("Topping", 0f, 0.30f, 0f, 0.24f, 0.24f, 0.24f, "ChildYellow"));

        Build("_TAG1/CHOICES/Tag1_Choice_Verbrennen",
            B("Flame_Low", 0f, -0.22f, 0f, 0.44f, 0.40f, 0.44f, "KitchenTerracotta"),
            B("Flame_Mid", 0f, 0.06f, 0f, 0.30f, 0.34f, 0.30f, "ChildYellow"),
            B("Flame_Top", 0f, 0.32f, 0f, 0.16f, 0.24f, 0.16f, "Ivory"));

        Build("_TAG1/CHOICES/Tag1_Choice_Verstecken",
            B("Chest", 0f, -0.12f, 0f, 0.72f, 0.60f, 0.72f, "Wood"),
            B("Chest_Lid", 0f, 0.28f, 0f, 0.80f, 0.14f, 0.80f, "PrototypeDark"),
            B("Chest_Lock", 0f, 0.06f, -0.38f, 0.16f, 0.20f, 0.10f, "ChildYellow"));

        Build("_TAG1/CHOICES/Haltung_Flehend",
            Posture("Ivory", "Wood", -0.26f, -0.34f, 0.32f).ToArray());

        Build("_TAG1/CHOICES/Haltung_Schweigend",
            Posture("Ivory", "Wood", -0.10f, -0.06f, 0.30f).Concat(new[]
            {
                B("Hand_Over_Mouth", 0f, 0.30f, -0.30f, 0.26f, 0.16f, 0.12f, "Wood"),
            }).ToArray());

        Build("_TAG1/CHOICES/Haltung_Trotzig",
            Posture("Ivory", "Wood", 0.02f, -0.06f, 0.30f).Concat(new[]
            {
                B("Crossed_Arms", 0f, 0.02f, -0.28f, 0.62f, 0.14f, 0.14f, "Ivory"),
            }).ToArray());

        Build("_TAG1/CHOICES/Wahrheit_Wahl",
            S("Sun_Core", 0f, -0.05f, 0f, 0.44f, 0.44f, 0.44f, "ChildYellow"),
            B("Ray_Vertical", 0f, 0.30f, 0f, 0.12f, 0.44f, 0.12f, "ChildYellow"),
            B("Ray_Horizontal", 0f, -0.05f, 0f, 0.94f, 0.12f, 0.12f, "ChildYellow"),
            B("Ray_Depth", 0f, -0.05f, 0f, 0.12f, 0.12f, 0.94f, "ChildYellow"));

        Build("_TAG1/CHOICES/Luege_Wahl",
            S("Dark_Core", 0f, -0.05f, 0f, 0.46f, 0.46f, 0.46f, "PrototypeDark"),
            B("Crack", 0.06f, 0.06f, -0.20f, 0.70f, 0.10f, 0.12f, "Ivory"),
            B("Crack_Cross", -0.06f, 0.06f, -0.20f, 0.44f, 0.10f, 0.12f, "Ivory"));

        Build("_TAG1/CHOICES/Linde_Wahl_Alles",
            Posture("ChildSage", "Wood", -0.24f, -0.30f, 0.36f).ToArray());

        Build("_TAG1/CHOICES/Linde_Wahl_Andeuten",
            Posture("ChildSage", "Wood", -0.02f, -0.16f, 0.30f).Concat(new[]
            {
                B("Pointing_Arm", 0.34f, -0.02f, -0.30f, 0.44f, 0.12f, 0.12f, "ChildSage"),
            }).ToArray());

        Build("_TAG1/CHOICES/Linde_Wahl_Schweigen",
            Posture("ChildSage", "Wood", -0.08f, -0.06f, 0.30f).Concat(new[]
            {
                B("Seal", 0f, 0.30f, -0.30f, 0.26f, 0.16f, 0.12f, "PrototypeDark"),
            }).ToArray());

        Build("_TAG1/CHOICES/Versteck_1",
            C("Barrel", 0f, 0.02f, 0f, 0.66f, 0.44f, 0.66f, "Wood"),
            C("Barrel_Band_Top", 0f, 0.22f, 0f, 0.72f, 0.04f, 0.72f, "PrototypeDark"),
            C("Barrel_Band_Bottom", 0f, -0.22f, 0f, 0.72f, 0.04f, 0.72f, "PrototypeDark"));

        Build("_TAG1/CHOICES/Versteck_2",
            B("Crate", 0f, 0f, 0f, 0.78f, 0.78f, 0.78f, "Wood"),
            B("Crate_Band", 0f, -0.06f, 0f, 0.86f, 0.12f, 0.86f, "PrototypeDark"));

        Build("_TAG1/CHOICES/Versteck_3",
            B("Chest", 0f, -0.14f, 0f, 0.78f, 0.56f, 0.70f, "Wood"),
            B("Chest_Lid", 0f, 0.22f, 0f, 0.84f, 0.16f, 0.76f, "PrototypeDark"));

        Build("_TAG1/CHOICES/Abend_Bett",
            B("Bed_Frame", 0f, -0.26f, 0f, 0.84f, 0.26f, 0.92f, "Wood"),
            B("Mattress", 0f, -0.04f, 0f, 0.76f, 0.20f, 0.84f, "Ivory"),
            B("Pillow", 0f, 0.14f, -0.28f, 0.52f, 0.14f, 0.22f, "BedroomBlue"));

        Build("_TAG1/CHOICES/Rank_Mantel",
            B("Coat_Body", 0f, -0.06f, 0f, 0.62f, 0.84f, 0.44f, "OfficeNavy"),
            B("Coat_Collar", 0f, 0.42f, 0f, 0.54f, 0.16f, 0.40f, "PrototypeDark"),
            B("Coat_Lapel", 0f, 0.10f, -0.24f, 0.10f, 0.52f, 0.06f, "PrototypeDark"));

        Build("_TAG1/CHOICES/Rebellion_Aufrichten",
            B("Body", 0f, -0.10f, 0f, 0.34f, 0.58f, 0.26f, "KitchenTerracotta"),
            S("Head", 0f, 0.34f, 0f, 0.30f, 0.30f, 0.30f, "Wood"),
            B("Arm_Raised_Left", -0.34f, 0.24f, 0f, 0.13f, 0.52f, 0.13f, "KitchenTerracotta"),
            B("Arm_Raised_Right", 0.34f, 0.24f, 0f, 0.13f, 0.52f, 0.13f, "KitchenTerracotta"));

        Build("_TAG1/CHOICES/Papier_Rueckblende_B",
            B("Sheet", 0f, 0f, 0f, 0.90f, 0.94f, 0.08f, "Ivory"),
            B("Line_1", 0f, 0.22f, -0.48f, 0.62f, 0.08f, 0.08f, "PrototypeDark"),
            B("Line_2", 0f, 0.00f, -0.48f, 0.62f, 0.08f, 0.08f, "PrototypeDark"),
            B("Line_3", 0f, -0.22f, -0.48f, 0.62f, 0.08f, 0.08f, "PrototypeDark"));

        Build("_TAG1/CHOICES/Tag1_EndInteractable",
            B("Door_Frame", 0f, 0f, 0f, 0.92f, 0.94f, 0.16f, "Wood"),
            B("Door_Leaf", 0f, 0f, -0.08f, 0.64f, 0.78f, 0.08f, "Ivory"),
            S("Door_Knob", 0.30f, -0.06f, -0.20f, 0.18f, 0.18f, 0.18f, "ChildYellow"));

        // ---------------- Chore-Objekte ----------------
        Build("_TAG1/CHORES/Chore_Blumen",
            C("Flower_Pot", 0f, -0.30f, 0f, 0.52f, 0.10f, 0.52f, "KitchenTerracotta"),
            B("Stem", 0f, 0.02f, 0f, 0.08f, 0.52f, 0.08f, "ChildSage"),
            S("Bloom", 0f, 0.36f, 0f, 0.32f, 0.30f, 0.32f, "ChildYellow"),
            B("Leaf", 0.22f, -0.02f, 0f, 0.30f, 0.10f, 0.20f, "ChildSage"));

        Build("_TAG1/CHORES/Chore_Kartoffeln",
            C("Basket", 0f, -0.16f, 0f, 0.70f, 0.18f, 0.70f, "Wood"),
            S("Potato_A", -0.16f, 0.14f, 0.06f, 0.26f, 0.22f, 0.26f, "ChildSage"),
            S("Potato_B", 0.16f, 0.14f, -0.08f, 0.26f, 0.22f, 0.26f, "ChildSage"),
            S("Potato_C", 0f, 0.24f, -0.14f, 0.24f, 0.20f, 0.24f, "ChildSage"));

        Build("_TAG1/CHORES/Chore_Kinderzimmer",
            B("Toy_Block_A", -0.20f, -0.20f, 0.12f, 0.38f, 0.38f, 0.38f, "ChildYellow"),
            B("Toy_Block_B", 0.16f, -0.20f, -0.14f, 0.38f, 0.38f, 0.38f, "KitchenTerracotta"),
            B("Toy_Block_C", -0.02f, 0.16f, -0.02f, 0.38f, 0.38f, 0.38f, "ChildSage"));

        Build("_TAG1/CHORES/Chore_Staub",
            B("Duster_Handle", 0f, -0.16f, 0f, 0.10f, 0.62f, 0.10f, "Wood"),
            B("Duster_Head", 0f, 0.28f, 0f, 0.48f, 0.30f, 0.30f, "Ivory"),
            S("Dust_Mote_A", 0.28f, 0.10f, 0.12f, 0.12f, 0.12f, 0.12f, "ChildYellow"),
            S("Dust_Mote_B", -0.24f, 0.02f, -0.10f, 0.10f, 0.10f, 0.10f, "ChildYellow"));

        // ---------------- Raetsel-Objekte ----------------
        Build("_TAG1/PUZZLES_AND_INTERACTIONS/Schatulle",
            B("Casket_Body", 0f, -0.16f, 0f, 0.90f, 0.58f, 0.86f, "Wood"),
            B("Casket_Lid", 0f, 0.24f, 0f, 0.96f, 0.20f, 0.92f, "PrototypeDark"),
            B("Casket_Lock", 0f, 0.04f, -0.46f, 0.16f, 0.24f, 0.10f, "ChildYellow"));

        Build("_TAG1/PUZZLES_AND_INTERACTIONS/Code_Ziffer_0",
            B("Plate", 0f, 0f, 0f, 0.90f, 0.86f, 0.34f, "Ivory"),
            B("Ring_Frame", 0f, 0f, -0.42f, 0.62f, 0.58f, 0.08f, "OfficeNavy"));

        Build("_TAG1/PUZZLES_AND_INTERACTIONS/Code_Ziffer_2",
            B("Plate", 0f, 0f, 0f, 0.90f, 0.86f, 0.34f, "Ivory"),
            B("Bar_Top", 0f, 0.22f, -0.42f, 0.60f, 0.14f, 0.08f, "OfficeNavy"),
            B("Bar_Bottom", 0f, -0.22f, -0.42f, 0.60f, 0.14f, 0.08f, "OfficeNavy"));

        Build("_TAG1/PUZZLES_AND_INTERACTIONS/Code_Ziffer_9",
            B("Plate", 0f, 0f, 0f, 0.90f, 0.86f, 0.34f, "Ivory"),
            B("Pip_A", -0.20f, 0.20f, -0.42f, 0.18f, 0.18f, 0.08f, "OfficeNavy"),
            B("Pip_B", 0.20f, 0.20f, -0.42f, 0.18f, 0.18f, 0.08f, "OfficeNavy"),
            B("Pip_C", -0.20f, -0.20f, -0.42f, 0.18f, 0.18f, 0.08f, "OfficeNavy"),
            B("Pip_D", 0.20f, -0.20f, -0.42f, 0.18f, 0.18f, 0.08f, "OfficeNavy"));

        Build("_TAG1/PUZZLES_AND_INTERACTIONS/Klavier_1",
            B("White_Key", -0.14f, 0f, 0f, 0.68f, 0.92f, 0.90f, "Ivory"),
            B("Black_Key", 0.10f, 0.24f, 0f, 0.26f, 0.44f, 0.92f, "PrototypeDark"));

        Build("_TAG1/PUZZLES_AND_INTERACTIONS/Klavier_2",
            B("White_Key", -0.14f, 0f, 0f, 0.68f, 0.92f, 0.90f, "Ivory"),
            B("Black_Key_A", -0.02f, 0.24f, 0f, 0.22f, 0.44f, 0.92f, "PrototypeDark"),
            B("Black_Key_B", 0.30f, 0.24f, 0f, 0.22f, 0.44f, 0.92f, "PrototypeDark"));

        Build("_TAG1/PUZZLES_AND_INTERACTIONS/Klavier_3",
            B("White_Key", -0.14f, 0f, 0f, 0.68f, 0.92f, 0.90f, "Ivory"),
            B("Black_Key_A", -0.10f, 0.24f, 0f, 0.18f, 0.44f, 0.92f, "PrototypeDark"),
            B("Black_Key_B", 0.14f, 0.24f, 0f, 0.18f, 0.44f, 0.92f, "PrototypeDark"),
            B("Black_Key_C", 0.38f, 0.24f, 0f, 0.18f, 0.44f, 0.92f, "PrototypeDark"));

        Build("_TAG1/PUZZLES_AND_INTERACTIONS/Kerze_1",
            C("Candle", 0f, -0.22f, 0f, 0.80f, 0.34f, 0.80f, "Ivory"),
            B("Wick", 0f, 0.16f, 0f, 0.12f, 0.16f, 0.12f, "PrototypeDark"),
            S("Flame", 0f, 0.38f, 0f, 0.36f, 0.42f, 0.36f, "ChildYellow"));

        Build("_TAG1/PUZZLES_AND_INTERACTIONS/Kerze_2",
            C("Candle", 0f, -0.22f, 0f, 0.80f, 0.34f, 0.80f, "ChildYellow"),
            B("Wick", 0f, 0.16f, 0f, 0.12f, 0.16f, 0.12f, "PrototypeDark"),
            S("Flame", 0f, 0.38f, 0f, 0.36f, 0.42f, 0.36f, "KitchenTerracotta"));

        Build("_TAG1/PUZZLES_AND_INTERACTIONS/Kerze_3",
            C("Candle", 0f, -0.22f, 0f, 0.80f, 0.34f, 0.80f, "ChildSage"),
            B("Wick", 0f, 0.16f, 0f, 0.12f, 0.16f, 0.12f, "PrototypeDark"),
            S("Flame", 0f, 0.38f, 0f, 0.36f, 0.42f, 0.36f, "Ivory"));

        Build("_TAG1/PUZZLES_AND_INTERACTIONS/Tuer_Schluessel_Haken",
            B("Hook_Board", 0f, 0.36f, 0f, 0.92f, 0.24f, 0.30f, "Wood"),
            B("Hook_Pin", 0f, 0.02f, -0.10f, 0.12f, 0.48f, 0.12f, "PrototypeDark"),
            C("Key_Ring", 0f, -0.34f, -0.10f, 0.36f, 0.10f, 0.36f, "ChildYellow"));

        Build("_TAG1/PUZZLES_AND_INTERACTIONS/Schublade_Arm",
            B("Drawer_Front", 0f, 0f, -0.30f, 0.90f, 0.86f, 0.40f, "Wood"),
            B("Drawer_Handle", 0f, 0f, -0.56f, 0.40f, 0.14f, 0.12f, "PrototypeDark"),
            B("Content_Arm", 0f, 0.06f, 0.16f, 0.62f, 0.34f, 0.44f, "Ivory"));

        Build("_TAG1/PUZZLES_AND_INTERACTIONS/Schublade_Bankunterlagen",
            B("Drawer_Front", 0f, 0f, -0.30f, 0.90f, 0.86f, 0.40f, "Wood"),
            B("Drawer_Handle", 0f, 0f, -0.56f, 0.40f, 0.14f, 0.12f, "PrototypeDark"),
            B("Content_Papers", 0f, 0.10f, 0.16f, 0.72f, 0.30f, 0.50f, "Ivory"));

        Build("_TAG1/PUZZLES_AND_INTERACTIONS/Schublade_Schluessel",
            B("Drawer_Front", 0f, 0f, -0.30f, 0.90f, 0.86f, 0.40f, "Wood"),
            B("Drawer_Handle", 0f, 0f, -0.56f, 0.40f, 0.14f, 0.12f, "PrototypeDark"),
            B("Content_Key", 0f, 0.14f, 0.12f, 0.22f, 0.22f, 0.56f, "ChildYellow"));
    }
}
