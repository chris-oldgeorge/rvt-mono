using System.Globalization;
using RvtPortal.Application.Common;

namespace RvtPortal.Application.Sites;

public sealed record ValidatedSiteOperatingHours(
    int DayOfWeek,
    TimeSpan? StartTime,
    TimeSpan? EndTime,
    bool IsClosed);

public sealed record ValidatedSiteMutation(
    SiteMutation Source,
    TimeSpan? StartTime,
    TimeSpan? EndTime,
    TimeSpan? SaturdayStartTime,
    TimeSpan? SaturdayEndTime,
    TimeSpan? SundayStartTime,
    TimeSpan? SundayEndTime,
    IReadOnlyList<ValidatedSiteOperatingHours> OperatingHours);

public sealed record SiteMutationValidationResult(
    IReadOnlyList<UseCaseError> Errors,
    ValidatedSiteMutation? Value)
{
    public bool IsValid => Errors.Count == 0 && Value is not null;
}

public sealed record SiteTimePairValidationResult(
    IReadOnlyList<UseCaseError> Errors,
    TimeSpan? StartTime,
    TimeSpan? EndTime)
{
    public bool IsValid => Errors.Count == 0;
}

public static class SiteMutationValidator
{
    public static SiteMutationValidationResult ValidateShape(SiteMutation request)
    {
        var errors = new List<UseCaseError>();
        var siteName = request.SiteName?.Trim();
        if (string.IsNullOrWhiteSpace(siteName))
        {
            errors.Add(new UseCaseError(
                nameof(SiteMutation.SiteName),
                "The Site Name is required"));
        }
        else if (siteName.Length > 100)
        {
            errors.Add(new UseCaseError(
                nameof(SiteMutation.SiteName),
                "Site name must be 100 characters or fewer."));
        }

        ValidateMaxLength(
            nameof(SiteMutation.AddressLine1),
            request.AddressLine1,
            100,
            errors);
        ValidateMaxLength(
            nameof(SiteMutation.AddressLine2),
            request.AddressLine2,
            100,
            errors);
        ValidateMaxLength(
            nameof(SiteMutation.Postcode),
            request.Postcode,
            10,
            errors);
        ValidateMaxLength(
            nameof(SiteMutation.City),
            request.City,
            30,
            errors);
        ValidateMaxLength(
            nameof(SiteMutation.County),
            request.County,
            30,
            errors);

        var weekday = ValidateTimePair(
            request.StartTime,
            request.EndTime,
            nameof(SiteMutation.StartTime));
        var saturday = ValidateTimePair(
            request.SatStartTime,
            request.SatEndTime,
            nameof(SiteMutation.SatStartTime));
        var sunday = ValidateTimePair(
            request.SunStartTime,
            request.SunEndTime,
            nameof(SiteMutation.SunStartTime));
        errors.AddRange(weekday.Errors);
        errors.AddRange(saturday.Errors);
        errors.AddRange(sunday.Errors);

        var operatingHours = ValidateOperatingHours(request, errors);
        if (errors.Count > 0)
        {
            return new SiteMutationValidationResult(errors, null);
        }

        var normalized = request with
        {
            SiteName = siteName!,
            AddressLine1 = EmptyToNull(request.AddressLine1),
            AddressLine2 = EmptyToNull(request.AddressLine2),
            Postcode = EmptyToNull(request.Postcode),
            City = EmptyToNull(request.City),
            County = EmptyToNull(request.County)
        };
        return new SiteMutationValidationResult(
            [],
            new ValidatedSiteMutation(
                normalized,
                weekday.StartTime,
                weekday.EndTime,
                saturday.StartTime,
                saturday.EndTime,
                sunday.StartTime,
                sunday.EndTime,
                operatingHours));
    }

    public static SiteMutationValidationResult ValidateBusinessRules(
        SiteMutationValidationResult shape,
        SiteMutationValidationData data,
        bool requireContract)
    {
        if (!shape.IsValid)
        {
            return shape;
        }

        var errors = new List<UseCaseError>();
        if (data.DuplicateSiteName)
        {
            errors.Add(new UseCaseError(
                nameof(SiteMutation.SiteName),
                "The Site Name is already registered"));
        }

        if (!data.CompanyExists)
        {
            errors.Add(new UseCaseError(
                nameof(SiteMutation.CompanyId),
                "The Company is required"));
        }

        if (requireContract)
        {
            var contractId = shape.Value!.Source.ContractId;
            if (!contractId.HasValue ||
                contractId.Value == Guid.Empty ||
                !data.ContractExists)
            {
                errors.Add(new UseCaseError(
                    nameof(SiteMutation.ContractId),
                    "The Contract is Required"));
            }
            else if (!data.ContractBelongsToCompany)
            {
                errors.Add(new UseCaseError(
                    nameof(SiteMutation.ContractId),
                    "The Contract must belong to the selected company."));
            }
            else if (!data.ContractIsUnassigned)
            {
                errors.Add(ContractAlreadyAssignedError());
            }
        }

        return errors.Count == 0
            ? shape
            : new SiteMutationValidationResult(errors, null);
    }

