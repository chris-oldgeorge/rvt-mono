using MyAtm.Model.Dto;

namespace MyAtm.Api.Db;

public interface IMyAtmAlertCommitCommands
{
    Task<MyAtmAlertCommitResult> CommitAlertAsync(
        MyAtmAlertCommit commit,
        CancellationToken cancellationToken = default);
}
