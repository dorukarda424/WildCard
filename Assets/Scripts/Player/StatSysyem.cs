using UnityEngine;

public enum StatType { MoveSpeed, SprintSpeed, CrouchSpeed, JumpForce, Gravity, MaxJumps, MaxFallSpeed }

public class StatSystem : MonoBehaviour 
{
    
    public float GetStat(StatType type)
    {
        return type switch
        {
            StatType.MoveSpeed => 6f,
            StatType.JumpForce => 5f,
            StatType.Gravity => 19.62f,
            StatType.MaxJumps => 2f,
            StatType.MaxFallSpeed => 20f
        };
    }
}