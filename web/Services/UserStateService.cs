namespace AutoCo.Web.Services;

/// <summary>
/// Servei Scoped que guarda l'estat de sessió de l'usuari per al circuit Blazor.
/// Substitueix ISession + SessionHelper de Razor Pages.
/// </summary>
public class UserStateService
{
    public string? Token      { get; private set; }
    public string? NomComplet { get; private set; }
    public string? Role       { get; private set; }
    public int?    UserId     { get; private set; }

    public bool IsLoggedIn   => Token is not null;
    public bool IsProfessor  => Role is "Professor" or "Admin";
    public bool IsStudent    => Role == "Student";
    public bool IsAdmin      => Role == "Admin";

    public event Action? OnChange;

    public void SetLogin(LoginResponse login)
    {
        Token      = login.Token;
        NomComplet = login.NomComplet;
        Role       = login.Role;
        UserId     = login.UserId;
        OnChange?.Invoke();
    }

    public void Logout()
    {
        Token      = null;
        NomComplet = null;
        Role       = null;
        UserId     = null;
        OnChange?.Invoke();
    }
}
