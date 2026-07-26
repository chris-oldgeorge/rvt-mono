using RvtPortal.Application.Sites;

namespace RvtPortal.Application.Tests.Sites;

public sealed class SiteMutationValidatorTests
{
    [Theory]
    [InlineData(nameof(SiteMutation.EndTime), nameof(SiteMutation.StartTime))]
    [InlineData(nameof(SiteMutation.SatEndTime), nameof(SiteMutation.SatStartTime))]
    [InlineData(nameof(SiteMutation.SunEndTime), nameof(SiteMutation.SunStartTime))]
    public void ValidateShape_MalformedLegacyEndTimeReportsExactLegacyFields(
        string endField,
        string startField)
    {
        var request = WithMalformedEndTime(ValidMutation(), endField);

        var result = SiteMutationValidator.ValidateShape(request);

        Assert.False(result.IsValid);
        Assert.Collection(
            result.Errors,
            error =>
            {
                Assert.Equal(endField, error.Field);
                Assert.Equal("Time values must use HH:mm format.", error.Message);
            },
            error =>
            {
                Assert.Equal(startField, error.Field);
                Assert.Equal("You need to set both start and end time", error.Message);
            });
    }

    [Fact]
    public void ValidateShape_ReversedLegacyWeekdayPairReportsOnlyStartTime()
    {
        var request = ValidMutation() with
        {
            StartTime = "17:00",
            EndTime = "08:00"
        };

        var result = SiteMutationValidator.ValidateShape(request);

        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(SiteMutation.StartTime), error.Field);
        Assert.Equal("Start time needs to be before end time", error.Message);
    }

    private static SiteMutation WithMalformedEndTime(
        SiteMutation request,
        string endField) =>
        endField switch
        {
            nameof(SiteMutation.EndTime) => request with
            {
                StartTime = "08:00",
                EndTime = "not-a-time"
            },
            nameof(SiteMutation.SatEndTime) => request with
            {
                SatStartTime = "08:00",
                SatEndTime = "not-a-time"
            },
            nameof(SiteMutation.SunEndTime) => request with
            {
                SunStartTime = "08:00",
                SunEndTime = "not-a-time"
            },
            _ => throw new ArgumentOutOfRangeException(nameof(endField))
        };

    private static SiteMutation ValidMutation() =>
        new(
            "Validator Site",
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            []);
}
