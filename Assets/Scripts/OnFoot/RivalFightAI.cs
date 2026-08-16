using UnityEngine;
using Draftmaster.Fights;

// The rival driver's side of a scrap. Keeps them at arm's length from the player, squared up, and throws a
// move every so often — more often the higher their roster Aggression. Added by DriverFight when the fight
// starts and destroyed with it.
//
// This drives the transform directly rather than through a Rigidbody: paddock drivers are kinematic bodies
// whose walking is scripted (PaddockWalker does the same), and the fight needs frame-accurate spacing.
[RequireComponent(typeof(Fighter))]
public class RivalFightAI : MonoBehaviour
{
    [Tooltip("Move speed while closing on or backing off the player (m/s). A shade quicker than a paddock walk.")]
    public float moveSpeed = 1.6f;
    [Tooltip("Paper-doll walk frames per second while moving.")]
    public float walkFrameRate = 8f;
    [Tooltip("Metres of slack around the ideal fighting distance before they bother stepping in or out.")]
    public float rangeTolerance = 0.18f;
    [Tooltip("How much they circle rather than stand still, as a fraction of move speed. 0 = square-on stand-off.")]
    [Range(0f, 1f)] public float circleAmount = 0.45f;
    [Tooltip("Seconds before the first swing, so the fight opens with a beat of squaring up rather than an instant hit.")]
    public float openingDelay = 0.9f;

    Fighter _self;
    float _nextAttack;
    float _frameTimer;
    int _frame;
    float _circleSign = 1f;
    float _circleFlipAt;

    void Awake()
    {
        _self = GetComponent<Fighter>();
        _nextAttack = Time.time + openingDelay;
        _circleSign = Random.value < 0.5f ? -1f : 1f;
        _circleFlipAt = Time.time + Random.Range(1.5f, 3f);
    }

    // Push the next swing back. Used when the fight is held behind something — the first-fight tutorial popup
    // — so the rival doesn't come out of the hold already throwing.
    public void DelayNextAttack(float seconds)
        => _nextAttack = Mathf.Max(_nextAttack, Time.time + Mathf.Max(0f, seconds));

    void Update()
    {
        if (_self == null || _self.Opponent == null) return;

        Vector3 to = _self.Opponent.transform.position - transform.position;
        to.z = 0f;
        float distance = to.magnitude;
        Vector2 dir = distance > 0.0001f ? (Vector2)(to / distance) : Vector2.down;

        _self.FaceOpponent();

        // Winded, mid-move, rocked back, or the crews have arrived: hold position and breathe.
        if (_self.IsWinded || _self.Busy || _self.AttacksLocked)
        {
            FightMotion.IdleFrame(_self.Appearance, ref _frameTimer, ref _frame);
            return;
        }

        Step(dir, distance);

        if (Time.time >= _nextAttack)
        {
            float ideal = FightRules.DesiredRange(_self.reach);
            // Only swing from somewhere the move can actually land; otherwise close first and try again soon.
            if (distance <= _self.reach)
            {
                _self.TryThrow(FightMove.Shove);
                _nextAttack = Time.time + FightRules.AiAttackInterval(_self.aggression, Random.value);
            }
            else if (distance > ideal)
            {
                _nextAttack = Time.time + 0.25f;
            }
        }
    }

    // Hold the fighting distance: close if the player backs off, give ground if they crowd in, and drift
    // around them the rest of the time so two characters aren't stood nose to nose like shop mannequins.
    void Step(Vector2 dir, float distance)
    {
        float ideal = FightRules.DesiredRange(_self.reach);
        Vector2 move = Vector2.zero;

        if (distance > ideal + rangeTolerance) move += dir;
        else if (distance < ideal - rangeTolerance) move -= dir;

        if (Time.time >= _circleFlipAt)
        {
            _circleSign = -_circleSign;
            _circleFlipAt = Time.time + Random.Range(1.5f, 3.5f);
        }
        move += new Vector2(-dir.y, dir.x) * (_circleSign * circleAmount);

        if (move.sqrMagnitude < 0.0001f)
        {
            FightMotion.IdleFrame(_self.Appearance, ref _frameTimer, ref _frame);
            return;
        }

        move = move.normalized * moveSpeed * Time.deltaTime;
        Vector3 next = transform.position + (Vector3)move;

        // Never shove a fighter out of the walkable paddock — the player can't follow them out there.
        if (PaddockBoundary.AnyActive)
        {
            Vector2 clamped = PaddockBoundary.Constrain(next);
            next = new Vector3(clamped.x, clamped.y, next.z);
        }

        FightMotion.PlaceAt(transform, _self.Body, next);
        FightMotion.StepFrames(_self.Appearance, ref _frameTimer, ref _frame, walkFrameRate);
    }
}
