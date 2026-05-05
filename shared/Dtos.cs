namespace AutoCo.Shared.DTOs;

// ─── Autenticació ────────────────────────────────────────────────────────────
public record ProfessorLoginRequest(string Email, string Password);
public record StudentLoginRequest(string Email, string Password);
public record LoginResponse(string Token, string NomComplet, string Role, int UserId, string? FotoUrl = null);

// ─── Professors ──────────────────────────────────────────────────────────────
public record ProfessorDto(
    int Id, string Email, string Nom, string Cognoms, string NomComplet,
    bool IsAdmin, DateTime CreatedAt, string? FotoUrl = null);

public record CreateProfessorRequest(
    string Email, string Nom, string Cognoms, bool IsAdmin);

public record UpdateProfessorRequest(
    string Email, string Nom, string Cognoms, bool IsAdmin, string? NewPassword);

public record SendCredentialsResult(bool Sent, string? Reason);
public record SendAllResult(int Sent, int Skipped, List<string> Details);

// ─── Classes ─────────────────────────────────────────────────────────────────
public record ClassDto(
    int Id, string Name, string? AcademicYear, DateTime CreatedAt, int NumStudents);

public record CreateClassRequest(string Name, string? AcademicYear);
public record UpdateClassRequest(string Name, string? AcademicYear);

// ─── Alumnes ─────────────────────────────────────────────────────────────────
public record StudentDto(
    int Id, int ClassId, string Nom, string Cognoms, string NomComplet,
    int NumLlista, string Email, DateTime CreatedAt,
    string? Dni = null, string? FotoUrl = null);

public record CreateStudentRequest(
    string Nom, string Cognoms, int NumLlista, string Email, string? Dni = null);

public record UpdateStudentRequest(
    string Nom, string Cognoms, int NumLlista, string Email, string? Dni = null);

public record MoveStudentRequest(int TargetClassId);
public record BulkMoveStudentsRequest(List<int> StudentIds, int TargetClassId);

public record BulkCreateStudentsRequest(List<CreateStudentRequest> Students);
public record BulkCreateResult(int Created, int Updated, int Skipped, List<string> Errors);
public record ResetPasswordResult(string NewPassword);
public record SendPasswordResult(bool Sent, string? Reason);
public record ChangeStudentPasswordRequest(string CurrentPassword, string NewPassword);
public record ImportFotosResult(int Imported, List<string> NotFound, List<string> Errors);

// ─── Criteris per activitat ──────────────────────────────────────────────────
public record ActivityCriterionDto(int Id, string Key, string Label, int OrderIndex);
public record SaveCriteriaRequest(List<CriterionItem> Items);
public record CriterionItem(string Key, string Label);

// ─── Mòduls ──────────────────────────────────────────────────────────────────
public record ModuleDto(
    int Id, int ClassId, string ClassName, string? ClassAcademicYear,
    int ProfessorId, string ProfessorName,
    string Code, string Name, int ActivityCount, int NumExclusions);

public record CreateModuleRequest(string Code, string Name);
public record UpdateModuleRequest(string Code, string Name);

public record ModuleExclusionDto(int StudentId, string StudentName, string Email);

// ─── Activitats ──────────────────────────────────────────────────────────────
public record ActivityDto(
    int Id,
    int ModuleId, string ModuleCode, string ModuleName,
    int ClassId, string ClassName, string? ClassAcademicYear, string ProfessorName,
    string Name, string? Description, bool IsOpen,
    DateTime CreatedAt, int NumGroups, int NumStudents);

public record CreateActivityRequest(int ModuleId, string Name, string? Description);
public record UpdateActivityRequest(string Name, string? Description);
public record DuplicateActivityRequest(string Name, string? Description);
public record DuplicateCrossRequest(int TargetModuleId, string Name, string? Description);
public record ImportGroupsRequest(string CsvContent);
public record ImportGroupsResult(int Assigned, int Skipped, List<string> Errors);
public record ParticipationDto(int ActivityId, int Submitted, int Total);
public record ReminderResult(int Sent, int Skipped, bool EmailDisabled);
public record InviteTargetDto(int StudentId, string NomComplet, string Email, string GroupName);
public record InviteOneRequest(bool IncludePassword);
public record InviteOneResult(bool Sent, string? Error);

// ─── Grups ───────────────────────────────────────────────────────────────────
public record GroupDto(int Id, int ActivityId, string Name, List<StudentDto> Members, int OrderIndex = 0);
public record ReorderGroupsRequest(List<int> OrderedGroupIds);
public record CreateGroupRequest(string Name);
public record RenameGroupRequest(string Name);
public record AddMemberRequest(int StudentId);

// ─── Avaluacions ─────────────────────────────────────────────────────────────
public record EvaluationFormDto(
    ActivityDto Activity, GroupDto Group, List<EvaluationEntryDto> Entries);

public record EvaluationEntryDto(
    int StudentId, string StudentName, bool IsSelf,
    Dictionary<string, double> Scores, string? Comment, string? FotoUrl = null);

public record SaveEvaluationsRequest(List<EvaluationEntryRequest> Evaluations);

public record EvaluationEntryRequest(
    int EvaluatedId, Dictionary<string, double> Scores, string? Comment);

// ─── Resultats ───────────────────────────────────────────────────────────────
public record ActivityResultsDto(
    ActivityDto Activity, List<StudentResultDto> Students, List<CriteriaDto> Criteria);

