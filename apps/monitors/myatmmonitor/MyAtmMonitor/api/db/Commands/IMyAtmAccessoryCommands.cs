using MyAtm.Model.Dto;

namespace MyAtm.Api.Db;

public interface IMyAtmAccessoryCommands
{
    Task InsertAccessoryPageAsync(
        IReadOnlyList<AccessoryInfoDto> page,
        CancellationToken cancellationToken = default);
}
