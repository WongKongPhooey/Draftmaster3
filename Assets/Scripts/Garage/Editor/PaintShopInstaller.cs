using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Adds the paint shop to the open TeamGarage scene without rebuilding it: the Paint Scheme Designer NPC
// beside the car, and TeamCarDisplay on the TeamCar so the shop car wears the player's paint job.
// Idempotent — run it again and it only fills in what's missing, never moves or replaces what is there.
// Kept out of TeamGarageBuilder's "GarageContent" root, so a full rebuild of the room leaves it alone.
//
// Menu: Draftmaster > Team HQ > Install Paint Shop In Open Scene.
public static class PaintShopInstaller
{
    const string ArtDir = "Assets/Art/Garage";
    const string DiscPath = ArtDir + "/GarageDisc.png";
    const string MatPath = ArtDir + "/GarageUnlit.mat";
    const string RootName = "PaintShop";

    [MenuItem("Draftmaster/Team HQ/Install Paint Shop In Open Scene")]
    static void Install()
    {
        var scene = EditorSceneManager.GetActiveScene();
        int changes = 0;

        // --- the designer ---
        if (Object.FindFirstObjectByType<PaintShopNPC>(FindObjectsInactive.Include) == null)
        {
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Install Paint Shop");
            }

            var go = new GameObject("PaintSchemeDesigner");
            Undo.RegisterCreatedObjectUndo(go, "Install Paint Shop");
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(5.5f, -4f, 0f);       // beside the car pad, clear of the exit door

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DiscPath);
            sr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            sr.color = new Color(0.90f, 0.45f, 0.85f);                  // the one in overalls covered in paint
            sr.sortingOrder = 20;
            go.AddComponent<CircleCollider2D>().radius = 0.45f;

            var npc = go.AddComponent<PaintShopNPC>();
            npc.speakerName = "Paint Scheme Designer";
            npc.lines = new[]
            {
                "Back for a new look? The booth's warm.",
                "Pick your base coat and a livery, then tell me whose name goes where.",
            };
            npc.interactRange = 2.4f;
            LocationTitle.Attach(go, "PAINT SCHEME DESIGNER", 4.5f, "Paint and sponsors");
            changes++;
        }

        // --- the car on the floor wears the player's paint ---
        var car = GameObject.Find("TeamCar");
        if (car != null && car.GetComponent<TeamCarDisplay>() == null && car.GetComponent<SpriteRenderer>() != null)
        {
            Undo.AddComponent<TeamCarDisplay>(car);
            changes++;
        }
        else if (car == null) Debug.LogWarning("Install Paint Shop: no 'TeamCar' in this scene — the shop car won't show the paint job.");

        // --- the door now leads back to the road (Team HQ is reached from the travel map) ---
        var door = Object.FindFirstObjectByType<SceneDoorInteractable>(FindObjectsInactive.Include);
        if (door != null)
        {
            var title = door.GetComponent<LocationTitle>();
            if (title != null && title.subtitle != "Back on the road")
            {
                Undo.RecordObject(title, "Install Paint Shop");
                title.subtitle = "Back on the road";
                changes++;
            }
        }

        if (changes > 0) EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[PaintShop] Install Paint Shop: {changes} change(s) in {scene.name}. Save the scene to keep them.");
    }
}
