using Unity.Netcode;
using UnityEngine;

// Pre-race multiplayer lobby. Lists every connected player with a ready tick and a Ready button for yourself.
// The race is held on the grid (RaceStart.PreGrid) until the server's gate in NetworkedCarBindings sees 2+
// players all ready, then flips to Green. This panel only shows in multiplayer before the green flag; it
// self-bootstraps so no scene wiring is needed and stays invisible in single-player and the menus.
public class MultiplayerLobbyUI : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<MultiplayerLobbyUI>() != null) return;
        var go = new GameObject("MultiplayerLobbyUI");
        go.AddComponent<MultiplayerLobbyUI>();
        DontDestroyOnLoad(go);
    }

    const int MinPlayers = 2;

    GUIStyle _title, _row, _btn;
    static Texture2D _tex;

    void EnsureStyles()
    {
        if (_tex == null) { _tex = new Texture2D(1, 1); _tex.SetPixel(0, 0, Color.white); _tex.Apply(); }
        if (_title == null)
            _title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.85f, 0.3f) } };
        if (_row == null)
            _row = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 0, 0), normal = { textColor = Color.white } };
        if (_btn == null)
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
    }

    void OnGUI()
    {
        if (!GameSession.IsMultiplayer) return;
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return;
        if (RaceStart.IsGreen) return; // race underway — hide the lobby

        EnsureStyles();

        var players = NetworkedCarBindings.Players;
        int count = players.Count;
        int ready = NetworkedCarBindings.ReadyCount;

        float w = 380f, rowH = 30f;
        float h = 56f + Mathf.Max(count, 1) * rowH + 52f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height * 0.18f;

        Fill(new Rect(x - 12f, y - 12f, w + 24f, h + 24f), new Color(0f, 0f, 0f, 0.78f));
        GUI.Label(new Rect(x, y, w, 28f), "MULTIPLAYER LOBBY", _title);
        y += 34f;
        GUI.Label(new Rect(x, y, w, 22f), $"Waiting for players — {ready}/{count} ready", _row);
        y += 26f;

        for (int i = 0; i < count; i++)
        {
            var p = players[i];
            if (p == null) continue;

            string num = p.Number > 0 ? $"#{p.Number}" : "";
            string label = string.IsNullOrEmpty(p.DisplayLabel) ? "Player" : p.DisplayLabel;
            string status = p.IsReadyFlag ? "READY" : "waiting";
            var prevCol = GUI.color;
            GUI.color = p.IsReadyFlag ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(new Rect(x, y, w - 120f, rowH), $"{num,-4} {label}", _row);
            GUI.color = prevCol;

            if (p.IsLocalPlayer)
            {
                if (GUI.Button(new Rect(x + w - 116f, y + 2f, 110f, rowH - 4f), p.IsReadyFlag ? "Unready" : "READY", _btn))
                    p.ToggleReadyLocal();
            }
            else
            {
                GUI.Label(new Rect(x + w - 116f, y, 110f, rowH), status, _row);
            }
            y += rowH;
        }

        y += 8f;
        string footer = count < MinPlayers
            ? $"Need at least {MinPlayers} players to start"
            : (ready == count ? "All ready — starting…" : "Waiting for everyone to ready up…");
        GUI.Label(new Rect(x, y, w, 24f), footer, _title);
    }

    static void Fill(Rect r, Color c)
    {
        var prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, _tex);
        GUI.color = prev;
    }
}
