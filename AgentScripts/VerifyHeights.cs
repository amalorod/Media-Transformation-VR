using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>Messt exakt in Board-lokalen Koordinaten (Board-Y = Board-Hochachse).</summary>
public static class VerifyHeights
{
    static Transform Board;
    static Vector3 BL(Vector3 w) => Board.InverseTransformPoint(w);
    static string F(Vector3 v) => $"({v.x:F4}, {v.y:F4}, {v.z:F4})";

    static void MeshRange(Renderer r, out float minY, out float maxY, out float minX, out float maxX, out float minZ, out float maxZ)
    {
        minY = minX = minZ = float.MaxValue;
        maxY = maxX = maxZ = float.MinValue;
        var mf = r.GetComponent<MeshFilter>();
        Mesh m = mf != null ? mf.sharedMesh : null;
        if (m == null) { var c = BL(r.bounds.center); minY = maxY = c.y; minX = maxX = c.x; minZ = maxZ = c.z; return; }
        var mtx = r.transform.localToWorldMatrix;
        foreach (var v in m.vertices)
        {
            var p = BL(mtx.MultiplyPoint3x4(v));
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.z < minZ) minZ = p.z;
            if (p.z > maxZ) maxZ = p.z;
        }
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            var r = FindDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }

    public static string Main()
    {
        Physics.SyncTransforms();
        var sb = new StringBuilder();
        Board = GameObject.Find("DollhouseBoard").transform;

        sb.AppendLine($"MESSPUNKT: Board-lokale Y-Achse. Ziel MiniFloorY = 0.0130");
        var bv = Board.Find("BoardVisual");
        MeshRange(bv.GetComponent<Renderer>(), out float by0, out float by1, out _, out _, out _, out _);
        sb.AppendLine($"Board-Platte Y {by0:F4}..{by1:F4}");

        // ---------- Flur ----------
        var hall = GameObject.Find("Hallway_Module");
        var hfloor = hall.transform.Find("VisualRoot/Visual_Prototype/Hallway_Floor");
        MeshRange(hfloor.GetComponent<Renderer>(), out float h0, out float h1, out float hx0, out float hx1, out float hz0, out float hz1);
        sb.AppendLine($"\nFLUR Boden      Y {h0:F4}..{h1:F4}   X {hx0:F4}..{hx1:F4}  Z {hz0:F4}..{hz1:F4}   Abweichung zu MiniFloorY: {h1 - 0.0130f:+0.00000;-0.00000}");
        foreach (var r in hall.transform.Find("VisualRoot/Visual_Prototype").GetComponentsInChildren<Renderer>(true).Where(x => x.gameObject.activeInHierarchy))
        {
            MeshRange(r, out float a, out float b, out _, out _, out _, out _);
            sb.AppendLine($"   {r.name,-32} Y {a:F4}..{b:F4}  (Boden+{a - 0.0130f:F4}..+{b - 0.0130f:F4})");
        }

        // ---------- Room_A ----------
        var roomA = GameObject.Find("Room_A");
        sb.AppendLine($"\nROOM_A Root boardLocal {F(BL(roomA.transform.position))}");
        Transform living = roomA.transform.Find("VisualRoot/Visual_Prototype/Living_Room (1)");
        sb.AppendLine($"   Living_Room (1) gefunden: {living != null}  local={F(living.localPosition)}");
        var slabs = roomA.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("Base_Slab")).ToList();
        foreach (var s in slabs)
        {
            MeshRange(s, out float a, out float b, out _, out _, out _, out _);
            sb.AppendLine($"   Base_Slab Y {a:F4}..{b:F4}");
        }
        float raFloorTop = float.MinValue, raFloorBot = float.MaxValue;
        foreach (var r in roomA.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.name.ToLowerInvariant().Contains("floor")) continue;
            MeshRange(r, out float a, out float b, out _, out _, out _, out _);
            raFloorTop = Mathf.Max(raFloorTop, b);
            raFloorBot = Mathf.Min(raFloorBot, a);
        }
        sb.AppendLine($"   Wohnzimmer-Boden Y {raFloorBot:F4}..{raFloorTop:F4}   Abweichung zu MiniFloorY: {raFloorTop - 0.0130f:+0.00000;-0.00000}");
        var colA = roomA.GetComponent<BoxCollider>();
        sb.AppendLine($"   Collider localBottom={colA.center.y - colA.size.y * 0.5f:F4} top={colA.center.y + colA.size.y * 0.5f:F4} size={F(colA.size)}");
        var dsE = FindDeep(roomA.transform, "DoorSocket_East");
        sb.AppendLine($"   DoorSocket_East boardLocal {F(BL(dsE.position))}  (Boden+{BL(dsE.position).y - 0.0130f:F4})");
        foreach (var r in roomA.GetComponentsInChildren<Renderer>(true))
        {
            MeshRange(r, out float a, out float b, out _, out _, out _, out _);
            if (b > raFloorTop + 0.0001f) { }
        }

        // ---------- Flur-Anschluesse ----------
        sb.AppendLine($"\nFLUR-ANSCHLUESSE");
        var conn = hall.transform.Find("ConnectionPoints");
        foreach (Transform c in conn)
        {
            var p = BL(c.position);
            sb.AppendLine($"   {c.name,-28} boardLocal {F(p)}  (Boden+{p.y - 0.0130f:F4})");
        }
        sb.AppendLine($"   X-Spanne Flurboden {hx0:F4}..{hx1:F4}");

        // ---------- Module ----------
        sb.AppendLine($"\nMODULE (Ruhelage + simulierter Snap)");
        var grid = Board.GetComponentInChildren<DollhouseGrid>(true);
        var slots = UnityEngine.Object.FindObjectsByType<DollhouseRoomSlot>();
        foreach (var mn in new[] { "Office_Module", "ChildrenRoom_Module", "Kitchen_Module", "Bedroom_Module" })
        {
            var go = GameObject.Find(mn);
            var room = go.GetComponent<DollhouseRoom>();
            Transform vp = go.transform.Find("VisualRoot/Visual_Prototype");
            var floor = vp.Find("Floor");
            MeshRange(floor.GetComponent<Renderer>(), out float fl0, out float fl1, out _, out _, out _, out _);
            float floorTopLocal = fl1 - BL(go.transform.position).y * 0f; // Module sind unrotiert -> siehe unten
            // Modul-lokale Werte direkt aus dem Transform:
            float ftLocal = floor.localPosition.y + floor.localScale.y * 0.5f;
            float fbLocal = floor.localPosition.y - floor.localScale.y * 0.5f;

            var anchor = room.ModuleSnapAnchor;
            var slot = slots.First(s => s.SlotType.ToString() == room.RequiredSlotType.ToString());
            var st = slot.SnapTransform;
            Quaternion tr = st.rotation * Quaternion.Inverse(anchor.localRotation);
            Vector3 rootPos = st.position - tr * Vector3.Scale(anchor.localPosition, go.transform.localScale);
            float rootBoardY = BL(rootPos).y;

            sb.AppendLine($"\n[{mn}]");
            sb.AppendLine($"   Ruhelage root.worldY={go.transform.position.y:F4}  Bodenunterkante.worldY={go.transform.position.y + fbLocal:F4}  (Sockeloberkante 0.3000)");
            sb.AppendLine($"   Anker local Y={anchor.localPosition.y:F4}   Sockel-Anker boardLocal {F(BL(st.position))}");
            sb.AppendLine($"   SNAP -> root boardY={rootBoardY:F4}  Bodenoberkante boardY={rootBoardY + ftLocal:F4}  (Abweichung {rootBoardY + ftLocal - 0.0130f:+0.00000;-0.00000})  Bodenunterkante boardY={rootBoardY + fbLocal:F4}");
            sb.AppendLine($"   SNAP -> Modul-Tueranker boardY={rootBoardY + anchor.localPosition.y:F4}  (Flur-Anker 0.0200)");
            if (grid.TryGetGridCoordinates(rootPos, room.Width, room.Depth, slot.GridYRotation, out int gx, out int gz))
                sb.AppendLine($"   Grid ({gx},{gz}) vs Slot ({slot.GridX},{slot.GridZ}) -> {(gx == slot.GridX && gz == slot.GridZ ? "OK" : "ABWEICHUNG")}");

            // Vertikale Ausdehnung des Moduls relativ zum Board
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var r in vp.GetComponentsInChildren<Renderer>(true))
            {
                float a = rootBoardY + (r.transform.localPosition.y - r.transform.localScale.y * 0.5f);
                float b = rootBoardY + (r.transform.localPosition.y + r.transform.localScale.y * 0.5f);
                minY = Mathf.Min(minY, a); maxY = Mathf.Max(maxY, b);
            }
            sb.AppendLine($"   Modul belegt boardY {minY:F4}..{maxY:F4}  -> Board-Platte (0.0000) ueberlappt: {(minY < 0f ? "JA" : "nein")}");
        }

        // ---------- Slots ----------
        sb.AppendLine($"\nSLOTMARKER");
        foreach (var s in slots)
        {
            var mr = s.MarkerRenderer;
            if (mr == null) continue;
            MeshRange(mr, out float a, out float b, out float x0, out float x1, out float z0, out float z1);
            sb.AppendLine($"   {s.name,-26} Y {a:F4}..{b:F4} (Boden+{a - 0.0130f:F4}..+{b - 0.0130f:F4})  X {x0:F4}..{x1:F4} Z {z0:F4}..{z1:F4}  sichtbar={mr.enabled}");
        }

        // ---------- Ankerpaarung Flur <-> Module ----------
        sb.AppendLine($"\nANKERPAARUNG (Snap-Ziel)");
        foreach (var s in slots)
        {
            var st = s.SnapTransform;
            string dsName = "DoorSocket_" + s.SlotType;
            Transform ds = hall.transform.Find("DoorSockets/" + dsName);
            var dsc = ds != null ? ds.GetComponent<DoorSocket>() : null;
            float d = ds != null ? Vector3.Distance(st.position, ds.position) : -1f;
            sb.AppendLine($"   {s.name}: Flur-Socket {dsName} {(ds == null ? "FEHLT" : $"Dist={d:F5} side={dsc.Side}")}");
        }

        // ---------- Vertrag: Visual_Prototype frei von Technik ----------
        sb.AppendLine($"\nVISUAL_PROTOTYPE-PRUEFUNG");
        foreach (var rootName in new[] { "Hallway_Module", "Room_A", "Office_Module", "ChildrenRoom_Module", "Kitchen_Module", "Bedroom_Module" })
        {
            var go = GameObject.Find(rootName);
            var vp = go.transform.Find("VisualRoot/Visual_Prototype");
            if (vp == null) { sb.AppendLine($"   {rootName}: kein Visual_Prototype"); continue; }
            var bad = new List<string>();
            foreach (var c in vp.GetComponentsInChildren<Component>(true))
            {
                if (c == null) { bad.Add("MISSING SCRIPT"); continue; }
                if (c is Transform || c is MeshFilter || c is MeshRenderer) continue;
                bad.Add($"{c.GetType().Name}@{c.gameObject.name}");
            }
            sb.AppendLine($"   {rootName}: {(bad.Count == 0 ? "OK (nur Transform/MeshFilter/MeshRenderer)" : "PROBLEM: " + string.Join(", ", bad))}");
            sb.AppendLine($"      aktive Renderer: {vp.GetComponentsInChildren<Renderer>(true).Count(r => r.gameObject.activeInHierarchy)}");
        }

        return sb.ToString();
    }
}
