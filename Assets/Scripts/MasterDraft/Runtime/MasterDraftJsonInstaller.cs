using System;
using Draftmaster.MasterDraft;
using UnityEngine;

// Hands Master Draft's engine-free Core its JSON: JsonUtility, exactly what the profile was always saved with.
// Core can't call JsonUtility itself because it is compiled without UnityEngine so the rules run under plain
// `dotnet test` (Tools/HeadlessTests). Installed before the first scene loads, on every editor domain reload
// (so EditMode tests have it), and again by LocalMasterDraftBackend before it reads the profile file.
public static class MasterDraftJsonInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Install()
    {
        ProfileJson.Write ??= (obj, pretty) => JsonUtility.ToJson(obj, pretty);
        ProfileJson.Read ??= (json, type) => JsonUtility.FromJson(json, type);
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    static void InstallInEditor() => Install();
#endif
}
