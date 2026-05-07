using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoCo.Shared.DTOs;
using Microsoft.AspNetCore.Components.Forms;

namespace AutoCo.Web.Services;

public class ApiClient
{
    private readonly HttpClient        _http;
    private readonly UserStateService  _userState;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ApiClient(HttpClient http, UserStateService userState)
    {
        _http      = http;
        _userState = userState;
    }

    public void SetToken(string token) =>
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

    // ── Auth ──────────────────────────────────────────────────────────────────

    public Task<LoginResponse?> LoginProfessorAsync(string email, string password) =>
        PostLoginAsync<LoginResponse>("/api/auth/professor", new ProfessorLoginRequest(email, password));

    public Task<LoginResponse?> LoginStudentAsync(string email, string password) =>
        PostLoginAsync<LoginResponse>("/api/auth/student", new StudentLoginRequest(email, password));

    // ── Classes ───────────────────────────────────────────────────────────────

    // ── Auth (refresh / logout) ───────────────────────────────────────────────

    public async Task<LoginResponse?> RefreshFromTokenAsync(string refreshToken)
    {
        var resp = await _http.PostAsync("/api/auth/refresh",
            Json(new RefreshRequest(refreshToken)));
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<LoginResponse>(_json);
    }

    public async Task LogoutAsync()
    {
        if (_userState.RefreshToken is not null)
            await _http.PostAsync("/api/auth/logout", Json(new LogoutRequest(_userState.RefreshToken)));
    }

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

    public async Task<List<StudentDto>?> GetStudentsAsync(int classId, int page = 1, int size = 500)
    {
        var r = await GetAsync<PagedResult<StudentDto>>(
            $"/api/classes/{classId}/students?page={page}&size={size}");
        return r?.Items;
    }

    public Task<StudentDto?> AddStudentAsync(int classId, CreateStudentRequest req) =>
        PostAsync<StudentDto>($"/api/classes/{classId}/students", req);

    public Task<StudentDto?> UpdateStudentAsync(int classId, int studentId, UpdateStudentRequest req) =>
        PutAsync<StudentDto>($"/api/classes/{classId}/students/{studentId}", req);

    public Task<bool> DeleteStudentAsync(int classId, int studentId) =>
        DeleteAsync($"/api/classes/{classId}/students/{studentId}");

    public Task<BulkCreateResult?> BulkAddStudentsAsync(int classId, BulkCreateStudentsRequest req) =>
        PostAsync<BulkCreateResult>($"/api/classes/{classId}/students/bulk", req);

    public Task<StudentDto?> MoveStudentAsync(int classId, int studentId, int targetClassId) =>
        PostAsync<StudentDto>($"/api/classes/{classId}/students/{studentId}/move",
            new MoveStudentRequest(targetClassId));

    public Task<ResetPasswordResult?> ResetPasswordAsync(int classId, int studentId) =>
        PostAsync<ResetPasswordResult>($"/api/classes/{classId}/students/{studentId}/reset-password", null);

    public Task<SendPasswordResult?> SendPasswordAsync(int classId, int studentId) =>
        PostAsync<SendPasswordResult>($"/api/classes/{classId}/students/{studentId}/send-password", null);

    public Task<SendAllResult?> SendAllPasswordsAsync(int classId) =>
        PostAsync<SendAllResult>($"/api/classes/{classId}/students/send-all-passwords", null);

    public Task<SendCredentialsResult?> SendProfessorCredentialsAsync(int professorId) =>
        PostAsync<SendCredentialsResult>($"/api/professors/{professorId}/send-credentials", null);

    public Task<SendAllResult?> SendAllProfessorCredentialsAsync() =>
        PostAsync<SendAllResult>("/api/professors/send-all-credentials", null);

    // ── Mòduls ────────────────────────────────────────────────────────────────

    public Task<List<ModuleDto>?> GetModulesAsync(int classId) =>
        GetAsync<List<ModuleDto>>($"/api/classes/{classId}/modules");

    public Task<ModuleDto?> CreateModuleAsync(int classId, CreateModuleRequest req) =>
        PostAsync<ModuleDto>($"/api/classes/{classId}/modules", req);

    public Task<ModuleDto?> UpdateModuleAsync(int classId, int id, UpdateModuleRequest req) =>
        PutAsync<ModuleDto>($"/api/classes/{classId}/modules/{id}", req);

    public Task<bool> DeleteModuleAsync(int classId, int id) =>
        DeleteAsync($"/api/classes/{classId}/modules/{id}");

