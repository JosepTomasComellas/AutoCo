namespace AutoCo.Api.DTOs;

// ─── Autenticació ────────────────────────────────────────────────────────────

public record ProfessorLoginRequest(string Username, string Password);
public record StudentLoginRequest(int ClassId, int NumLlista, string Pin);
public record LoginResponse(string Token, string NomComplet, string Role, int UserId);

// ─── Professors ──────────────────────────────────────────────────────────────

public record ProfessorDto(
    int Id, string Username, string Nom, string Cognoms, string NomComplet,
    string? CorreuElectronic, bool IsAdmin, DateTime CreatedAt, int NumClasses);

public record CreateProfessorRequest(
    string Username, string Password, string Nom, string Cognoms,
    string? CorreuElectronic, bool IsAdmin);

public record UpdateProfessorRequest(
    string Nom, string Cognoms, string? CorreuElectronic, bool IsAdmin, string? NewPassword);

// ─── Classes ─────────────────────────────────────────────────────────────────

public record ClassDto(
    int Id, int ProfessorId, string ProfessorName,
    string Name, string? AcademicYear, DateTime CreatedAt, int NumStudents);

public record CreateClassRequest(string Name, string? AcademicYear);
public record UpdateClassRequest(string Name, string? AcademicYear);

// ─── Alumnes ─────────────────────────────────────────────────────────────────

public record StudentDto(
    int Id, int ClassId, string Nom, string Cognoms, string NomComplet,
    int NumLlista, DateTime CreatedAt);

public record StudentWithPinDto(
    int Id, int ClassId, string Nom, string Cognoms, string NomComplet,
    int NumLlista, string Pin, string? CorreuElectronic, DateTime CreatedAt);

public record CreateStudentRequest(
    string Nom, string Cognoms, int NumLlista, string? Pin, string? CorreuElectronic);

public record UpdateStudentRequest(
    string Nom, string Cognoms, int NumLlista, string? NewPin, string? CorreuElectronic);

public record BulkCreateStudentsRequest(List<CreateStudentRequest> Students);
public record BulkCreateResult(int Created, int Skipped, List<string> Errors);
public record ResetPinResult(string NewPin);
public record SendPinResult(bool Sent, string? Reason);
public record SendAllResult(int Sent, int Skipped, List<string> Details);
public record SendCredentialsResult(bool Sent, string? Reason);

// ─── Activitats ──────────────────────────────────────────────────────────────

public record ActivityDto(
    int Id, int ClassId, string ClassName, string? ClassAcademicYear, string ProfessorName,
    string Name, string? Description, bool IsOpen,
    DateTime CreatedAt, int NumGroups, int NumStudents);

public record CreateActivityRequest(int ClassId, string Name, string? Description);
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
    ActivityDto Activity,
    GroupDto    Group,
    List<EvaluationEntryDto> Entries);

public record EvaluationEntryDto(
    int StudentId, string StudentName, bool IsSelf,
    Dictionary<string, int> Scores, string? Comment);

public record SaveEvaluationsRequest(
    List<EvaluationEntryRequest> Evaluations);

public record EvaluationEntryRequest(
    int EvaluatedId,
    Dictionary<string, int> Scores,
    string? Comment);

// ─── Resultats ───────────────────────────────────────────────────────────────

public record ActivityResultsDto(
    ActivityDto Activity,
    List<StudentResultDto> Students,
    List<CriteriaDto> Criteria);

public record StudentResultDto(
    int    StudentId,
    string Nom,
    string Cognoms,
    string StudentName,
    string? CorreuElectronic,
    int    NumLlista,
    string GroupName,
    Dictionary<string, int?>  SelfScores,
    string? SelfComment,
    List<PeerEvaluationDto>   PeerEvaluations,
    Dictionary<string, double?> AvgCoScores,
    double? AvgGlobal,
    double? AutAvgGlobal,
    int    NumPeerEvaluators);

public record PeerEvaluationDto(
    int EvaluatorId, string EvaluatorName,
    Dictionary<string, int> Scores, string? Comment);

public record CriteriaDto(string Key, string Label);

// ─── Gràfiques ────────────────────────────────────────────────────────────────

public record GroupChartDto(
    string GroupName,
    double? AvgAutoEval,
    double? AvgCoEval,
    int NumStudentsTotal,
    int NumStudentsWithAuto,
    int NumStudentsWithCo);

public record ActivityChartDto(
    int    ActivityId,
    string ActivityName,
    string ClassName,
    string? ClassAcademicYear,
    List<GroupChartDto> Groups,
    List<CriteriaDto>   Criteria,
    List<CriteriaGroupChartDto> CriteriaDetail);

public record CriteriaGroupChartDto(
    string CriteriaKey,
    string CriteriaLabel,
    List<CriteriaGroupValueDto> Groups);

public record CriteriaGroupValueDto(string GroupName, double? AvgAuto, double? AvgCo);

// ─── Dashboard ───────────────────────────────────────────────────────────────

public record StudentDashboardDto(
    List<StudentActivityDto> Activities);

public record StudentActivityDto(
    int Id, string Name, string? Description, bool IsOpen,
    string GroupName, int GroupId,
    int TotalToEvaluate, int AlreadyEvaluated);
