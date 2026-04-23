using UnityEngine;
using Photon.Pun;

public class PlayerMeshSwitcher : MonoBehaviourPunCallbacks
{
    [Header("Meshes")]
    [SerializeField] private GameObject localMesh;  // FPS Hands
    [SerializeField] private GameObject globalMesh; // Full Body

    [Header("Debug")]
    public bool testing;

    private void Start()
    {
        bool isLocal = testing || !PhotonNetwork.InRoom || (photonView != null && photonView.IsMine);

        if (isLocal)
        {
            // Local player: show hands, hide full body
            if (localMesh != null) localMesh.SetActive(true);
            if (globalMesh != null) globalMesh.SetActive(false);
            
            Debug.Log($"[PlayerMeshSwitcher] Local player: LocalMesh=ON, GlobalMesh=OFF on {gameObject.name}");
        }
        else
        {
            // Remote player: hide hands, show full body
            if (localMesh != null) localMesh.SetActive(false);
            if (globalMesh != null) globalMesh.SetActive(true);
            
            Debug.Log($"[PlayerMeshSwitcher] Remote player: LocalMesh=OFF, GlobalMesh=ON on {gameObject.name}");
        }

        // Notify other components to refresh their references
        var movement = GetComponent<PlayerMovement>();
        if (movement != null) movement.RefreshAnimators();

        var cam = GetComponent<PlayerCamera>();
        if (cam != null) cam.RefreshAnimators();

        var combat = GetComponent<PlayerCombat>();
        if (combat != null) combat.RefreshReferences();
    }
}
