using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Dresses the Team HQ crew (the three RoleStations and the Paint Scheme Designer) as paper-doll people out of
// the same part library the paddock crowd is built from, replacing the placeholder coloured discs.
//
// The outfit is rolled once from a seed per character and stored as an authored outfit on a `Look` child, so
// each crew member is the same person every visit and can be re-dressed by hand in the inspector. The layers
// are built here for the editor view; NPCLayeredAppearance.Start rebuilds them from the saved outfit at play.
// Idempotent — a station already wearing a Look is left alone.
//
// Menu: Draftmaster > Team HQ > Dress Crew In Open Scene. TeamGarageBuilder and PaintShopInstaller call
// Dress() on what they create, so a rebuilt room comes out dressed.
public static class GarageCrewDresser
{
    const string MatPath = "Assets/Art/Garage/GarageUnlit.mat";
    const string LibraryPath = "NPC/NPCPartLibrary";   // Resources
    const int SortingOrder = 20;                        // same slot the discs drew in, above the floor props

    [MenuItem("Draftmaster/Team HQ/Dress Crew In Open Scene")]
    static void DressAll()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Dress Crew: stop Play Mode first — anything dressed now is lost on exit."); return; }
        int dressed = 0;
        foreach (var npc in Object.FindObjectsByType<NPCInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!(npc is RoleStation) && !(npc is PaintShopNPC)) continue;
            if (Dress(npc.gameObject)) dressed++;
        }
        if (dressed > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"Dress Crew: dressed {dressed} crew member(s). Save the scene to keep them.");
    }

    // Swap `go`'s disc sprite for a paper-doll body. Returns false if it was already dressed or there is no
    // part art to dress it in (the disc is then left in place so the station never goes invisible).
    public static bool Dress(GameObject go)
    {
        if (go == null || go.transform.Find(NPCFactory.LookChild) != null) return false;

        var library = Resources.Load<NPCPartLibrary>(LibraryPath);
        if (library == null) { Debug.LogWarning($"Dress Crew: no NPCPartLibrary at Resources/{LibraryPath}."); return false; }

        var lookGo = new GameObject(NPCFactory.LookChild);
        Undo.RegisterCreatedObjectUndo(lookGo, "Dress Crew");
        lookGo.transform.SetParent(go.transform, false);

        var doll = lookGo.AddComponent<NPCLayeredAppearance>();
        doll.library = library;
        doll.sortingLayerName = "Default";   // the room's floor, car and desk all draw on Default
        doll.baseSortingOrder = SortingOrder;
        doll.layerMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatPath);

        // Seeded off the object's name so each crew member rolls the same person on every rebuild.
        if (!doll.Build(Animator.StringToHash(go.name)))
        {
            Undo.DestroyObjectImmediate(lookGo);
            Debug.LogWarning("Dress Crew: the part library has no art — leaving " + go.name + " as a disc.");
            return false;
        }
        doll.authoredOutfit = doll.WornOutfit();
        doll.useAuthoredOutfit = true;

        float frameWorldH = Mathf.Max(0.01f, library.frameHeight / Mathf.Max(1f, library.pixelsPerUnit));
        lookGo.transform.localScale = Vector3.one * (PitCrewSpawner.OnFootPersonHeight / frameWorldH);

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) Undo.DestroyObjectImmediate(sr);

        // The disc's 0.45 m collider was sized to the placeholder; a person is about half that across.
        var col = go.GetComponent<CircleCollider2D>();
        if (col != null) { Undo.RecordObject(col, "Dress Crew"); col.radius = 0.25f; }
        return true;
    }
}
