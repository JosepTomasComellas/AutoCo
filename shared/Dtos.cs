namespace AutoCo.Shared.DTOs;

// ─── Autenticació ────────────────────────────────────────────────────────────
public record ProfessorLoginRequest(string Email, string Password);
public record StudentLoginRequest(string Email, string Password);
public record LoginResponse(string Token, string NomComplet, string Role, int UserId);

// ─── Professors ──────────────────────────────────────────────────────────────
public record ProfessorDto(
    int Id, string Email, string Nom, string Cognoms, string NomComplet,
    bool IsAdmin, DateTime CreatedAt);

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
    int NumLlista, string Email, DateTime CreatedAt);

public record CreateStudentRequest(
    string Nom, string Cognoms, int NumLlista, string Email);

public record UpdateStudentRequest(
    string Nom, string Cognoms, int NumLlista, string Email);

public record BulkCreateStudentsRequest(List<CreateStudentRequest> Students);
public record BulkCreateResult(int Created, int Skipped, List<string> Errors);
public record ResetPasswordResult(string NewPassword);
public record SendPasswordResult(bool Sent, string? Reason);

// ─── Mòduls ──────────────────────────────────────────────────────────────────
public record ModuleDto(
    int Id, int ClassId, string ClassName, string? ClassAcademicYear,
    int ProfessorId, string ProfessorName,
    string Code, string Name, int ActivityCount);

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
public record ImportGroupsRequest(string CsvContent);
public record ImportGroupsResult(int Assigned, int Skipped, List<string> Errors);

// ─── Grups ───────────────────────────────────────────────────────────────────
public record GroupDto(int Id, int ActivityId, string Name, List<StudentDto> Members);
public record CreateGroupRequest(string Name);
public record AddMemberRequest(int StudentId);

// ─── Avaluacions ─────────────────────────────────────────────────────────────
public record EvaluationFormDto(
    ActivityDto Activity, GroupDto Group, List<EvaluationEntryDto> Entries);

public record EvaluationEntryDto(
    int StudentId, string StudentName, bool IsSelf,
    Dictionary<string, double> Scores, string? Comment);

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
    double? AvgGlobal, double? AutAvgGlobal, int NumPeerEvaluators);

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
    List<ActivityBackupDto>   Activities);

public record ProfessorBackupDto(
    int Id, string Email, string Nom, string Cognoms,
    bool IsAdmin, string PasswordHash, DateTime CreatedAt);

public record ClassBackupDto(
    int Id, string Name, string? AcademicYear, DateTime CreatedAt,
    List<StudentBackupDto> Students,
    List<ModuleBackupDto>  Modules);

public record StudentBackupDto(
    int Id, string Nom, string Cognoms, int NumLlista,
    string Email, string PasswordHash, DateTime CreatedAt);

public record ModuleBackupDto(
    int Id, int ProfessorId, string Code, string Name, DateTime CreatedAt,
    List<int> ExcludedStudentIds);

public record ActivityBackupDto(
    int Id, int ModuleId, string Name, string? Description,
    bool IsOpen, DateTime CreatedAt,
    List<GroupBackupDto>      Groups,
    List<EvaluationBackupDto> Evaluations);

public record GroupBackupDto(int Id, string Name, List<int> StudentIds);

public record EvaluationBackupDto(
    int EvaluatorId, int EvaluatedId, bool IsSelf,
    Dictionary<string, double> Scores, string? Comment, DateTime UpdatedAt);

public record BackupFileInfoDto(string Name, DateTime CreatedAt, long SizeBytes);

public record ImportResult(
    bool Success, string? Error,
    int Professors, int Classes, int Students,
    int Modules, int Activities, int Evaluations);

// ─── Dashboard ───────────────────────────────────────────────────────────────
public record StudentDashboardDto(List<StudentActivityDto> Activities);

public record StudentActivityDto(
    int Id, string Name, string? Description, bool IsOpen,
    string GroupName, int GroupId, int TotalToEvaluate, int AlreadyEvaluated);
