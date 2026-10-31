using System.Collections.Generic;
using System.Threading.Tasks;
using Draftmaster.MasterDraft;

// Everything Master Draft asks of a server. The UI only ever talks to this, so the offline file backend and
// the Unity Gaming Services one are interchangeable.
//
// The shape is deliberately "send an intent, get the result": the client asks to claim the starter, open a
// pack or accept a trade, and the backend decides what came out. Locally that is ProfileOps running on the
// device; online it must be Cloud Code running the same rules, so a modified client can't print cards.
public interface IMasterDraftBackend
{
    string Name { get; }
    bool SignedIn { get; }
    string PlayerId { get; }

    // The profile as last loaded or changed. Null until LoadProfile has run.
    MasterDraftProfile Profile { get; }

    Task<bool> SignIn();
    Task<MasterDraftProfile> LoadProfile();
    // Persists client-owned choices (team layout, display name). Coins and cards are only ever changed by
    // the operations below.
    Task SaveProfile();

    Task<DraftResult> ClaimStarterPack();
    Task<DraftResult> BuyAndOpenPack(PackKind kind);
    Task<DraftResult> AwardRace(int position, int cardsPlayed);

    Task<List<TradeOffer>> ListTradeOffers();
    Task<DraftResult> ProposeTrade(string myCardUid, CardType wantType, Rarity wantMinRarity);
    Task<DraftResult> AcceptTrade(string offerId, string myCardUid);
    Task<bool> CancelTrade(string listingId);
}