    public Task<List<ModuleExclusionDto>?> GetExclusionsAsync(int moduleId) =>
        GetAsync<List<ModuleExclusionDto>>($"/api/modules/{moduleId}/exclusions");

    public Task<bool> AddExclusionAsync(int moduleId, int studentId) =>
        PostNoContentAsync($"/api/modules/{moduleId}/exclusions/{studentId}", null);

    public Task<bool> RemoveExclusionAsync(int moduleId, int studentId) =>
        DeleteAsync($"/api/modules/{moduleId}/exclusions/{studentId}");

    // ── Activitats ────────────────────────────────────────────────────────────

    public async Task<List<ActivityDto>?> GetActivitiesAsync(int page = 1, int size = 500)
    {
        var r = await GetAsync<PagedResult<ActivityDto>>(
            $"/api/activities?page={page}&size={size}");
        return r?.Items;
    }

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

    public Task<ActivityDto?> DuplicateActivityCrossAsync(int id, DuplicateCrossRequest req) =>
        PostAsync<ActivityDto>($"/api/activities/{id}/duplicate-cross", req);

    public Task<ParticipationDto?> GetParticipationAsync(int activityId) =>
        GetAsync<ParticipationDto>($"/api/activities/{activityId}/participation");

    public Task<ReminderResult?> SendRemindersAsync(int activityId) =>
        PostAsync<ReminderResult>($"/api/activities/{activityId}/remind", (object?)null);

    public Task<List<InviteTargetDto>?> GetRemindTargetsAsync(int activityId) =>
        GetAsync<List<InviteTargetDto>>($"/api/activities/{activityId}/remind-targets");

    public async Task<bool> RemindOneAsync(int activityId, int studentId)
    {
        var r = await PostAsync<InviteOneResult>($"/api/activities/{activityId}/remind-one/{studentId}", (object?)null);
        return r?.Sent == true;
    }

    public Task<List<InviteTargetDto>?> GetInviteTargetsAsync(int activityId) =>
        GetAsync<List<InviteTargetDto>>($"/api/activities/{activityId}/invite-targets");

    public async Task<bool> InviteOneAsync(int activityId, int studentId, bool includePassword)
    {
        var r = await PostAsync<InviteOneResult>(
            $"/api/activities/{activityId}/invite/{studentId}",
            new InviteOneRequest(includePassword));
        return r?.Sent == true;
    }

    public Task<List<ActivityCriterionDto>?> GetActivityCriteriaAsync(int activityId) =>
        GetAsync<List<ActivityCriterionDto>>($"/api/activities/{activityId}/criteria");

    public Task<List<ActivityCriterionDto>?> SaveActivityCriteriaAsync(int activityId, SaveCriteriaRequest req) =>
        PutAsync<List<ActivityCriterionDto>>($"/api/activities/{activityId}/criteria", req);

    public async Task<(byte[] Content, string FileName)?> ExportGroupsAsync(int activityId)
    {
        var resp = await _http.GetAsync($"/api/activities/{activityId}/groups/export");
        if (!resp.IsSuccessStatusCode) { if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized) _userState.SessionExpired(); return null; }
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

    public Task<bool> RenameGroupAsync(int activityId, int groupId, string name) =>
        PutNoContentAsync($"/api/activities/{activityId}/groups/{groupId}", new RenameGroupRequest(name));

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

    public Task<StudentOwnResultDto?> GetStudentOwnResultAsync(int activityId) =>
        GetAsync<StudentOwnResultDto>($"/api/student/results/{activityId}");

    public Task<bool> ChangeStudentPasswordAsync(string currentPassword, string newPassword) =>
        PutNoContentAsync("/api/student/password",
            new ChangeStudentPasswordRequest(currentPassword, newPassword));

    // ── Resultats ─────────────────────────────────────────────────────────────

    public Task<ActivityResultsDto?> GetResultsAsync(int activityId) =>
        GetAsync<ActivityResultsDto>($"/api/results/{activityId}");

    public Task<ModuleEvolutionDto?> GetModuleEvolutionAsync(int moduleId, int studentId) =>
        GetAsync<ModuleEvolutionDto>($"/api/results/module/{moduleId}/evolution?studentId={studentId}");

    public Task<ActivityChartDto?> GetChartAsync(int activityId) =>
        GetAsync<ActivityChartDto>($"/api/results/{activityId}/chart");

