using UnityEngine;

/** Public class for storing and accessing OrbCount (the number of orbs the player has collected).
 Everytime player collides with a wall, OrbCost is deducted from OrbCount **/
public class OrbData : MonoBehaviour
{
    public int OrbCount = 0;
}