public record StudentResultDto(
    int StudentId, string Nom, string Cognoms, string StudentName,
    string? Email, int NumLlista, string GroupName,
    Dictionary<string, double?> SelfScores, string? SelfComment,
    List<PeerEvaluationDto> PeerEvaluations,
    Dictionary<string, double?> AvgCoScores,
    double? AvgGlobal, double? AutAvgGlobal, int NumPeerEvaluators,
    string? FotoUrl = null);

public record PeerEvaluationDto(
    int EvaluatorId, string EvaluatorName,
    Dictionary<string, double> Scores, string? Comment);

public record CriteriaDto(string Key, string Label);

// ─── Gràfiques ────────────────────────────────────────────────────────────────
public record GroupChartDto(
    string GroupName, double? AvgAutoEval, double? AvgCoEval,
    int NumStudentsTotal, int NumStudentsWithAuto, int NumStudentsWithCo);

public record ActivityChartDto(
    int ActivityId, string ActivityName, string ClassName, string? ClassAcademicYear,
    List<GroupChartDto> Groups, List<CriteriaDto> Criteria,
    List<CriteriaGroupChartDto> CriteriaDetail);

public record CriteriaGroupChartDto(
    string CriteriaKey, string CriteriaLabel, List<CriteriaGroupValueDto> Groups);

public record CriteriaGroupValueDto(string GroupName, double? AvgAuto, double? AvgCo);

// ─── Backup / Restore ────────────────────────────────────────────────────────
public record BackupDto(
    string Version, DateTime CreatedAt,
    List<ProfessorBackupDto>  Professors,
    List<ClassBackupDto>      Classes,
    List<ActivityBackupDto>   Activities,
    List<TemplateBackupDto>?  Templates = null);

public record ProfessorBackupDto(
    int Id, string Email, string Nom, string Cognoms,
    bool IsAdmin, string PasswordHash, DateTime CreatedAt);

public record ClassBackupDto(
    int Id, string Name, string? AcademicYear, DateTime CreatedAt,
    List<StudentBackupDto> Students,
    List<ModuleBackupDto>  Modules);

public record StudentBackupDto(
    int Id, string Nom, string Cognoms, int NumLlista,
    string Email, string PasswordHash, DateTime CreatedAt,
    string? PlainPasswordEncrypted = null);

public record ModuleBackupDto(
    int Id, int ProfessorId, string Code, string Name, DateTime CreatedAt,
    List<int> ExcludedStudentIds);

public record CriterionBackupDto(string Key, string Label, int OrderIndex);
public record NoteBackupDto(int StudentId, string Note, DateTime UpdatedAt);
public record TemplateBackupDto(
    int Id, int ProfessorId, string Name, string? Description,
    string CriteriaJson, DateTime CreatedAt);

public record ActivityBackupDto(
    int Id, int ModuleId, string Name, string? Description,
    bool IsOpen, DateTime CreatedAt,
    List<GroupBackupDto>       Groups,
    List<EvaluationBackupDto>  Evaluations,
    List<CriterionBackupDto>?  Criteria  = null,
    List<NoteBackupDto>?       Notes     = null);

public record GroupBackupDto(int Id, string Name, List<int> StudentIds);

public record EvaluationBackupDto(
    int EvaluatorId, int EvaluatedId, bool IsSelf,
    Dictionary<string, double> Scores, string? Comment, DateTime UpdatedAt);

public record BackupFileInfoDto(string Name, DateTime CreatedAt, long SizeBytes);

public record ImportResult(
    bool Success, string? Error,
    int Professors, int Classes, int Students,
    int Modules, int Activities, int Evaluations);

// ─── Notes del professor per alumne ──────────────────────────────────────────
public record ProfessorNoteDto(int StudentId, string Note, DateTime UpdatedAt);
public record SaveNoteRequest(string Note);

// ─── Perfil professor (canvi propi) ──────────────────────────────────────────
public record UpdateOwnProfileRequest(string Nom, string Cognoms, string? CurrentPassword, string? NewPassword);

// ─── Reset de contrasenya (OTP per email) ────────────────────────────────────
public record PasswordResetRequestDto(string Email);
public record PasswordResetConfirmDto(string Email, string Code, string NewPassword);

// ─── Plantilles d'activitat ───────────────────────────────────────────────────
public record ActivityTemplateDto(int Id, string Name, string? Description,
    List<CriterionItem> Criteria, DateTime CreatedAt, string? ProfessorName = null);
public record CreateTemplateRequest(string Name, string? Description, List<CriterionItem> Criteria);

// ─── Registre d'activitat ─────────────────────────────────────────────────────
public record ActivityLogDto(int Id, string Action, string? ActorName, string? Details, DateTime CreatedAt);

// ─── Estadístiques d'administrador ───────────────────────────────────────────
public record AdminStatsDto(
    List<ProfessorStatsDto> Professors,
    List<MonthlyStatDto>    MonthlyLogins,
    List<MonthlyStatDto>    MonthlyActivities);

public record ProfessorStatsDto(
    int Id, string NomComplet, string Email, bool IsAdmin,
    int LoginsLast30, int TotalActivities,
    double AvgParticipation,
    DateTime? LastAccess);

public record MonthlyStatDto(int Year, int Month, int Count);

// ─── Dashboard ───────────────────────────────────────────────────────────────
public record StudentDashboardDto(List<StudentActivityDto> Activities);

public record StudentActivityDto(
    int Id, string Name, string? Description, bool IsOpen,
    string GroupName, int GroupId, int TotalToEvaluate, int AlreadyEvaluated);