    public async Task<(byte[] Content, string FileName)?> ExportCsvAsync(int activityId)
    {
        var resp = await _http.GetAsync($"/api/results/{activityId}/csv");
        if (!resp.IsSuccessStatusCode) { if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized) _userState.SessionExpired(); return null; }
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

    public async Task<int> GetEvalsCountAsync(int activityId)
    {
        var resp = await GetAsync<System.Text.Json.JsonElement?>($"/api/activities/{activityId}/evals-count");
        return resp?.TryGetProperty("count", out var c) == true ? c.GetInt32() : 0;
    }

    // ── Notes professor ───────────────────────────────────────────────────────

    public Task<List<ProfessorNoteDto>?> GetNotesForActivityAsync(int activityId) =>
        GetAsync<List<ProfessorNoteDto>>($"/api/notes/{activityId}");

    public Task<ProfessorNoteDto?> SaveNoteAsync(int activityId, int studentId, string note) =>
        PutAsync<ProfessorNoteDto>($"/api/notes/{activityId}/{studentId}", new SaveNoteRequest(note));

    // ── Plantilles ────────────────────────────────────────────────────────────

    public Task<List<ActivityTemplateDto>?> GetTemplatesAsync() =>
        GetAsync<List<ActivityTemplateDto>>("/api/templates");

    public Task<ActivityTemplateDto?> CreateTemplateAsync(CreateTemplateRequest req) =>
        PostAsync<ActivityTemplateDto>("/api/templates", req);

    public Task<bool> DeleteTemplateAsync(int id) =>
        DeleteAsync($"/api/templates/{id}");

    // ── Registre d'activitat ──────────────────────────────────────────────────

    public Task<List<ActivityLogDto>?> GetActivityLogAsync(int activityId) =>
        GetAsync<List<ActivityLogDto>>($"/api/activities/{activityId}/log");

    public Task<bool> ReorderGroupsAsync(int activityId, List<int> orderedGroupIds) =>
        PutNoContentAsync($"/api/activities/{activityId}/groups/reorder",
            new ReorderGroupsRequest(orderedGroupIds));

    // ── Perfil professor ──────────────────────────────────────────────────────

    public Task<ProfessorDto?> GetOwnProfileAsync() =>
        GetAsync<ProfessorDto>("/api/professors/me");

    public Task<ProfessorDto?> UpdateOwnProfileAsync(UpdateOwnProfileRequest req) =>
        PutAsync<ProfessorDto>("/api/professors/me", req);

    // ── Reset de contrasenya ──────────────────────────────────────────────────

    public Task<bool> RequestPasswordResetAsync(string email) =>
        PostNoContentAsync("/api/auth/request-reset", new PasswordResetRequestDto(email));

