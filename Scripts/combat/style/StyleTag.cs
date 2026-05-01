using System;

[Flags]
public enum StyleTag
{
    None = 0,
    Hit = 1 << 0,
    Kill = 1 << 1,
    WeakPoint = 1 << 2,
    Aerial = 1 << 3,
    Dash = 1 << 4,
    Slide = 1 << 5,
    WallRun = 1 << 6,
    Slam = 1 << 7,
    Swap = 1 << 8,
    Chain = 1 << 9,
    Parry = 1 << 10,
    Finisher = 1 << 11,
    MultiTarget = 1 << 12,
    CloseRange = 1 << 13,
    Risky = 1 << 14,
    Ability = 1 << 15,
    Melee = 1 << 16
}
