using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Draftmaster > Travel Map > Build Roadside Shop Scene — makes Assets/Scenes/Landmark.unity, the one on-foot
// scene every satnav stop loads (LandmarkLoader.SceneName), and adds it to the build. It holds no shop: just
// the camera, the walking player (the same rig as Team HQ, TeamGarageBuilder) and the LandmarkDirector, which
// builds the stop's interior from its layout data at runtime. So there's nothing hand-authored in it and
// re-running is safe. Built additively and closed again — whatever scene you have open is left alone.
public static class RoadsideShopSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Landmark.unity";
    const string MatPath = "Assets/Art/Garage/GarageUnlit.mat";
    const string ControlsPath = "Assets/Input/PlayerControl.inputactions";
    const string PlayerSpritePath = "Assets/Sprites/Walking/walk.png";
    const string PlayerAnimatorPath = "Assets/Animation/OnFoot/PlayerOnFoot.controller";

    [MenuItem("Draftmaster/Travel Map/Build Roadside Shop Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Build Roadside Shop Scene: not in Play Mode.");
            return;
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);

            var director = new GameObject("LandmarkDirector");
            SceneManager.MoveGameObjectToScene(director, scene);
            var dir = director.AddComponent<LandmarkDirector>();
            dir.spriteMaterial = mat;

            var player = MakePlayer(mat);
            SceneManager.MoveGameObjectToScene(player, scene);

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            SceneManager.MoveGameObjectToScene(camGo, scene);
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, -6f, -10f);
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 3.5f;          // the on-foot zoom everywhere else (PitLaneStart.onFootOrthoSize)
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.09f);
            var follow = camGo.AddComponent<OnFootCameraFollow>();
            follow.target = player.transform;

            var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(es, scene);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        var list = EditorBuildSettings.scenes.ToList();
        if (!list.Any(s => s.path == ScenePath))
        {
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
        Debug.Log($"Build Roadside Shop Scene: wrote {ScenePath} and it is in the build. Every satnav stop loads it.");
    }

    static GameObject MakePlayer(Material mat)
    {
        var player = new GameObject("OnFootPlayer");
        player.transform.position = new Vector3(0f, -6f, 0f);
        var sr = player.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 25;
        if (mat != null) sr.sharedMaterial = mat;
        Sprite walk = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(PlayerSpritePath))
            if (a is Sprite sp && (walk == null || string.CompareOrdinal(sp.name, walk.name) < 0)) walk = sp;
        sr.sprite = walk;
        var anim = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerAnimatorPath);
        if (walk != null && anim != null) player.AddComponent<Animator>().runtimeAnimatorController = anim;
        var rb = player.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        player.AddComponent<CircleCollider2D>().radius = 0.25f;
        var ofc = player.AddComponent<OnFootController>();
        ofc.faceMoveDirection = walk != null;
        ofc.controlsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath);
        if (walk == null || ofc.controlsAsset == null)
            Debug.LogWarning("Build Roadside Shop Scene: player art or controls missing — check the OnFootPlayer.");
        return player;
    }
}
