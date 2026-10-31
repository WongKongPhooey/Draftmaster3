// The one switch between Master Draft's offline build and its online one.
//
// OFF (v1): profiles live in masterdraft.json on this device (LocalMasterDraftBackend), GO RACE offers Quick
// Race against the AI only, and the trade board is filled with generated bot offers so the flow can be
// exercised. Home shows an OFFLINE / DEV badge.
//
// ON: UgsMasterDraftBackend (Unity Gaming Services) — sign-in, Cloud Save, server-side pack opening and
// trades — and the Online race tab. Not to be flipped until the work listed under "Before online launch"
// in Docs/Master-Draft.md is done: pack opening and trades must be server-authoritative first.
public static class MasterDraftOnline
{
    public static readonly bool Enabled = false;

    public static string BadgeText => Enabled ? "ONLINE" : "OFFLINE / DEV";
}
