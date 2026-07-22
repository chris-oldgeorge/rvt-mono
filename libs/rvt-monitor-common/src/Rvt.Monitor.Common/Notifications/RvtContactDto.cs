using Rvt.Monitor.Common.Utilities;

namespace Rvt.Monitor.Common.Notifications
{
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


        public RvtContactDto(ContactMethod contactMethod, string emailAddress, string? phoneNumber,
            TimeSpan? sendStartTime, TimeSpan? sendEndTime)
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
        public RvtContactDto(bool useEmail, bool useSms, string emailAddress, string? phoneNumber,
                             TimeSpan? sendStartTime, TimeSpan? sendEndTime)
            : this(contactMethod: FromFlags(email: useEmail, sms: useSms),
                   emailAddress: emailAddress,
                   phoneNumber: phoneNumber,
                   email: useEmail,
                   sms: useSms,
                   sendStartTime: sendStartTime,
                   sendEndTime: sendEndTime)
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

        private static bool ShouldSendAtTime(TimeSpan timeOfDay, TimeSpan? start, TimeSpan? end)
        {
            if (start == null || end == null)
            {
                return true;
            }

            // Convert given time of day to local time to allow for daylight saving
            var localTimeOfDay = DateTimeUtil.UtcToLocal(timeOfDay);

            return localTimeOfDay >= start && localTimeOfDay <= end;
        }

        override public string ToString()
        {
            return string.Format(@"RvtContactDto ContactMethod={0} EmailAddress={1}, PhoneNumber={2}
                                   SendStartTime={3} SendEndTime={4}",
                                   ContactMethod, EmailAddress, PhoneNumber,
                                   SendStartTime, SendEndTime);

        }
    }
}
