using UnityEngine;

// What a piece of a roadside shop is, so the Shop Layouts window can read a tweaked room back.
public class ShopPiece : MonoBehaviour
{
    public enum Role { Counter, Clerk, Bay, Worker, Prop, Door }
    public Role role;
    public string state = "";      // Bay: "finished" | "working"
    public string propKind = "";   // Prop: "bench" | "toolbox" | ...
}
