using UnityEngine;

public class Orb : MonoBehaviour
{
    [SerializeField] private OrbData orbData;

    public float magnetstrength = 2f;
    public float radius = 20f;
    public float rotationSpeed = 50f;
    public float bobSpeed = 2f;
    public float bobHeight = 0.25f;
    public GameObject player;

    private Vector3 startPos;   // store original spawn position

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        float DistFromPlayer = Vector3.Distance(transform.position, player.transform.position);
        float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;

        
        if (DistFromPlayer < radius)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                player.transform.position,
                magnetstrength * Time.deltaTime
            );
        }

        /* subtle animation for orbs */
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f);

    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject == player)
        {
            orbData.OrbCount++;
            Debug.Log("Orbs: " + orbData.OrbCount);
            Destroy(gameObject);
        }
    }
}
