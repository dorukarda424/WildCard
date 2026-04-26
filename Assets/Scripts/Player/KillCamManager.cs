using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;

public class KillCamManager : MonoBehaviour
{
    public static KillCamManager Instance { get; private set; }

    [Header("KillCam Scene References")]
    public Camera killCamCamera;
    public GameObject victimReplayBody;
    public GameObject killerReplayBody;
    public GameObject replayRoot;

    private Dictionary<int, PlayerRecorder> _recorders = new Dictionary<int, PlayerRecorder>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeReplayBodies();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeReplayBodies()
    {
        if (victimReplayBody != null) victimReplayBody.SetActive(false);
        if (killerReplayBody != null) killerReplayBody.SetActive(false);
        if (killCamCamera != null) killCamCamera.enabled = false;
        
        // Disable gameplay scripts on replay bodies if they are not already disabled
        DisableGameplayScripts(victimReplayBody);
        DisableGameplayScripts(killerReplayBody);
    }

    private void DisableGameplayScripts(GameObject obj)
    {
        if (obj == null) return;
        MonoBehaviour[] scripts = obj.GetComponentsInChildren<MonoBehaviour>();
        foreach (var s in scripts)
        {
            // Keep Animator, but disable movement, health, combat, etc.
            if (s is Animator || s is PhotonView || s is PhotonAnimatorView) continue;
            s.enabled = false;
        }
    }

    public void SetKillCamActive(bool active)
    {
        if (killCamCamera != null) killCamCamera.enabled = active;
        if (victimReplayBody != null) victimReplayBody.SetActive(active);
        if (killerReplayBody != null) killerReplayBody.SetActive(active);
        if (replayRoot != null) replayRoot.SetActive(active);
    }

    public void RegisterRecorder(int actorNumber, PlayerRecorder recorder)
    {
        if (actorNumber == -1) return;
        _recorders[actorNumber] = recorder;
    }

    public void UnregisterRecorder(int actorNumber)
    {
        if (_recorders.ContainsKey(actorNumber))
        {
            _recorders.Remove(actorNumber);
        }
    }

    public List<PlayerRecorder.PlayerStateFrame> GetKillerBuffer(int killerActorNumber)
    {
        if (_recorders.TryGetValue(killerActorNumber, out PlayerRecorder recorder))
        {
            return recorder.GetBuffer();
        }
        return null;
    }
}
