using MyAtm.Model.Dto;

namespace MyAtm.Api.Db;

public interface IMyAtmDustImportCommands
{
    Task<DustImportCommitResult> CommitDustImportAsync(
        MyAtmDustImportCommit commit,
        CancellationToken cancellationToken = default);
}
