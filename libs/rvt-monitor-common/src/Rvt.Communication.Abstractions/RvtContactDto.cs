namespace Rvt.Monitor.Common.Notifications;

public enum ContactMethod { None = 0, Email = 1, SMS = 2, SMSAndEmail = 3 }

public class RvtContactDto
{
    public ContactMethod ContactMethod { get; }
    public string EmailAddress { get; }
    public string? PhoneNumber { get; }
    public bool Email { get; }
    public bool SMS { get; }
    public TimeSpan? SendStartTime { get; }
    public TimeSpan? SendEndTime { get; }

    public RvtContactDto(ContactMethod contactMethod, string emailAddress, string? phoneNumber, TimeSpan? sendStartTime, TimeSpan? sendEndTime)
    {
        ContactMethod = contactMethod;
        EmailAddress = emailAddress;
        PhoneNumber = phoneNumber;
        SendStartTime = sendStartTime;
        SendEndTime = sendEndTime;
    }

    public RvtContactDto(ContactMethod contactMethod, string emailAddress, string? phoneNumber, bool email, bool sms, TimeSpan? sendStartTime, TimeSpan? sendEndTime)
    {
        ContactMethod = contactMethod;
        EmailAddress = emailAddress;
        PhoneNumber = phoneNumber;
        Email = email;
        SMS = sms;
        SendStartTime = sendStartTime;
        SendEndTime = sendEndTime;
    }

    public RvtContactDto(bool useEmail, bool useSms, string emailAddress, string? phoneNumber, TimeSpan? sendStartTime, TimeSpan? sendEndTime)
        : this(FromFlags(useEmail, useSms), emailAddress, phoneNumber, useEmail, useSms, sendStartTime, sendEndTime) { }

    public static ContactMethod FromFlags(bool email, bool sms) => (email, sms) switch
    {
        (true, true) => ContactMethod.SMSAndEmail,
        (true, false) => ContactMethod.Email,
        (false, true) => ContactMethod.SMS,
        _ => ContactMethod.None
    };

    public bool ShouldSendAtTime(DateTime dateTime) =>
        SendStartTime is null || SendEndTime is null ||
        (dateTime.TimeOfDay >= SendStartTime && dateTime.TimeOfDay <= SendEndTime);

    public override string ToString() => string.Format(@"RvtContactDto ContactMethod={0} EmailAddress={1}, PhoneNumber={2}
                                   SendStartTime={3} SendEndTime={4}", ContactMethod, EmailAddress, PhoneNumber, SendStartTime, SendEndTime);
}
