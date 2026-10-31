using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Draftmaster.MasterDraft;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

// SKELETON. The online backend on Unity Gaming Services. Only used when MasterDraftOnline.Enabled is true,
// which it is not in v1.
//
// What works today: sign-in. Unity Core + Authentication are already in the project (pulled in by
// com.unity.services.multiplayer, and NetworkLauncher signs in the same way), so this signs in anonymously
// for real.
//
// What doesn't: Cloud Save, Economy and Cloud Code are NOT installed (no packages were added — the project
// is pinned to 6000.3). Every call that needs them is a TODO inside #if MASTERDRAFT_UGS. Without that define
// the backend falls back to the local file for storage and the local rules for packs and trades, and says so
// in the log — useful for testing sign-in, NOT safe to ship: a client that opens its own packs can be made
// to open any pack it likes. See Docs/Master-Draft.md, "Before online launch".
//
// To finish it: install com.unity.services.cloudsave, com.unity.services.cloudcode (and economy if coins move
// there), set up the project in the UGS dashboard, add MASTERDRAFT_UGS to the scripting define symbols, and
// replace each TODO below with the call it describes.
public class UgsMasterDraftBackend : IMasterDraftBackend
{
    // Cloud Save key the profile JSON lives under, and the Cloud Code module that owns the rules.
    public const string ProfileKey = "masterdraft_profile";
    public const string CloudCodeModule = "MasterDraft";

    readonly LocalMasterDraftBackend _local;   // offline cache + stand-in for the TODOs

    public UgsMasterDraftBackend(CardCatalog catalog)
    {
        _local = new LocalMasterDraftBackend(catalog);
    }

    public string Name => "Unity Gaming Services";
    public bool SignedIn => UnityServices.State == ServicesInitializationState.Initialized
                            && AuthenticationService.Instance.IsSignedIn;
    public string PlayerId => SignedIn ? AuthenticationService.Instance.PlayerId : _local.PlayerId;
    public MasterDraftProfile Profile => _local.Profile;

    public async Task<bool> SignIn()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            await _local.SignIn();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MasterDraft] UGS sign-in failed: {e.Message}");
            return false;
        }
    }

    public async Task<MasterDraftProfile> LoadProfile()
    {
#if MASTERDRAFT_UGS
        // TODO(UGS Cloud Save): load the server copy and prefer it over the local cache.
        //   var items = await Unity.Services.CloudSave.CloudSaveService.Instance.Data.Player.LoadAsync(
        //       new HashSet<string> { ProfileKey });
        //   if (items.TryGetValue(ProfileKey, out var item))
        //       return MasterDraftProfile.FromJson(item.Value.GetAsString());   // then cache locally
#endif
        Debug.Log("[MasterDraft] UGS backend: Cloud Save not wired — profile loaded from the local file.");
        var profile = await _local.LoadProfile();
        if (SignedIn && profile != null && profile.playerId.StartsWith("local-"))
        {
            profile.playerId = AuthenticationService.Instance.PlayerId;
            await _local.SaveProfile();
        }
        return profile;
    }

    public async Task SaveProfile()
    {
#if MASTERDRAFT_UGS
        // TODO(UGS Cloud Save): write client-owned fields only (team, display name). Cards and coins must be
        // written by Cloud Code, never by the client — use a Cloud Save access class that makes them
        // server-write-only.
        //   await Unity.Services.CloudSave.CloudSaveService.Instance.Data.Player.SaveAsync(
        //       new Dictionary<string, object> { { ProfileKey + "_team", JsonUtility.ToJson(Profile.team) } });
#endif
        await _local.SaveProfile();
    }

    public Task<DraftResult> ClaimStarterPack()
    {
#if MASTERDRAFT_UGS
        // TODO(UGS Cloud Code): server-authoritative.
        //   return Unity.Services.CloudCode.CloudCodeService.Instance.CallModuleEndpointAsync<DraftResult>(
        //       CloudCodeModule, "ClaimStarterPack", new Dictionary<string, object>());
#endif
        return _local.ClaimStarterPack();
    }

    public Task<DraftResult> BuyAndOpenPack(PackKind kind)
    {
#if MASTERDRAFT_UGS
        // TODO(UGS Cloud Code + Economy): the server debits coins (Economy currency COINS), rolls the pack
        // with PackGenerator's rules and its own seed, writes the cards, and returns them.
        //   CallModuleEndpointAsync<DraftResult>(CloudCodeModule, "OpenPack", new Dictionary<string, object> { { "kind", (int)kind } });
#endif
        return _local.BuyAndOpenPack(kind);
    }

    public Task<DraftResult> AwardRace(int position, int cardsPlayed)
    {
#if MASTERDRAFT_UGS
        // TODO(UGS Cloud Code): an offline race result is self-reported, so cap and rate-limit it server-side;
        // an online race should be reported by the host/server, not the player.
#endif
        return _local.AwardRace(position, cardsPlayed);
    }

    public Task<List<TradeOffer>> ListTradeOffers()
    {
#if MASTERDRAFT_UGS
        // TODO(UGS Cloud Code): the real trade board — other players' listings, paged.
#endif
        return _local.ListTradeOffers();
    }

    public Task<DraftResult> ProposeTrade(string myCardUid, CardType wantType, Rarity wantMinRarity)
    {
#if MASTERDRAFT_UGS
        // TODO(UGS Cloud Code): escrow the card server-side while it is listed.
#endif
        return _local.ProposeTrade(myCardUid, wantType, wantMinRarity);
    }

    public Task<DraftResult> AcceptTrade(string offerId, string myCardUid)
    {
#if MASTERDRAFT_UGS
        // TODO(UGS Cloud Code): an atomic swap between two players' Cloud Save data, both cards checked
        // against the server's copy.
#endif
        return _local.AcceptTrade(offerId, myCardUid);
    }

    public Task<bool> CancelTrade(string listingId)
    {
#if MASTERDRAFT_UGS
        // TODO(UGS Cloud Code): release the escrowed card.
#endif
        return _local.CancelTrade(listingId);
    }
}
