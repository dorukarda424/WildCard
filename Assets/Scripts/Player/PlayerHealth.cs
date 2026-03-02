using System;
using UnityEngine;
using Photon.Pun;

public class PlayerHealth : MonoBehaviourPunCallbacks, IDamageable, IPunObservable
{
    [Header("Health")]
    public float maxHealth = 100f;

    [Header("Debug")]
    public bool testing;
    
    public event Action<float> OnDamageTaken;
    public event Action<int> OnKill;

    private float CurrentHealth { get; set; }
    private bool IsDead { get; set; }

    private float _networkHealth;
    
    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    private void Update()
    {
        if (!testing && !photonView.IsMine)
        {
            CurrentHealth = Mathf.Lerp(CurrentHealth, _networkHealth, Time.deltaTime * 10f);
        }
    }
    
    [PunRPC]
    public void TakeDamage(float damage, int attackerViewID)
    {
        if (!testing && !photonView.IsMine) return;
        if (IsDead) return;

        CurrentHealth -= damage;
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, maxHealth);

        OnDamageTaken?.Invoke(damage);
        Debug.Log($"Took {damage} damage. HP: {CurrentHealth}/{maxHealth}");

        if (CurrentHealth <= 0f)
        {
            Die(attackerViewID);
        }
    }
    
    public void Heal(float amount)
    {
        if (IsDead) return;

        CurrentHealth += amount;
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, maxHealth);
        Debug.Log($"Healed {amount}. HP: {CurrentHealth}/{maxHealth}");
    }
    
    private void Die(int killerViewID)
    {
        IsDead = true;
        OnKill?.Invoke(killerViewID);
        Debug.Log($"Player {photonView.ViewID} killed by {killerViewID}");
        
        InputManager.Instance?.SetInputEnabled(false);
        
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null) movement.enabled = false;
        
        if (!testing)
            photonView.RPC("RPC_OnDeath", RpcTarget.All, killerViewID);
        else
            HandleDeathVisuals();
    }

    [PunRPC]
    private void RPC_OnDeath(int killerViewID)
    {
        HandleDeathVisuals();
    }

    private void HandleDeathVisuals()
    {
        // TODO: play death animation, ragdoll, hide model etc.
        Debug.Log("Death visuals playing");
    }
    
    public void Respawn(Vector3 spawnPosition)
    {
        if (!testing && !photonView.IsMine) return;

        CurrentHealth = maxHealth;
        IsDead = false;

        transform.position = spawnPosition;
        
        InputManager.Instance?.SetInputEnabled(true);

        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null) movement.enabled = true;

        if (!testing)
            photonView.RPC("RPC_OnRespawn", RpcTarget.All);
    }

    [PunRPC]
    private void RPC_OnRespawn()
    {
        // TODO: play respawn animation, show model etc.
        Debug.Log("Respawn visuals playing");
    }
    
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(CurrentHealth);
            stream.SendNext(IsDead);
        }
        else
        {
            _networkHealth = (float)stream.ReceiveNext();
            IsDead = (bool)stream.ReceiveNext();
        }
    }
}
