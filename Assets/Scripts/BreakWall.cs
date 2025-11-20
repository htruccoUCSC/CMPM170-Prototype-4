using UnityEngine;

public class SimpleBreakableWall : MonoBehaviour
{
    [SerializeField] private OrbData orbData;
   /* wall objects */
    public GameObject intactWall;
    public GameObject brokenWall;


    /* explosion vars */
    public float maxRecordedSpeed = 2f;
    public float minExplosionForce = 0.2f;
    public float maxExplosionForce = 2f;
    public float explosionRadius = 5f;
    public float upwardsModifier = 0.5f;
    public float debrisLifetime = 5f;
    /* orb cost to destroy wall */
    public float orbcost = 0;
    /**
        references OrbData to get number of orbs collected. 
        If higher than or equal to orbcost of wall, player can break
     **/
    private float NumOrbs => orbData != null ? orbData.OrbCount : 0f;
    bool hasBroken = false;

    void Awake()
    {
        if (orbData == null)
        {
            orbData = FindObjectOfType<OrbData>();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasBroken) {
            return;
             }
        if (!other.CompareTag("Player")) 
            {
            return;
            }

        if (orbcost >= NumOrbs)
        {
            Debug.Log("Not enough orbs to break this wall.");
            return;
        }

        GridMover mover = other.GetComponent<GridMover>();
        float speed = 0f;

        if (mover != null)
        {
            /* taken from Gridmover (absolute) */
            speed = mover.CurrentSpeedAbs;
        }

        float clampedSpeed = Mathf.Clamp(speed, 0f, maxRecordedSpeed);

        float t = (maxRecordedSpeed > 0f) ? (clampedSpeed / maxRecordedSpeed) : 0f;

        /* computes explosion force based off of player speed */
        float explosionForce = Mathf.Lerp(minExplosionForce, maxExplosionForce, t);

        Vector3 explosionPoint = other.transform.position;

        DeductOrbs();
        Break(explosionForce, explosionPoint);
    }

    void Break(float explosionForce, Vector3 explosionPoint)
    {
        hasBroken = true;
        Debug.Log($"WALL BROKE!");

        if (intactWall != null)
            intactWall.SetActive(false);

        if (brokenWall != null)
            brokenWall.SetActive(true);

        Rigidbody[] pieces = brokenWall.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in pieces)
        {
            rb.AddExplosionForce(
                explosionForce,
                explosionPoint,
                explosionRadius,
                upwardsModifier,
                ForceMode.Impulse
            );
           
        }

    }

    void DeductOrbs()
    {
        if (orbData == null)
        {
            return;
        }

        int cost = Mathf.RoundToInt(orbcost);
        orbData.OrbCount = Mathf.Max(0, orbData.OrbCount - cost);
    }
}
