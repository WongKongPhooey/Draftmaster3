using System.Text;
using UnityEngine;
using Draftmaster.Garage;

// A garage crew member the player walks up to and interacts with. Instead of speech lines it opens a
// role-specific info panel (chassis / engine / sponsorship). Subclasses NPCInteractable so OnFootController's
// proximity prompt, facing and movement lock all work unchanged — only what "interact" does is overridden.
public class RoleStation : NPCInteractable
{
    public enum Role { Fabricator, EngineMechanic, SponsorshipManager }

    [Tooltip("Which crew role this station represents. Drives the panel title and body.")]
    public Role role = Role.Fabricator;

    bool _open;

    // OnFootController locks movement / hides the prompt while this is true.
    public override bool IsTalking => _open;

    public override bool Interact()
    {
        var panel = GaragePanelUI.Ensure();
        if (!_open)
        {
            _open = true;
            panel.Show(TitleFor(role), BodyFor(role));
            return true;            // keep focus so the player stays put while reading
        }
        // Second press closes the panel and ends the interaction.
        _open = false;
        panel.Hide();
        return false;
    }

    public static string TitleFor(Role r) => r switch
    {
        Role.Fabricator         => "FABRICATOR — Chassis Shop",
        Role.EngineMechanic     => "ENGINE MECHANIC — Dyno Bay",
        Role.SponsorshipManager => "SPONSORSHIP MANAGER",
        _                       => "Crew",
    };

    // Default speaker name / greeting per role, applied by the director when it spawns the station.
    public static string SpeakerFor(Role r) => r switch
    {
        Role.Fabricator         => "Fabricator",
        Role.EngineMechanic     => "Engine Mechanic",
        Role.SponsorshipManager => "Sponsorship Manager",
        _                       => "Crew",
    };

    static string BodyFor(Role r)
    {
        var sb = new StringBuilder();
        switch (r)
        {
            case Role.Fabricator:
                sb.AppendLine($"Current chassis:  {TeamGarageData.CurrentChassisName}");
                sb.AppendLine($"Condition:        {Bar(TeamGarageData.CurrentChassisCondition)}  {TeamGarageData.CurrentChassisCondition}%");
                sb.AppendLine();
                sb.AppendLine($"NEW BUILD — {TeamGarageData.NewBuildName}");
                sb.AppendLine($"Progress:         {Bar(TeamGarageData.NewBuildProgress)}  {TeamGarageData.NewBuildProgress}%");
                sb.AppendLine($"ETA:              {TeamGarageData.NewBuildEtaRaces} races");
                break;

            case Role.EngineMechanic:
                sb.AppendLine($"Spec:             {TeamGarageData.EngineSpec}");
                sb.AppendLine($"Peak power:       {TeamGarageData.EnginePeakHp} hp");
                sb.AppendLine();
                sb.AppendLine($"Wear:             {Bar(TeamGarageData.EngineWear)}  {TeamGarageData.EngineWear}%");
                sb.AppendLine($"Development:      {Bar(TeamGarageData.EngineDevelopment)}  {TeamGarageData.EngineDevelopment}%");
                sb.AppendLine($"Since rebuild:    {TeamGarageData.RacesSinceRebuild} races");
                break;

            case Role.SponsorshipManager:
                sb.AppendLine("ON THE CAR");
                foreach (var s in TeamGarageData.Sponsors)
                    if (s.Signed) sb.AppendLine($"  {s.Name}  —  ${s.PerRace:N0}/race  —  {s.Demand}");
                sb.AppendLine();
                sb.AppendLine("PROSPECTS TO LAND");
                foreach (var s in TeamGarageData.Sponsors)
                    if (!s.Signed) sb.AppendLine($"  {s.Name}  —  ${s.PerRace:N0}/race  —  wants {s.Demand}");
                break;
        }
        return sb.ToString();
    }

    // 10-cell ASCII meter, e.g. 42% -> [####------].
    static string Bar(int pct)
    {
        int filled = Mathf.Clamp(pct, 0, 100) / 10;
        return "[" + new string('#', filled) + new string('-', 10 - filled) + "]";
    }
}
