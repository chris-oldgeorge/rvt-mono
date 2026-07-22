// File summary: Provides compile-time generated mapping for legacy search projection repositories.
// Major updates:
// - 2026-06-25 pending Replaced reflection-based repository projection copies with Mapperly mappings.

using Riok.Mapperly.Abstractions;
using RVT.DataAccess.EntityModels.Models;

namespace RVT.DataAccess;

[Mapper]
internal static partial class SearchProjectionMapper
{
    // Function summary: Maps an eight-hour dust average projection into the common dust level shape.
    [MapperIgnoreTarget(nameof(MyAtmDustLevel.Avrg))]
    [MapperIgnoreTarget(nameof(MyAtmDustLevel.WeatherT))]
    [MapperIgnoreTarget(nameof(MyAtmDustLevel.WeatherP))]
    [MapperIgnoreTarget(nameof(MyAtmDustLevel.WeatherRh))]
    public static partial MyAtmDustLevel ToMyAtmDustLevel(MyAtmDustLevel8hourAvg source);

    // Function summary: Clones a dust level projection without reflection.
    public static partial MyAtmDustLevel CloneMyAtmDustLevel(MyAtmDustLevel source);

    // Function summary: Maps one-minute Omnidots peak projections into the common Omnidots peak shape.
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.Xfdom))]
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.XvtopOverflow))]
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.Yfdom))]
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.YvtopOverflow))]
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.Zfdom))]
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.ZvtopOverflow))]
    public static partial OmnidotsPeakLevel ToOmnidotsPeakLevel(OmnidotsPeakLevel1min source);

    // Function summary: Maps five-minute Omnidots peak projections into the common Omnidots peak shape.
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.Xfdom))]
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.XvtopOverflow))]
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.Yfdom))]
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.YvtopOverflow))]
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.Zfdom))]
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.ZvtopOverflow))]
    public static partial OmnidotsPeakLevel ToOmnidotsPeakLevel(OmnidotsPeakLevel5min source);

    // Function summary: Maps fifteen-minute Omnidots peak projections into the common Omnidots peak shape.
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.Xfdom))]
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.XvtopOverflow))]
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.Yfdom))]
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.YvtopOverflow))]
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.Zfdom))]
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.ZvtopOverflow))]
    public static partial OmnidotsPeakLevel ToOmnidotsPeakLevel(OmnidotsPeakLevel15min source);

    // Function summary: Maps twenty-minute Omnidots peak projections into the common Omnidots peak shape.
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.Xfdom))]
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.XvtopOverflow))]
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.Yfdom))]
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.YvtopOverflow))]
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.Zfdom))]
    [MapperIgnoreTarget(nameof(OmnidotsPeakLevel.ZvtopOverflow))]
    public static partial OmnidotsPeakLevel ToOmnidotsPeakLevel(OmnidotsPeakLevel20min source);

    // Function summary: Clones an Omnidots peak projection without reflection.
    public static partial OmnidotsPeakLevel CloneOmnidotsPeakLevel(OmnidotsPeakLevel source);
}
