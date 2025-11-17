using UnityEngine;

public class SimpleBreakableWall : MonoBehaviour
{
   // wall objects
    public GameObject intactWall;
    public GameObject brokenWall;


    /* explosion vars (assumes a max player speed of '50'),
     then smooths the explosion based off this max */
    public float maxRecordedSpeed = 2f;
    public float minExplosionForce = 0.2f;
    public float maxExplosionForce = 2f;
    public float explosionRadius = 5f;
    public float upwardsModifier = 0.5f;
    public float debrisLifetime = 5f;

    bool hasBroken = false;

    private void OnTriggerEnter(Collider other)
    {

        if (hasBroken) {
            return;
             }
        if (!other.CompareTag("Player")) 
            {
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
}
