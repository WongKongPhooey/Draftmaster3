#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Where an object made from a GameObject > Draftmaster menu item actually belongs.
//
// Two things go wrong otherwise, and both look like the object vanishing:
//
// 1. `new GameObject()` always goes into the MAIN SCENE. Make one while a track package is open for
//    editing (Draftmaster > Tracks > Edit Selected Package In Context, which is Prefab Mode on a stage)
//    and it is created behind the stage the Hierarchy is showing — selected, alive in the Inspector, and
//    nowhere to be found in the tree.
// 2. A prefab stage saves what is UNDER the prefab's own root. An object left as a root-level sibling of
//    that root is shown in the Hierarchy and then quietly lost when the stage is saved.
//
// So: put it in whatever is open, parent it into the thing being edited, and stand it where the scene
// view is looking so it lands on screen rather than at the origin.
public static class PaddockAuthoringStage
{
    public static void Place(GameObject go, MenuCommand cmd)
    {
        StageUtility.PlaceGameObjectInCurrentStage(go);

        // A right-click in the Hierarchy names the parent outright. That is a deliberate choice, so the
        // object is aligned to it and left there rather than dragged off to wherever the camera is.
        var chosen = cmd != null ? cmd.context as GameObject : null;
        if (chosen != null)
        {
            GameObjectUtility.SetParentAndAlign(go, chosen);
            return;
        }

        // Otherwise, in Prefab Mode, everything belongs under the prefab's root or it is not saved. Prefer
        // the package's paddock root when it has one, so authored pieces land where the rest of them live.
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            var parent = stage.prefabContentsRoot;
            var package = stage.prefabContentsRoot.GetComponentInChildren<TrackPackage>(true);
            if (package != null && package.paddockRoot != null) parent = package.paddockRoot.gameObject;
            GameObjectUtility.SetParentAndAlign(go, parent);
        }

        // On the ground plane at whatever the scene view is looking at. In Prefab Mode that pivot is in the
        // stage's own space, which is the package's space, so it is the right place either way.
        var view = SceneView.lastActiveSceneView;
        Vector3 pivot = view != null ? view.pivot : Vector3.zero;
        go.transform.position = new Vector3(pivot.x, pivot.y, 0f);
    }
}
#endif
