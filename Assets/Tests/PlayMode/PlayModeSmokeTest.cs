using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Linchpin proof: do PlayMode tests actually ADVANCE FRAMES (and game time) while the editor is unfocused?
// If yes, behavioural integration tests (run the pace lap, assert no crash) are viable over MCP. If no, we're
// limited to EditMode pure-logic tests.
public class PlayModeSmokeTest
{
    [UnityTest]
    public IEnumerator Frames_And_Time_Advance()
    {
        float t0 = Time.time;
        int frames = 0;
        for (int i = 0; i < 30; i++) { yield return null; frames++; }
        float elapsed = Time.time - t0;

        Assert.AreEqual(30, frames, "did not advance 30 frames");
        Assert.Greater(elapsed, 0f, "Time.time did not advance across 30 frames (sim is paused)");
    }

    [UnityTest]
    public IEnumerator FixedUpdate_Physics_Steps()
    {
        // A throwaway rigidbody under gravity must fall if FixedUpdate is stepping.
        var go = new GameObject("fallTest");
        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = true;
        float y0 = go.transform.position.y;
        for (int i = 0; i < 60; i++) yield return new WaitForFixedUpdate();
        float dropped = y0 - go.transform.position.y;
        Object.Destroy(go);
        Assert.Greater(dropped, 0.001f, "object did not fall — FixedUpdate/physics not stepping");
    }
}
