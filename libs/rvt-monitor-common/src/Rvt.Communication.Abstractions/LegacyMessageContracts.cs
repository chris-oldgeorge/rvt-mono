namespace Rvt.Communication.Abstractions;

public enum LegacyMessageKind
{
    Password_Set,
    Password_Forgotten,
    Alert,
    Caution,
    Offline,
    Battery_Caution,
    Battery_Alert,
    Report_Weekly,
    Report_Monthly
}

public enum LegacyMessageChannel
{
    Email = 0,
    SMS = 1,
    Both = 2
}
