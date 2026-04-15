using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoCo.Web.Services;

// ─── Models compartits amb l'API ──────────────────────────────────────────────

public record LoginResponse(string Token, string NomComplet, string Role, int UserId);
public record ProfessorLoginRequest(string Username, string Password);
public record StudentLoginRequest(int ClassId, int NumLlista, string Pin);

public record ClassDto(int Id, int ProfessorId, string ProfessorName,
    string Name, string? AcademicYear, DateTime CreatedAt, int NumStudents);
public record CreateClassRequest(string Name, string? AcademicYear);
public record UpdateClassRequest(string Name, string? AcademicYear);

public record StudentDto(int Id, int ClassId, string Nom, string Cognoms, string NomComplet, int NumLlista, DateTime CreatedAt);
public record StudentWithPinDto(int Id, int ClassId, string Nom, string Cognoms, string NomComplet, int NumLlista, string Pin, string? CorreuElectronic, DateTime CreatedAt);
public record CreateStudentRequest(string Nom, string Cognoms, int NumLlista, string? Pin, string? CorreuElectronic);
public record UpdateStudentRequest(string Nom, string Cognoms, int NumLlista, string? NewPin, string? CorreuElectronic);
public record BulkCreateStudentsRequest(List<CreateStudentRequest> Students);
public record BulkCreateResult(int Created, int Skipped, List<string> Errors);
public record ResetPinResult(string NewPin);
public record SendPinResult(bool Sent, string? Reason);
public record SendAllResult(int Sent, int Skipped, List<string> Details);
public record SendCredentialsResult(bool Sent, string? Reason);

public record ActivityDto(
    int Id, int ClassId, string ClassName, string? ClassAcademicYear, string ProfessorName,
    string Name, string? Description, bool IsOpen,
    DateTime CreatedAt, int NumGroups, int NumStudents);
public record CreateActivityRequest(int ClassId, string Name, string? Description);
public record UpdateActivityRequest(string Name, string? Description);
public record DuplicateActivityRequest(string Name, string? Description);
public record ImportGroupsRequest(string CsvContent);
public record ImportGroupsResult(int Assigned, int Skipped, List<string> Errors);

public record GroupDto(int Id, int ActivityId, string Name, List<StudentDto> Members);
public record CreateGroupRequest(string Name);
public record AddMemberRequest(int StudentId);

public record EvaluationFormDto(ActivityDto Activity, GroupDto Group, List<EvaluationEntryDto> Entries);
public record EvaluationEntryDto(int StudentId, string StudentName, bool IsSelf,
    Dictionary<string, int> Scores, string? Comment);
public record SaveEvaluationsRequest(List<EvaluationEntryRequest> Evaluations);
public record EvaluationEntryRequest(int EvaluatedId, Dictionary<string, int> Scores, string? Comment);

public record StudentDashboardDto(List<StudentActivityDto> Activities);
public record StudentActivityDto(int Id, string Name, string? Description, bool IsOpen,
    string GroupName, int GroupId, int TotalToEvaluate, int AlreadyEvaluated);

public record ActivityResultsDto(ActivityDto Activity, List<StudentResultDto> Students, List<CriteriaDto> Criteria);
public record StudentResultDto(int StudentId, string Nom, string Cognoms, string StudentName,
    string? CorreuElectronic, int NumLlista, string GroupName,
    Dictionary<string, int?> SelfScores, string? SelfComment,
    List<PeerEvaluationDto> PeerEvaluations, Dictionary<string, double?> AvgCoScores,
    double? AvgGlobal, double? AutAvgGlobal, int NumPeerEvaluators);
public record PeerEvaluationDto(int EvaluatorId, string EvaluatorName,
    Dictionary<string, int> Scores, string? Comment);
public record CriteriaDto(string Key, string Label);

public record GroupChartDto(string GroupName, double? AvgAutoEval, double? AvgCoEval,
    int NumStudentsTotal, int NumStudentsWithAuto, int NumStudentsWithCo);
