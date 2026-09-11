using TMPro;
using UnityEngine;

// Handle for a built dialogue window, so callers set text rather than walk the hierarchy. Copy longer
// than the window is paged; Advance() steps a page and reports whether the line is finished.
//
// IN ITS OWN FILE ON PURPOSE. A MonoBehaviour whose file is not named after it gets no MonoScript of its
// own: Unity will let one into a scene and then write it out as a record with no field data behind it.
// The editor shrugs that off; a player build, reading the scene with no type tree to fall back on, walks
// off the end of the record and calls the whole file corrupt. That is what crashed the first two Windows
// builds. See IronOvalBlink.cs, which was the other half of it.
public class IronOvalDialogue : MonoBehaviour
{
    public TMP_Text speaker, status, line, caret;

    public int Page => line == null ? 1 : line.pageToDisplay;
    public int PageCount => line == null ? 1 : Mathf.Max(1, line.textInfo.pageCount);
    public bool AtEnd => Page >= PageCount;

    public void Set(string speakerName, string body, string statusText = null)
    {
        if (speaker != null) speaker.text = speakerName == null ? "" : speakerName.ToUpperInvariant();
        if (status != null)
        {
            status.text = statusText ?? "";
            status.gameObject.SetActive(!string.IsNullOrEmpty(statusText));
        }
        if (line != null)
        {
            line.text = body;
            line.pageToDisplay = 1;
            // pageCount is only valid once the text has been laid out, and the caret's visibility
            // depends on it.
            line.ForceMeshUpdate();
        }
        RefreshCaret();
    }

    // Returns true while there is more of this line to read.
    public bool Advance()
    {
        if (line == null) return false;
        if (AtEnd) return false;
        line.pageToDisplay++;
        RefreshCaret();
        return true;
    }

    void RefreshCaret()
    {
        if (caret != null) caret.gameObject.SetActive(!AtEnd);
    }
}
