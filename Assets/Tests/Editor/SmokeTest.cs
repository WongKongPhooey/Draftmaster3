using NUnit.Framework;
using UnityEngine;

// Pipeline proof: does the MCP run_tests harness discover + execute a project EditMode test at all?
// Self-contained (references no game code) so it isolates the harness from the Assembly-CSharp reference question.
public class SmokeTest
{
    [Test]
    public void Harness_Runs()
    {
        Assert.AreEqual(4, 2 + 2);
    }

    [Test]
    public void Harness_CanUseUnityMath()
    {
        Assert.AreEqual(0f, Mathf.DeltaAngle(10f, 10f), 1e-4f);
    }
}
