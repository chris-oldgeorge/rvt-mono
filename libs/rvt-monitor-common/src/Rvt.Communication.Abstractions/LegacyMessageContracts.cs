namespace Rvt.Communication.Abstractions;

public enum LegacyMessageKind
{
    Password_Set,
    Alert,
    Caution,
    Offline,
    Battery_Caution,
    Battery_Alert
}

public enum LegacyMessageChannel
{
    Email = 0,
    SMS = 1,
    Both = 2
}
