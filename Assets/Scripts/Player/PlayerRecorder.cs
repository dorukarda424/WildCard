using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;

public class PlayerRecorder : MonoBehaviourPunCallbacks
{
    [System.Serializable]
    public struct PlayerStateFrame
    {
        public Vector3 position;
        public Quaternion rotation;
        public float cameraPitch;
        public bool isShooting;
        public float timestamp;

        public PlayerStateFrame(Vector3 pos, Quaternion rot, float pitch, bool shooting, float time)
        {
            position = pos;
            rotation = rot;
            cameraPitch = pitch;
            isShooting = shooting;
            timestamp = time;
        }
    }

    [Header("Settings")]
    public float recordDuration = 5f;
    public float recordInterval = 0.033f; // ~30 FPS recording

    private List<PlayerStateFrame> _buffer = new List<PlayerStateFrame>();
    private float _nextRecordTime;
    private PlayerCamera _playerCamera;
    private PlayerCombat _playerCombat;

    public List<PlayerStateFrame> GetBuffer() => new List<PlayerStateFrame>(_buffer);

    private void Awake()
    {
        _playerCamera = GetComponent<PlayerCamera>();
        _playerCombat = GetComponent<PlayerCombat>();
    }

    private void Start()
    {
        if (KillCamManager.Instance != null)
        {
            int actorNumber = photonView.Owner != null ? photonView.Owner.ActorNumber : -1;
            KillCamManager.Instance.RegisterRecorder(actorNumber, this);
        }
    }

    private void Update()
    {
        if (Time.time >= _nextRecordTime)
        {
            RecordFrame();
            _nextRecordTime = Time.time + recordInterval;
        }
    }

    private void RecordFrame()
    {
        float pitch = (_playerCamera != null) ? _playerCamera.GetPitch() : 0f;
        // Simple check for shooting
        bool shooting = false;
        if (InputManager.Instance != null && photonView.IsMine)
        {
            shooting = InputManager.Instance.IsShooting;
        }

        PlayerStateFrame frame = new PlayerStateFrame(
            transform.position,
            transform.rotation,
            pitch,
            shooting,
            Time.time
        );

        _buffer.Add(frame);

        // Keep buffer within duration
        float cutoffTime = Time.time - recordDuration;
        while (_buffer.Count > 0 && _buffer[0].timestamp < cutoffTime)
        {
            _buffer.RemoveAt(0);
        }
    }

    private void OnDestroy()
    {
        if (KillCamManager.Instance != null)
        {
            int actorNumber = photonView.Owner != null ? photonView.Owner.ActorNumber : -1;
            KillCamManager.Instance.UnregisterRecorder(actorNumber);
        }
    }
}
