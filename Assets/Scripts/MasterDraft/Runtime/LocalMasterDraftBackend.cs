using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Draftmaster.MasterDraft;
using UnityEngine;

// The offline backend: one JSON file on this device, Application.persistentDataPath/masterdraft.json. Kept
// apart from the career's draftmaster.db on purpose — Master Draft never reads or writes the career.
//
// Every operation runs the pure rules in ProfileOps / TradeRules on the in-memory profile and writes the file
// straight after, so a crash between opening a pack and saving can't lose the cards (or duplicate them).
//
// A schema-1 (card collection) file is converted on load (SchemaMigration: a starter car plus coins) and the
// original text is copied to masterdraft.v1.json beside it BEFORE the converted profile is written over it. If
// that copy can't be made the profile is used but never saved, so the old file survives. A file that can't be
// read at all is copied to masterdraft.unreadable.json before a fresh profile replaces it.
public class LocalMasterDraftBackend : IMasterDraftBackend
{
    public const string FileName = "masterdraft.json";
    public const string UnreadableFileName = "masterdraft.unreadable.json";

    readonly string _path;
    readonly CardCatalog _catalog;
    MasterDraftProfile _profile;
    CarCatalog _cars;
    bool _saveBlocked;   // the old file couldn't be backed up: never overwrite it

    public LocalMasterDraftBackend(CardCatalog catalog, string path = null)
    {
        _catalog = catalog;
        _path = string.IsNullOrEmpty(path) ? Path.Combine(Application.persistentDataPath, FileName) : path;
    }

    // The car catalogue (Resources/MasterDraft/Cars.json), loaded on first use; null if it isn't there.
    public CarCatalog Cars
    {
        get
        {
            if (_cars != null) return _cars;
            MasterDraftJsonInstaller.Install();
            var text = Resources.Load<TextAsset>(CarCatalog.ResourcePath);
            _cars = text == null ? null : CarCatalog.FromJson(text.text);
            if (_cars == null) Debug.LogWarning($"[MasterDraft] No car catalogue at Resources/{CarCatalog.ResourcePath}.");
            return _cars;
        }
    }

    public string Name => "Local file";
    public bool SignedIn { get; private set; }
    public string PlayerId => _profile != null ? _profile.playerId : "";
    public MasterDraftProfile Profile => _profile;
    public string FilePath => _path;

    public Task<bool> SignIn()
    {
        SignedIn = true;
        return Task.FromResult(true);
    }

    public Task<MasterDraftProfile> LoadProfile()
    {
        MasterDraftJsonInstaller.Install();
        _profile = null;
        _saveBlocked = false;
        string text = null;
        int migratedFrom = 0;
        try
        {
            if (File.Exists(_path))
            {
                text = File.ReadAllText(_path);
                _profile = MasterDraftProfile.FromJson(text, Cars, out migratedFrom);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MasterDraft] Couldn't read {_path}: {e.Message}");
        }

        if (_profile != null && migratedFrom > 0)
        {
            // Keep the old save before the converted one is written over it.
            string backup = KeepCopy(text, $"masterdraft.v{migratedFrom}.json");
            if (backup != null)
            {
                _profile.migration.backupFile = Path.GetFileName(backup);
                Debug.Log($"[MasterDraft] Converted a schema-{migratedFrom} save: {_profile.migration.Describe()}");
                Write();
            }
            else
            {
                _saveBlocked = true;
                Debug.LogError($"[MasterDraft] Couldn't back up the schema-{migratedFrom} save at {_path}; " +
                               "playing on the converted profile without saving so the old file is kept.");
            }
        }
        else if (_profile == null)
        {
            // Unreadable text is kept aside rather than lost under a fresh profile.
            if (!string.IsNullOrWhiteSpace(text) && KeepCopy(text, UnreadableFileName) == null) _saveBlocked = true;
            if (text == null && File.Exists(_path)) _saveBlocked = true;   // there but couldn't be read: leave it be

            // A fresh profile. The id seeds this player's draws, so it is random once and then fixed.
            _profile = MasterDraftProfile.New("local-" + Guid.NewGuid().ToString("N").Substring(0, 12), "Player", Cars);
            Write();
        }
        return Task.FromResult(_profile);
    }

    public Task SaveProfile()
    {
        Write();
        return Task.CompletedTask;
    }

    public Task<DraftResult> ClaimStarterPack() => Run(p => ProfileOps.ClaimStarter(p, _catalog));

    public Task<DraftResult> BuyAndOpenPack(PackKind kind) =>
        kind == PackKind.Starter ? ClaimStarterPack() : Run(p => ProfileOps.BuyStandardPack(p, _catalog));

    public Task<DraftResult> AwardRace(int position, int cardsPlayed) =>
        Run(p => ProfileOps.AwardRace(p, position, cardsPlayed));

    public Task<List<TradeOffer>> ListTradeOffers()
    {
        if (_profile == null) return Task.FromResult(new List<TradeOffer>());
        int before = _profile.botOffers.Count;
        TradeRules.EnsureBotOffers(_profile, _catalog);
        if (_profile.botOffers.Count != before) Write();
        return Task.FromResult(new List<TradeOffer>(_profile.botOffers));
    }

    public Task<DraftResult> ProposeTrade(string myCardUid, CardType wantType, Rarity wantMinRarity) =>
        Run(p => TradeRules.Propose(p, myCardUid, wantType, wantMinRarity));

    public Task<DraftResult> AcceptTrade(string offerId, string myCardUid) =>
        Run(p => TradeRules.Accept(p, offerId, myCardUid));

    public Task<bool> CancelTrade(string listingId)
    {
        bool ok = _profile != null && TradeRules.Cancel(_profile, listingId);
        if (ok) Write();
        return Task.FromResult(ok);
    }

    Task<DraftResult> Run(Func<MasterDraftProfile, DraftResult> op)
    {
        if (_profile == null) return Task.FromResult(DraftResult.Fail("No profile loaded."));
        var result = op(_profile);
        if (result.ok) Write();
        return Task.FromResult(result);
    }

    // Copies text to name beside the profile file, never over an existing copy (name, then name-2, name-3...).
    // Returns the path written, or null if it couldn't be.
    string KeepCopy(string text, string name)
    {
        try
        {
            string dir = Path.GetDirectoryName(_path) ?? "";
            string stem = Path.GetFileNameWithoutExtension(name), ext = Path.GetExtension(name);
            string target = Path.Combine(dir, name);
            for (int n = 2; File.Exists(target); n++) target = Path.Combine(dir, $"{stem}-{n}{ext}");
            File.WriteAllText(target, text ?? "");
            return target;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MasterDraft] Couldn't keep a copy as {name}: {e.Message}");
            return null;
        }
    }

    // Write to a temp file and swap it in, so a half-written file never replaces a good one.
    void Write()
    {
        if (_profile == null) return;
        if (_saveBlocked)
        {
            Debug.LogWarning($"[MasterDraft] Not saving over {_path}: its old contents couldn't be backed up.");
            return;
        }
        try
        {
            string dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = _path + ".tmp";
            File.WriteAllText(tmp, _profile.ToJson(true));
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(tmp, _path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MasterDraft] Couldn't write {_path}: {e.Message}");
        }
    }
}