    public async Task<(bool Success, string? Error)> ConfirmPasswordResetAsync(
        string email, string code, string newPassword)
    {
        var resp = await _http.PostAsync("/api/auth/confirm-reset",
            Json(new PasswordResetConfirmDto(email, code, newPassword)));
        if (resp.IsSuccessStatusCode) return (true, null);
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(
                await resp.Content.ReadAsStringAsync());
            var err = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;
            return (false, err ?? "Error desconegut.");
        }
        catch { return (false, "Error desconegut."); }
    }

    // ── Excel ─────────────────────────────────────────────────────────────────

    public async Task<(byte[] Content, string FileName)?> ExportExcelAsync(int activityId)
    {
        var resp = await _http.GetAsync($"/api/results/{activityId}/excel");
        if (!resp.IsSuccessStatusCode) { if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized) _userState.SessionExpired(); return null; }
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        var fileName = resp.Content.Headers.ContentDisposition?.FileNameStar
            ?? resp.Content.Headers.ContentDisposition?.FileName
            ?? $"avaluacio_{activityId}.xlsx";
        return (bytes, fileName.Trim('"'));
    }

    // ── Nou curs (admin) ─────────────────────────────────────────────────────

    public Task<NewYearResult?> NewYearAsync(string targetYear) =>
        PostAsync<NewYearResult>("/api/admin/new-year", new NewYearRequest(targetYear));

    // ── Nivell de log (admin) ─────────────────────────────────────────────────

    public Task<LogLevelDto?> GetLogLevelAsync() =>
        GetAsync<LogLevelDto>("/api/admin/log-level");

    public Task<LogLevelDto?> SetLogLevelAsync(string level) =>
        PutAsync<LogLevelDto>("/api/admin/log-level", new SetLogLevelRequest(level));

    // ── Estadístiques (admin) ─────────────────────────────────────────────────

    public Task<AdminStatsDto?> GetAdminStatsAsync() =>
        GetAsync<AdminStatsDto>("/api/admin/stats");

    public Task<bool> DeleteAdminLoginsAsync() =>
        DeleteAsync("/api/admin/stats/logins");

    // ── Auditoria (admin) ─────────────────────────────────────────────────────

    public async Task<(List<AdminAuditLogDto> Items, int Total)?> GetAuditLogsAsync(int page = 1, int size = 50)
    {
        var r = await GetAsync<PagedResult<AdminAuditLogDto>>(
            $"/api/admin/audit?page={page}&size={size}");
        return r is null ? null : (r.Items, r.Total);
    }

    // ── Backup / Restore (admin) ──────────────────────────────────────────────

    public async Task<(byte[] Content, string FileName)?> ExportBackupAsync()
    {
        var resp = await _http.GetAsync("/api/admin/backup/export");
        if (!resp.IsSuccessStatusCode) { if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized) _userState.SessionExpired(); return null; }
        var bytes    = await resp.Content.ReadAsByteArrayAsync();
        var fileName = resp.Content.Headers.ContentDisposition?.FileNameStar
            ?? resp.Content.Headers.ContentDisposition?.FileName
            ?? $"autoco_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
        return (bytes, fileName.Trim('"'));
    }

    public Task<ImportResult?> ImportBackupAsync(BackupDto backup) =>
        PostAsync<ImportResult>("/api/admin/backup/import", backup);

    public Task<List<BackupFileInfoDto>?> ListBackupFilesAsync() =>
        GetAsync<List<BackupFileInfoDto>>("/api/admin/backup/files");

    public Task<BackupFileInfoDto?> CreateBackupFileAsync() =>
        PostAsync<BackupFileInfoDto>("/api/admin/backup/files", null);

    public async Task<(byte[] Content, string FileName)?> DownloadBackupFileAsync(string name)
    {
        var resp = await _http.GetAsync($"/api/admin/backup/files/{Uri.EscapeDataString(name)}");
        if (!resp.IsSuccessStatusCode) { if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized) _userState.SessionExpired(); return null; }
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        return (bytes, name);
    }

    public Task<bool> DeleteBackupFileAsync(string name) =>
        DeleteAsync($"/api/admin/backup/files/{Uri.EscapeDataString(name)}");

    public Task<ImportResult?> RestoreBackupFileAsync(string name) =>
        PostAsync<ImportResult>($"/api/admin/backup/files/{Uri.EscapeDataString(name)}/restore", null);

    // ── Fotos ─────────────────────────────────────────────────────────────────

    public async Task<string?> UploadStudentFotoAsync(int classId, int studentId, IBrowserFile file)
    {
        using var content = new MultipartFormDataContent();
        var stream = file.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024);
        content.Add(new StreamContent(stream), "file", file.Name);
        var resp = await _http.PostAsync(
            $"/api/classes/{classId}/students/{studentId}/foto", content);
        if (!resp.IsSuccessStatusCode) { if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized) _userState.SessionExpired(); return null; }
        using var doc = System.Text.Json.JsonDocument.Parse(
            await resp.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("fotoUrl", out var u) ? u.GetString() : null;
    }

    public async Task<bool> DeleteStudentFotoAsync(int classId, int studentId)
    {
        var resp = await _http.DeleteAsync(
            $"/api/classes/{classId}/students/{studentId}/foto");
        if (!resp.IsSuccessStatusCode) { if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized) _userState.SessionExpired(); return false; }
        return true;
    }

    public async Task<ImportFotosResult?> ImportStudentFotosZipAsync(int classId, IBrowserFile file)
    {
        using var content = new MultipartFormDataContent();
        var stream = file.OpenReadStream(maxAllowedSize: 100 * 1024 * 1024);
        content.Add(new StreamContent(stream), "file", file.Name);
        var resp = await _http.PostAsync(
            $"/api/classes/{classId}/students/fotos/zip", content);
        if (!resp.IsSuccessStatusCode) { if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized) _userState.SessionExpired(); return null; }
        return await resp.Content.ReadFromJsonAsync<ImportFotosResult>(_json);
    }

    public async Task<string?> UploadProfessorFotoAsync(int professorId, IBrowserFile file)
    {
        using var content = new MultipartFormDataContent();
        var stream = file.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024);
        content.Add(new StreamContent(stream), "file", file.Name);
        var resp = await _http.PostAsync($"/api/professors/{professorId}/foto", content);
        if (!resp.IsSuccessStatusCode) { if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized) _userState.SessionExpired(); return null; }
        using var doc = System.Text.Json.JsonDocument.Parse(
            await resp.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("fotoUrl", out var u) ? u.GetString() : null;
    }

    public async Task<bool> DeleteProfessorFotoAsync(int professorId)
    {
        var resp = await _http.DeleteAsync($"/api/professors/{professorId}/foto");
        if (!resp.IsSuccessStatusCode) { if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized) _userState.SessionExpired(); return false; }
        return true;
    }

    // ── Privats ───────────────────────────────────────────────────────────────

    // Intenta renovar el JWT usant el refresh token; retorna true si ha tingut èxit.
    private async Task<bool> TryRefreshAsync()
    {
        var rt = _userState.RefreshToken;
        if (rt is null) return false;
        var refreshed = await RefreshFromTokenAsync(rt);
        if (refreshed is null) return false;
        _userState.SetLogin(refreshed);
        SetToken(refreshed.Token);
        return true;
    }

    private async Task HandleUnauthorized()
    {
        if (!await TryRefreshAsync())
            _userState.SessionExpired();
    }

    // Login no ha de disparar SessionExpired en cas de 401 (credencials incorrectes, no sessió caducada)
    private async Task<T?> PostLoginAsync<T>(string url, object? body)
    {
        var resp = await _http.PostAsync(url, Json(body));
        if (!resp.IsSuccessStatusCode) return default;
        return await resp.Content.ReadFromJsonAsync<T>(_json);
    }

    private async Task<T?> GetAsync<T>(string url)
    {
        var resp = await _http.GetAsync(url);
        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            if (!await TryRefreshAsync()) { _userState.SessionExpired(); return default; }
            resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return default;
        }
        else if (!resp.IsSuccessStatusCode) return default;
        return await resp.Content.ReadFromJsonAsync<T>(_json);
    }

    private async Task<T?> PostAsync<T>(string url, object? body)
    {
        var json = Json(body);
        var resp = await _http.PostAsync(url, json);
        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            if (!await TryRefreshAsync()) { _userState.SessionExpired(); return default; }
            resp = await _http.PostAsync(url, Json(body));
            if (!resp.IsSuccessStatusCode) return default;
        }
        else if (!resp.IsSuccessStatusCode) return default;
        return await resp.Content.ReadFromJsonAsync<T>(_json);
    }

    private async Task PostVoidAsync(string url, object? body)
    {
        var resp = await _http.PostAsync(url, Json(body));
        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            if (!await TryRefreshAsync()) _userState.SessionExpired();
            else await _http.PostAsync(url, Json(body));
        }
    }

    private async Task<bool> PostNoContentAsync(string url, object? body)
    {
        var resp = await _http.PostAsync(url, Json(body));
        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            if (!await TryRefreshAsync()) { _userState.SessionExpired(); return false; }
            resp = await _http.PostAsync(url, Json(body));
        }
        return resp.IsSuccessStatusCode;
    }

    private async Task<T?> PutAsync<T>(string url, object? body)
    {
        var resp = await _http.PutAsync(url, Json(body));
        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            if (!await TryRefreshAsync()) { _userState.SessionExpired(); return default; }
            resp = await _http.PutAsync(url, Json(body));
            if (!resp.IsSuccessStatusCode) return default;
        }
        else if (!resp.IsSuccessStatusCode) return default;
        return await resp.Content.ReadFromJsonAsync<T>(_json);
    }

    private async Task<bool> PutNoContentAsync(string url, object? body)
    {
        var resp = await _http.PutAsync(url, Json(body));
        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            if (!await TryRefreshAsync()) { _userState.SessionExpired(); return false; }
            resp = await _http.PutAsync(url, Json(body));
        }
        return resp.IsSuccessStatusCode;
    }

    private async Task<bool> DeleteAsync(string url)
    {
        var resp = await _http.DeleteAsync(url);
        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            if (!await TryRefreshAsync()) { _userState.SessionExpired(); return false; }
            resp = await _http.DeleteAsync(url);
        }
        return resp.IsSuccessStatusCode;
    }

    private static StringContent Json(object? body) =>
        new(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json");
}
