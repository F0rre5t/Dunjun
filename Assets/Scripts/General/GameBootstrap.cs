using UnityEngine;

[DefaultExecutionOrder(-50)]
public class GameBootstrap : MonoBehaviour
{
    [Header("Player")]
    
    public GameObject playerPrefab;
    
    public bool destroyScenePlayerOnSpawn = true;

    [Header("Camera")]
    public bool snapCameraOnSpawn = true;

    GameObject spawnedPlayer;

    public GameObject Player => spawnedPlayer;

    void Awake()
    {
        PlayerControl.ConfigureCombatPhysics();
    }

    public void SpawnPlayerAtRoom(Room startRoom)
    {
        if (startRoom == null)
        {
            Debug.LogError("GameBootstrap: start room is null.");
            return;
        }

        Vector3 spawnPos = startRoom.GetSpawnPosition();

        if (playerPrefab != null)
        {
            if (destroyScenePlayerOnSpawn)
            {
                GameObject existing = GameObject.FindGameObjectWithTag("Player");
                if (existing != null)
                {
                    Destroy(existing);
                }
            }

            spawnedPlayer = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            spawnedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (spawnedPlayer == null)
            {
                Debug.LogError("GameBootstrap: no playerPrefab assigned and no Player found in scene.");
                return;
            }

            spawnedPlayer.transform.position = spawnPos;
        }

        spawnedPlayer.tag = "Player";

        if (spawnedPlayer.GetComponent<RelicEffectApplier>() == null)
        {
            spawnedPlayer.AddComponent<RelicEffectApplier>();
        }

        if (spawnedPlayer.GetComponent<PoisonTrailSpawner>() == null)
        {
            spawnedPlayer.AddComponent<PoisonTrailSpawner>();
        }

        if (CameraController.instance != null)
        {
            CameraController.instance.Changetarget(startRoom.transform);
            if (snapCameraOnSpawn)
            {
                CameraController.instance.SnapToTarget();
            }
        }

        HealthManager healthManager = FindAnyObjectByType<HealthManager>(FindObjectsInactive.Include);
        if (healthManager != null)
        {
            healthManager.BindPlayer(spawnedPlayer);
        }
        else
        {
            Debug.LogWarning("GameBootstrap: HealthManager not found.");
        }
    }
}
