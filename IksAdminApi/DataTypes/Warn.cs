namespace IksAdminApi;

public class Warn
{
    public int Id {get; set;}
    public int AdminId {get; set;}
    public int? IssuedImmunity { get; set; }
    public int TargetId {get; set;}
    public ulong? TargetSteamId { get; set; }
    public int Duration {get; set;}
    public string Reason {get; set;}
    public bool IsTest => string.Equals(Reason?.Trim(), "test", StringComparison.OrdinalIgnoreCase);
    public int CreatedAt {get; set;} = AdminUtils.CurrentTimestamp();

    public int EndAt { get; set; } = 0;

    public int UpdatedAt {get; set;} = AdminUtils.CurrentTimestamp();
    public int? DeletedAt {get; set;} = null;
    public int? DeletedBy {get; set;} = null;
    public bool IsPlayerWarning { get; set; }

    public Warn() { }

    public Admin? Admin {get {
        return AdminUtils.Admin(AdminId);
    }}
    public Admin? TargetAdmin {get {
        return IsPlayerWarning && TargetSteamId is not null
            ? AdminUtils.Admin(TargetSteamId.Value.ToString()) : AdminUtils.Admin(TargetId);
    }}
    public Admin? DeletedByAdmin {get {
        return DeletedBy == null ? null : AdminUtils.Admin((int)DeletedBy);
    }}

    public Warn(
        int id,
        int adminId,
        int targetId,
        int duration,
        string reason,
        int createdAt,
        int updatedAt,
        int endAt,
        int? deletedAt,
        int? deletedBy
    )
    {
        Id = id;
        AdminId = adminId;
        TargetId = targetId;
        Duration = duration;
        Reason = reason;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        DeletedAt = deletedAt;
        DeletedBy = deletedBy;
        EndAt = endAt;
    }
    public Warn(
        int adminId,
        int targetId,
        int duration,
        string reason
    ) {
        AdminId = adminId;   
        TargetId = targetId;  
        Duration = 0;
        Reason = reason;
        EndAt = 0;
    }
    public void SetEndAt()
    {
        EndAt = Duration == 0 ? 0 : AdminUtils.CurrentTimestamp() + Duration;
    }
}
