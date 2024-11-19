using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "Vehicle/New Vehicle", order = 1)]
public class VehicleInfo : ScriptableObject {
   
   [Header("Vehicle Info")]
   public int topSpeed;
   public float zeroToSixty;
   public AnimationCurve speedCurve;
   public AnimationCurve accelerationCurve;
}