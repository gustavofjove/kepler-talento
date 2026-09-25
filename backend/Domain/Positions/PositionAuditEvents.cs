namespace KeplerTalento.Domain.Positions;

public static class PositionAuditEvents
{
    public const string Created = "position.created";
    public const string Updated = "position.updated";
    public const string StatusChanged = "position.status_changed";

    // KTL-30. The subject is "{positionId:N}:{candidateId:N}"; the stage is never recorded.
    public const string CandidateAdded = "position.candidate_added";
    public const string CandidateStageChanged = "position.candidate_stage_changed";
    public const string CandidateRemoved = "position.candidate_removed";
}
