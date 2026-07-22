// File summary: Centralizes the per-table time-series projections the legacy repositories used to embed, exposed for the generic reader.
// Major updates:
// - 2026-07-09 pending Extracted the twelve repository row projections into one place during time-series consolidation.

using RVT.DataAccess.EntityModels.Models;

namespace RVT.DataAccess
{
    // Function summary: Projection functions mapping each time-series source row into the shape the monitor read paths expect.
    public static class TimeSeriesProjections
    {
        // ----- Dust -----

        // Function summary: Clones a dust level row into a detached projection.
        public static MyAtmDustLevel DustLevel(MyAtmDustLevel source)
            => SearchProjectionMapper.CloneMyAtmDustLevel(source);

        // Function summary: Projects an eight-hour dust average row into the common dust level shape.
        public static MyAtmDustLevel DustLevelFromEightHour(MyAtmDustLevel8hourAvg source)
            => SearchProjectionMapper.ToMyAtmDustLevel(source);

        // ----- Noise -----

        // Function summary: Passes a fifteen-minute noise average row through unchanged.
        public static NoiseLevel15minAvg NoiseLevel(NoiseLevel15minAvg source)
            => source;

        // Function summary: Projects a one-hour noise average row into the common noise level shape.
        public static NoiseLevel15minAvg NoiseLevelFromHour(NoiseLevel1hourAvg entity)
        {
            NoiseLevel15minAvg item = new NoiseLevel15minAvg();
            item.SerialId = entity.SerialId;
            item.SampleTime = entity.SampleTime;
            item.Laeq = entity.Laeq == null ? 0 : entity.Laeq.Value;
            item.Lamax = entity.Lamax == null ? 0 : entity.Lamax.Value;
            item.La90 = entity.La90 == null ? 0 : entity.La90.Value;
            item.La10 = entity.La10 == null ? 0 : entity.La10.Value;
            item.Lceq = entity.Lceq == null ? 0 : entity.Lceq.Value;
            item.Lcmax = entity.Lcmax == null ? 0 : entity.Lcmax.Value;
            item.Lc90 = entity.Lc90 == null ? 0 : entity.Lc90.Value;
            item.Lc10 = entity.Lc10 == null ? 0 : entity.Lc10.Value;
            return item;
        }

        // Function summary: Projects a one-day noise average row into the common noise level shape.
        public static NoiseLevel15minAvg NoiseLevelFromDay(NoiseLevel1dayAvg entity)
        {
            NoiseLevel15minAvg item = new NoiseLevel15minAvg();
            item.SerialId = entity.SerialId;
            item.SampleTime = entity.SampleTime.GetValueOrDefault(); //Can't be null here so doesn't matter
            item.Laeq = entity.Laeq == null ? 0 : entity.Laeq.Value;
            item.Lamax = entity.Lamax == null ? 0 : entity.Lamax.Value;
            item.La90 = entity.La90 == null ? 0 : entity.La90.Value;
            item.La10 = entity.La10 == null ? 0 : entity.La10.Value;
            item.Lceq = entity.Lceq == null ? 0 : entity.Lceq.Value;
            item.Lcmax = entity.Lcmax == null ? 0 : entity.Lcmax.Value;
            item.Lc90 = entity.Lc90 == null ? 0 : entity.Lc90.Value;
            item.Lc10 = entity.Lc10 == null ? 0 : entity.Lc10.Value;
            return item;
        }

        // Function summary: Projects a site-average noise row into the common noise level shape.
        public static NoiseLevel15minAvg NoiseLevelFromSite(NoiseLevelSiteAvg entity)
        {
            NoiseLevel15minAvg item = new NoiseLevel15minAvg();
            item.SerialId = entity.SerialId;
            item.SampleTime = entity.SampleTime;
            item.Laeq = entity.Laeq == null ? 0 : entity.Laeq.Value;
            item.Lamax = entity.Lamax == null ? 0 : entity.Lamax.Value;
            item.La90 = entity.La90 == null ? 0 : entity.La90.Value;
            item.La10 = entity.La10 == null ? 0 : entity.La10.Value;
            item.Lceq = entity.Lceq == null ? 0 : entity.Lceq.Value;
            item.Lcmax = entity.Lcmax == null ? 0 : entity.Lcmax.Value;
            item.Lc90 = entity.Lc90 == null ? 0 : entity.Lc90.Value;
            item.Lc10 = entity.Lc10 == null ? 0 : entity.Lc10.Value;
            return item;
        }

        // ----- Vibration / Omnidots peak -----

        // Function summary: Clones an Omnidots peak row into a detached projection.
        public static OmnidotsPeakLevel PeakLevel(OmnidotsPeakLevel source)
            => SearchProjectionMapper.CloneOmnidotsPeakLevel(source);

        // Function summary: Projects a one-minute Omnidots peak row into the common peak shape.
        public static OmnidotsPeakLevel PeakLevelFrom1Min(OmnidotsPeakLevel1min source)
            => SearchProjectionMapper.ToOmnidotsPeakLevel(source);

        // Function summary: Projects a five-minute Omnidots peak row into the common peak shape.
        public static OmnidotsPeakLevel PeakLevelFrom5Min(OmnidotsPeakLevel5min source)
            => SearchProjectionMapper.ToOmnidotsPeakLevel(source);

        // Function summary: Projects a fifteen-minute Omnidots peak row into the common peak shape.
        public static OmnidotsPeakLevel PeakLevelFrom15Min(OmnidotsPeakLevel15min source)
            => SearchProjectionMapper.ToOmnidotsPeakLevel(source);

        // Function summary: Projects a twenty-minute Omnidots peak row into the common peak shape.
        public static OmnidotsPeakLevel PeakLevelFrom20Min(OmnidotsPeakLevel20min source)
            => SearchProjectionMapper.ToOmnidotsPeakLevel(source);

        // Function summary: Passes a one-day Omnidots peak row through unchanged.
        public static OmnidotsPeakLevel1dayPeak PeakLevelDay(OmnidotsPeakLevel1dayPeak source)
            => source;
    }
}