    public static UseCaseError ContractAlreadyAssignedError() =>
        new(
            nameof(SiteMutation.ContractId),
            "The Contract is already assigned to a site.");

    public static SiteTimePairValidationResult ValidateTimePair(
        string? startValue,
        string? endValue,
        string field)
    {
        var errors = new List<UseCaseError>();
        var start = ParseOptionalTime(startValue, field, errors);
        var end = ParseOptionalTime(endValue, field, errors);
        ValidateTimePair(field, start, end, errors);
        return new SiteTimePairValidationResult(errors, start, end);
    }

    private static IReadOnlyList<ValidatedSiteOperatingHours> ValidateOperatingHours(
        SiteMutation request,
        List<UseCaseError> errors)
    {
        var parsedByDay = new Dictionary<int, ValidatedSiteOperatingHours>();
        var supplied = request.OperatingHours is { Count: > 0 }
            ? request.OperatingHours
            : LegacyOperatingHours(request);
        var seenDays = new HashSet<int>();

        foreach (var hours in supplied)
        {
            var key = $"{nameof(SiteMutation.OperatingHours)}[{hours.DayOfWeek}]";
            if (hours.DayOfWeek is < 1 or > 7 ||
                !seenDays.Add(hours.DayOfWeek))
            {
                errors.Add(new UseCaseError(
                    key,
                    "Operating hours must use unique days from 1 to 7."));
                continue;
            }

            if (hours.IsClosed)
            {
                parsedByDay[hours.DayOfWeek] = new ValidatedSiteOperatingHours(
                    hours.DayOfWeek,
                    null,
                    null,
                    true);
                continue;
            }

            var pair = ValidateTimePair(hours.StartTime, hours.EndTime, key);
            errors.AddRange(pair.Errors);
            parsedByDay[hours.DayOfWeek] = new ValidatedSiteOperatingHours(
                hours.DayOfWeek,
                pair.StartTime,
                pair.EndTime,
                !pair.StartTime.HasValue && !pair.EndTime.HasValue);
        }

        return Enumerable.Range(1, 7)
            .Select(day => parsedByDay.TryGetValue(day, out var hours)
                ? hours
                : new ValidatedSiteOperatingHours(day, null, null, true))
            .ToList();
    }

    private static IReadOnlyList<SiteOperatingHoursMutation> LegacyOperatingHours(
        SiteMutation request) =>
        [
            new(1, request.StartTime, request.EndTime, IsClosed(request.StartTime, request.EndTime)),
            new(2, request.StartTime, request.EndTime, IsClosed(request.StartTime, request.EndTime)),
            new(3, request.StartTime, request.EndTime, IsClosed(request.StartTime, request.EndTime)),
            new(4, request.StartTime, request.EndTime, IsClosed(request.StartTime, request.EndTime)),
            new(5, request.StartTime, request.EndTime, IsClosed(request.StartTime, request.EndTime)),
            new(6, request.SatStartTime, request.SatEndTime, IsClosed(request.SatStartTime, request.SatEndTime)),
            new(7, request.SunStartTime, request.SunEndTime, IsClosed(request.SunStartTime, request.SunEndTime))
        ];

    private static TimeSpan? ParseOptionalTime(
        string? value,
        string field,
        List<UseCaseError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (TimeSpan.TryParseExact(
            value,
            "hh\\:mm",
            CultureInfo.InvariantCulture,
            out var parsed))
        {
            return parsed;
        }

        errors.Add(new UseCaseError(
            field,
            "Time values must use HH:mm format."));
        return null;
    }

    private static void ValidateTimePair(
        string field,
        TimeSpan? start,
        TimeSpan? end,
        List<UseCaseError> errors)
    {
        if (start.HasValue != end.HasValue)
        {
            errors.Add(new UseCaseError(
                field,
                "You need to set both start and end time"));
            return;
        }

        if (start.HasValue && start.Value >= end.GetValueOrDefault())
        {
            errors.Add(new UseCaseError(
                field,
                "Start time needs to be before end time"));
        }
    }

    private static void ValidateMaxLength(
        string field,
        string? value,
        int maxLength,
        List<UseCaseError> errors)
    {
        if (value?.Length > maxLength)
        {
            errors.Add(new UseCaseError(
                field,
                $"{field} must be {maxLength} characters or fewer."));
        }
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsClosed(string? startTime, string? endTime) =>
        string.IsNullOrWhiteSpace(startTime) &&
        string.IsNullOrWhiteSpace(endTime);
}
