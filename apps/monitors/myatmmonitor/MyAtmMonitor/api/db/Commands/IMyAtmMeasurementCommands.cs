using MyAtm.Model.Dto;

namespace MyAtm.Api.Db
{
    public interface IMyAtmMeasurementCommands
    {
        // Legacy single-row members remain on the compatibility facade until page commands migrate callers.
        void InsertDustDtos(List<DustDto> dtos);

        void InsertAccessoryDto(AccessoryInfoDto dto);
    }
}
