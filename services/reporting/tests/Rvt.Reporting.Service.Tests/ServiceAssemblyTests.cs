namespace Rvt.Reporting.Service.Tests;

/// <summary>
/// Keeps the service test project discoverable while endpoint-level tests are added later.
/// Major updates: 2026-06-24 initial service test project smoke coverage.
/// </summary>
public sealed class ServiceAssemblyTests
{
    [Fact]
    public void ProgramTypeIsAvailable()
    {
        Assert.Equal("Program", typeof(Program).Name);
    }
}