public record ActivityChartDto(int ActivityId, string ActivityName, string ClassName,
    string? ClassAcademicYear, List<GroupChartDto> Groups, List<CriteriaDto> Criteria,
    List<CriteriaGroupChartDto> CriteriaDetail);
public record CriteriaGroupChartDto(string CriteriaKey, string CriteriaLabel,
    List<CriteriaGroupValueDto> Groups);
public record CriteriaGroupValueDto(string GroupName, double? AvgAuto, double? AvgCo);

public record ProfessorDto(int Id, string Username, string Nom, string Cognoms, string NomComplet,
    string? CorreuElectronic, bool IsAdmin, DateTime CreatedAt, int NumClasses);
public record CreateProfessorRequest(string Username, string Password, string Nom, string Cognoms,
    string? CorreuElectronic, bool IsAdmin);
public record UpdateProfessorRequest(string Nom, string Cognoms, string? CorreuElectronic, bool IsAdmin, string? NewPassword);

// ─── Client HTTP ──────────────────────────────────────────────────────────────

public class ApiClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ApiClient(HttpClient http) => _http = http;

    public void SetToken(string token) =>
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

    // ── Auth ──────────────────────────────────────────────────────────────────

    public Task<LoginResponse?> LoginProfessorAsync(string username, string password) =>
        PostAsync<LoginResponse>("/api/auth/professor", new ProfessorLoginRequest(username, password));

    public Task<LoginResponse?> LoginStudentAsync(int classId, int numLlista, string pin) =>
        PostAsync<LoginResponse>("/api/auth/student", new StudentLoginRequest(classId, numLlista, pin));

    // ── Classes ───────────────────────────────────────────────────────────────

    public Task<List<ClassDto>?> GetClassesAsync() =>
        GetAsync<List<ClassDto>>("/api/classes");

    public Task<ClassDto?> GetClassAsync(int id) =>
        GetAsync<ClassDto>($"/api/classes/{id}");

    public Task<ClassDto?> CreateClassAsync(CreateClassRequest req) =>
        PostAsync<ClassDto>("/api/classes", req);

    public Task<ClassDto?> UpdateClassAsync(int id, UpdateClassRequest req) =>
        PutAsync<ClassDto>($"/api/classes/{id}", req);

    public Task<bool> DeleteClassAsync(int id) =>
        DeleteAsync($"/api/classes/{id}");

    // ── Alumnes ───────────────────────────────────────────────────────────────

    public Task<List<StudentWithPinDto>?> GetStudentsAsync(int classId) =>
        GetAsync<List<StudentWithPinDto>>($"/api/classes/{classId}/students");

    public Task<StudentWithPinDto?> AddStudentAsync(int classId, CreateStudentRequest req) =>
        PostAsync<StudentWithPinDto>($"/api/classes/{classId}/students", req);

    public Task<StudentWithPinDto?> UpdateStudentAsync(int classId, int studentId, UpdateStudentRequest req) =>
        PutAsync<StudentWithPinDto>($"/api/classes/{classId}/students/{studentId}", req);

    public Task<bool> DeleteStudentAsync(int classId, int studentId) =>
        DeleteAsync($"/api/classes/{classId}/students/{studentId}");

    public Task<BulkCreateResult?> BulkAddStudentsAsync(int classId, BulkCreateStudentsRequest req) =>
        PostAsync<BulkCreateResult>($"/api/classes/{classId}/students/bulk", req);

    public Task<ResetPinResult?> ResetPinAsync(int classId, int studentId) =>
        PostAsync<ResetPinResult>($"/api/classes/{classId}/students/{studentId}/reset-pin", null);

    public Task<SendPinResult?> SendPinAsync(int classId, int studentId) =>
        PostAsync<SendPinResult>($"/api/classes/{classId}/students/{studentId}/send-pin", null);

    public Task<SendAllResult?> SendAllPinsAsync(int classId) =>
        PostAsync<SendAllResult>($"/api/classes/{classId}/students/send-all-pins", null);

    public Task<SendCredentialsResult?> SendProfessorCredentialsAsync(int professorId) =>
        PostAsync<SendCredentialsResult>($"/api/professors/{professorId}/send-credentials", null);

    public Task<SendAllResult?> SendAllProfessorCredentialsAsync() =>
        PostAsync<SendAllResult>("/api/professors/send-all-credentials", null);

    public async Task<(byte[] Content, string FileName)?> ExportStudentsAsync(int? classId)
    {
        var url = classId.HasValue
            ? $"/api/classes/{classId}/students/export"
            : "/api/students/export";
        var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;
        var bytes    = await resp.Content.ReadAsByteArrayAsync();
        var fileName = resp.Content.Headers.ContentDisposition?.FileNameStar
            ?? resp.Content.Headers.ContentDisposition?.FileName
            ?? "alumnes.csv";
        return (bytes, fileName.Trim('"'));
    }

    // ── Activitats ────────────────────────────────────────────────────────────

    public Task<List<ActivityDto>?> GetActivitiesAsync() =>
        GetAsync<List<ActivityDto>>("/api/activities");

    public Task<ActivityDto?> GetActivityAsync(int id) =>
        GetAsync<ActivityDto>($"/api/activities/{id}");

    public Task<ActivityDto?> CreateActivityAsync(CreateActivityRequest req) =>
        PostAsync<ActivityDto>("/api/activities", req);

    public Task<ActivityDto?> UpdateActivityAsync(int id, UpdateActivityRequest req) =>
        PutAsync<ActivityDto>($"/api/activities/{id}", req);

    public Task<bool> DeleteActivityAsync(int id) =>
        DeleteAsync($"/api/activities/{id}");

    public Task<ActivityDto?> ToggleActivityAsync(int id) =>
        PostAsync<ActivityDto>($"/api/activities/{id}/toggle", (object?)null);

    public Task<ActivityDto?> DuplicateActivityAsync(int id, DuplicateActivityRequest req) =>
        PostAsync<ActivityDto>($"/api/activities/{id}/duplicate", req);

    public async Task<(byte[] Content, string FileName)?> ExportGroupsAsync(int activityId)
    {
        var resp = await _http.GetAsync($"/api/activities/{activityId}/groups/export");
        if (!resp.IsSuccessStatusCode) return null;
        var bytes    = await resp.Content.ReadAsByteArrayAsync();
        var fileName = resp.Content.Headers.ContentDisposition?.FileNameStar
            ?? resp.Content.Headers.ContentDisposition?.FileName
            ?? $"grups_{activityId}.csv";
        return (bytes, fileName.Trim('"'));
    }

    public Task<ImportGroupsResult?> ImportGroupsAsync(int activityId, string csvContent) =>
        PostAsync<ImportGroupsResult>($"/api/activities/{activityId}/groups/import",
            new ImportGroupsRequest(csvContent));

    // ── Grups ─────────────────────────────────────────────────────────────────

    public Task<List<GroupDto>?> GetGroupsAsync(int activityId) =>
        GetAsync<List<GroupDto>>($"/api/activities/{activityId}/groups");

    public Task<GroupDto?> CreateGroupAsync(int activityId, string name) =>
        PostAsync<GroupDto>($"/api/activities/{activityId}/groups", new CreateGroupRequest(name));

    public Task<bool> DeleteGroupAsync(int activityId, int groupId) =>
        DeleteAsync($"/api/activities/{activityId}/groups/{groupId}");

    public Task AddMemberAsync(int activityId, int groupId, int studentId) =>
        PostVoidAsync($"/api/activities/{activityId}/groups/{groupId}/members",
            new AddMemberRequest(studentId));

    public Task RemoveMemberAsync(int activityId, int groupId, int studentId) =>
        DeleteAsync($"/api/activities/{activityId}/groups/{groupId}/members/{studentId}");

    // ── Avaluacions ───────────────────────────────────────────────────────────

    public Task<EvaluationFormDto?> GetEvaluationFormAsync(int activityId) =>
        GetAsync<EvaluationFormDto>($"/api/evaluations/{activityId}");

    public Task<bool> SaveEvaluationsAsync(int activityId, SaveEvaluationsRequest req) =>
        PostNoContentAsync($"/api/evaluations/{activityId}", req);

    public Task<StudentDashboardDto?> GetStudentDashboardAsync() =>
        GetAsync<StudentDashboardDto>("/api/student/activities");

    // ── Resultats ─────────────────────────────────────────────────────────────

    public Task<ActivityResultsDto?> GetResultsAsync(int activityId) =>
        GetAsync<ActivityResultsDto>($"/api/results/{activityId}");

    public Task<ActivityChartDto?> GetChartAsync(int activityId) =>
        GetAsync<ActivityChartDto>($"/api/results/{activityId}/chart");

    public async Task<(byte[] Content, string FileName)?> ExportCsvAsync(int activityId)
    {
        var resp = await _http.GetAsync($"/api/results/{activityId}/csv");
        if (!resp.IsSuccessStatusCode) return null;
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        var fileName = resp.Content.Headers.ContentDisposition?.FileNameStar
            ?? resp.Content.Headers.ContentDisposition?.FileName
            ?? $"avaluacio_{activityId}.csv";
        return (bytes, fileName.Trim('"'));
    }

    // ── Professors (admin) ────────────────────────────────────────────────────

    public Task<List<ProfessorDto>?> GetProfessorsAsync() =>
        GetAsync<List<ProfessorDto>>("/api/professors");

    public Task<ProfessorDto?> CreateProfessorAsync(CreateProfessorRequest req) =>
        PostAsync<ProfessorDto>("/api/professors", req);

    public Task<ProfessorDto?> UpdateProfessorAsync(int id, UpdateProfessorRequest req) =>
        PutAsync<ProfessorDto>($"/api/professors/{id}", req);

    public Task<bool> DeleteProfessorAsync(int id) =>
        DeleteAsync($"/api/professors/{id}");

    public Task<List<CriteriaDto>?> GetCriteriaAsync() =>
        GetAsync<List<CriteriaDto>>("/api/criteria");

    public Task<List<ClassDto>?> GetPublicClassesAsync() =>
        GetAsync<List<ClassDto>>("/api/public/classes");

    // ── Privats ───────────────────────────────────────────────────────────────

    private async Task<T?> GetAsync<T>(string url)
    {
        var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return default;
        return await resp.Content.ReadFromJsonAsync<T>(_json);
    }

    private async Task<T?> PostAsync<T>(string url, object? body)
    {
        var resp = await _http.PostAsync(url, Json(body));
        if (!resp.IsSuccessStatusCode) return default;
        return await resp.Content.ReadFromJsonAsync<T>(_json);
    }

    private async Task PostVoidAsync(string url, object? body)
    {
        await _http.PostAsync(url, Json(body));
    }

    private async Task<bool> PostNoContentAsync(string url, object? body)
    {
        var resp = await _http.PostAsync(url, Json(body));
        return resp.IsSuccessStatusCode;
    }

    private async Task<T?> PutAsync<T>(string url, object? body)
    {
        var resp = await _http.PutAsync(url, Json(body));
        if (!resp.IsSuccessStatusCode) return default;
        return await resp.Content.ReadFromJsonAsync<T>(_json);
    }

    private async Task<bool> DeleteAsync(string url)
    {
        var resp = await _http.DeleteAsync(url);
        return resp.IsSuccessStatusCode;
    }

    private static StringContent Json(object? body) =>
        new(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json");
}
