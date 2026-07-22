namespace Rvt.Monitor.Common.Rules;

public enum ContactMethod
{
    None = 0,
    Email = 1,
    SMS = 2,
    SMSAndEmail = 3
}

public class RvtContactDto
{
    public ContactMethod ContactMethod { get; }
    public string EmailAddress { get; }
    public string? PhoneNumber { get; }
    public bool Email { get; }
    public bool SMS { get; }
    public TimeSpan? SendStartTime { get; }
    public TimeSpan? SendEndTime { get; }

    public RvtContactDto(
        ContactMethod contactMethod,
        string emailAddress,
        string? phoneNumber,
        TimeSpan? sendStartTime,
        TimeSpan? sendEndTime)
    {
        ContactMethod = contactMethod;
        EmailAddress = emailAddress;
        PhoneNumber = phoneNumber;
        SendStartTime = sendStartTime;
        SendEndTime = sendEndTime;
    }

    public RvtContactDto(
        ContactMethod contactMethod,
        string emailAddress,
        string? phoneNumber,
        bool email,
        bool sms,
        TimeSpan? sendStartTime,
        TimeSpan? sendEndTime)
    {
        ContactMethod = contactMethod;
        EmailAddress = emailAddress;
        PhoneNumber = phoneNumber;
        Email = email;
        SMS = sms;
        SendStartTime = sendStartTime;
        SendEndTime = sendEndTime;
    }

    public RvtContactDto(
        bool useEmail,
        bool useSms,
        string emailAddress,
        string? phoneNumber,
        TimeSpan? sendStartTime,
        TimeSpan? sendEndTime)
        : this(
            FromFlags(useEmail, useSms),
            emailAddress,
            phoneNumber,
            useEmail,
            useSms,
            sendStartTime,
            sendEndTime)
    {
    }

    public static ContactMethod FromFlags(bool email, bool sms)
    {
        if (email && sms)
        {
            return ContactMethod.SMSAndEmail;
        }

        if (email)
        {
            return ContactMethod.Email;
        }

        if (sms)
        {
            return ContactMethod.SMS;
        }

        return ContactMethod.None;
    }

    public bool ShouldSendAtTime(DateTime dateTime)
    {
        if (SendStartTime == null || SendEndTime == null)
        {
            return true;
        }

        return dateTime.TimeOfDay >= SendStartTime && dateTime.TimeOfDay <= SendEndTime;
    }

    public Rvt.Monitor.Common.Notifications.RvtContactDto ToNotificationDto() =>
        new(
            ToNotificationContactMethod(ContactMethod),
            EmailAddress,
            PhoneNumber,
            Email,
            SMS,
            SendStartTime,
            SendEndTime);

    public static RvtContactDto FromNotificationDto(Rvt.Monitor.Common.Notifications.RvtContactDto contact) =>
        new(
            FromNotificationContactMethod(contact.ContactMethod),
            contact.EmailAddress,
            contact.PhoneNumber,
            contact.Email,
            contact.SMS,
            contact.SendStartTime,
            contact.SendEndTime);

    private static Rvt.Monitor.Common.Notifications.ContactMethod ToNotificationContactMethod(ContactMethod contactMethod) =>
        contactMethod switch
        {
            ContactMethod.Email => Rvt.Monitor.Common.Notifications.ContactMethod.Email,
            ContactMethod.SMS => Rvt.Monitor.Common.Notifications.ContactMethod.SMS,
            ContactMethod.SMSAndEmail => Rvt.Monitor.Common.Notifications.ContactMethod.SMSAndEmail,
            _ => Rvt.Monitor.Common.Notifications.ContactMethod.None
        };

    private static ContactMethod FromNotificationContactMethod(Rvt.Monitor.Common.Notifications.ContactMethod contactMethod) =>
        contactMethod switch
        {
            Rvt.Monitor.Common.Notifications.ContactMethod.Email => ContactMethod.Email,
            Rvt.Monitor.Common.Notifications.ContactMethod.SMS => ContactMethod.SMS,
            Rvt.Monitor.Common.Notifications.ContactMethod.SMSAndEmail => ContactMethod.SMSAndEmail,
            _ => ContactMethod.None
        };

    public override string ToString()
    {
        return string.Format(@"RvtContactDto ContactMethod={0} EmailAddress={1}, PhoneNumber={2}
                                   SendStartTime={3} SendEndTime={4}",
            ContactMethod, EmailAddress, PhoneNumber,
            SendStartTime, SendEndTime);
    }
}
