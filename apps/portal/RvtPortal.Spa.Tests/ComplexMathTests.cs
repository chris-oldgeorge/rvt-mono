// File summary: Covers vendored numerical helpers used by signal-processing calculations.
// Major updates:
// - 2026-06-26 pending Covered Sonar complexity cleanup for vendored math helpers.
// - 2026-06-26 pending Covered the Complex ISerializable recommended pattern.
// - 2026-06-10 pending Covered near-zero complex-number comparisons for Sonar floating-point equality remediation.

using AForge.Math;
using System.Reflection;
using System.Runtime.Serialization;

namespace RvtPortal.Spa.Tests;

public sealed class ComplexMathTests
{
    [Fact]
    // Function summary: Verifies scalar division treats near-zero floating-point denominators as zero.
    public void Divide_ByNearZeroScalar_ThrowsDivideByZero()
    {
        var value = new Complex(1, 1);

        Assert.Throws<DivideByZeroException>(() => Complex.Divide(value, double.Epsilon));
    }

    [Fact]
    // Function summary: Verifies complex division treats near-zero complex denominators as zero.
    public void Divide_ByNearZeroComplex_ThrowsDivideByZero()
    {
        var value = new Complex(1, 1);
        var divisor = new Complex(double.Epsilon, double.Epsilon);

        Assert.Throws<DivideByZeroException>(() => Complex.Divide(value, divisor));
    }

    [Fact]
    // Function summary: Verifies real-axis complex functions tolerate floating-point noise around zero.
    public void RealAxisFunctions_TreatNearZeroImaginaryPartAsZero()
    {
        var value = new Complex(4, double.Epsilon);

        var squareRoot = Complex.Sqrt(value);
        var sine = Complex.Sin(value);

        Assert.InRange(Math.Abs(squareRoot.Im), 0, 1e-12);
        Assert.InRange(Math.Abs(sine.Im), 0, 1e-12);
    }

    [Fact]
    // Function summary: Verifies Complex follows the recommended ISerializable type shape.
    public void ComplexSerializationPattern_UsesSerializableAttributeAndPrivateConstructor()
    {
        var constructor = typeof(Complex).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(SerializationInfo), typeof(StreamingContext) },
            modifiers: null);

        Assert.NotNull(typeof(Complex).GetCustomAttribute<SerializableAttribute>());
        Assert.NotNull(constructor);
        Assert.True(constructor.IsPrivate);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(65536, 16)]
    [InlineData(65537, 17)]
    [InlineData(int.MaxValue, 31)]
    // Function summary: Verifies the vendored Log2 helper keeps its ceiling-log behavior.
    public void Log2_ReturnsCeilingBinaryLog(int value, int expected)
    {
        Assert.Equal(expected, Tools.Log2(value));
    }

}
