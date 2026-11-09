using System.Text.Json;
using Draftmaster.MasterDraft;
using NUnit.Framework;

// Outside the engine there is no JsonUtility, so Master Draft's profile JSON goes through System.Text.Json
// set up to JsonUtility's rules: public fields, no properties. In the game MasterDraftJsonInstaller installs
// JsonUtility instead. No namespace: an assembly-wide setup fixture, run once before every test.
[SetUpFixture]
public class HeadlessSetup
{
    static readonly JsonSerializerOptions Compact = new JsonSerializerOptions { IncludeFields = true, IgnoreReadOnlyProperties = true };
    static readonly JsonSerializerOptions Pretty = new JsonSerializerOptions { IncludeFields = true, IgnoreReadOnlyProperties = true, WriteIndented = true };

    [OneTimeSetUp]
    public void InstallJson()
    {
        ProfileJson.Write = (obj, pretty) => JsonSerializer.Serialize(obj, obj.GetType(), pretty ? Pretty : Compact);
        ProfileJson.Read = (json, type) => JsonSerializer.Deserialize(json, type, Compact);
    }
}
