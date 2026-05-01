using UnityEngine;

public class MoveSpeedItem : ItemBase
{
    [SerializeField] private float _moveSpeedBonus = 1f;

    public override void ApplyBuff()
    {
        var motor = Controller.GetComponent<NetworkPlayerMotor>();
        if (motor != null)
        {
            motor.AddMoveSpeedBonus(_moveSpeedBonus);
        }
    }

    public override void RemoveBuff()
    {
        var motor = Controller.GetComponent<NetworkPlayerMotor>();
        if (motor != null)
        {
            motor.RemoveMoveSpeedBonus(_moveSpeedBonus);
        }
    }
}