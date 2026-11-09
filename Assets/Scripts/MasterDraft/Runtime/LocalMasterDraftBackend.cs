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
public class LocalMasterDraftBackend : IMasterDraftBackend
{
    public const string FileName = "masterdraft.json";

    readonly string _path;
    readonly CardCatalog _catalog;
    MasterDraftProfile _profile;

    public LocalMasterDraftBackend(CardCatalog catalog, string path = null)
    {
        _catalog = catalog;
        _path = string.IsNullOrEmpty(path) ? Path.Combine(Application.persistentDataPath, FileName) : path;
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
        try
        {
            if (File.Exists(_path)) _profile = MasterDraftProfile.FromJson(File.ReadAllText(_path));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MasterDraft] Couldn't read {_path}: {e.Message}");
        }

        if (_profile == null)
        {
            // A fresh profile. The id seeds this player's packs, so it is random once and then fixed.
            _profile = MasterDraftProfile.New("local-" + Guid.NewGuid().ToString("N").Substring(0, 12));
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

    // Write to a temp file and swap it in, so a half-written file never replaces a good one.
    void Write()
    {
        if (_profile == null) return;
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
