using UnityEngine;

public class BoxSpawner : MonoBehaviour
{
    public GameObject boxPrefab;
    public Transform spawnPoint;
    public Transform pickupPoint;
    public StorageBin storageBin;
    public float spawnInterval = 8f;
    public float boxSpeed = 2f;

    float timer;

    void Update()
    {
        if (storageBin != null && storageBin.IsFull) return; // belt stops once storage is full

        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;
            SpawnBox();
        }
    }

    void SpawnBox()
    {
        GameObject box = Instantiate(boxPrefab, spawnPoint.position, spawnPoint.rotation);

        BoxMover mover = box.GetComponent<BoxMover>();
        mover.pickupPoint = pickupPoint;
        mover.speed = boxSpeed;

        Renderer rend = box.GetComponentInChildren<Renderer>();
        if (rend != null)
            rend.material.color = new Color(Random.Range(0.3f, 1f), Random.Range(0.3f, 1f), Random.Range(0.3f, 1f));
    }
}
