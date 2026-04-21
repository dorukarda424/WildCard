using UnityEngine;
using System.Collections.Generic;

public class KillCamManager : MonoBehaviour
{
    public static KillCamManager Instance { get; private set; }

    private Dictionary<int, PlayerRecorder> _recorders = new Dictionary<int, PlayerRecorder>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
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
