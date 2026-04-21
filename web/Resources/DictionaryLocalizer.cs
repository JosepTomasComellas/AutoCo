using System.Globalization;
using Microsoft.Extensions.Localization;

namespace AutoCo.Web.Resources;

/// <summary>
/// Localitzador basat en diccionaris estàtics. Bypassa completament ResourceManager
/// i els fitxers .resx embeguts, evitant problemes de resolució en Docker.
/// </summary>
public sealed class DictionaryLocalizer : IStringLocalizer<SharedResources>
{
    // ── Català (neutral / per defecte) ───────────────────────────────────────
    private static readonly Dictionary<string, string> Ca = new()
    {
        // Autenticació
        ["Login_Teacher_Title"]          = "Accés Professor",
        ["Login_Student_Title"]          = "Accés Alumne",
        ["Login_Subtitle"]               = "AutoCo · Avaluació entre iguals",
        ["Login_Email"]                  = "Correu electrònic",
        ["Login_Password"]               = "Contrasenya",
        ["Login_Submit"]                 = "Entrar",
        ["Login_ForgotPassword"]         = "Has oblidat la contrasenya?",
        ["Login_Back"]                   = "← Tornar",
        ["Login_Error_EmptyFields"]      = "Omple tots els camps.",
        ["Login_Error_InvalidCredentials"] = "Credencials incorrectes.",

        // Navegació
        ["Nav_MyProfile"]   = "El meu perfil",
        ["Nav_ColorTheme"]  = "Tema de color",
        ["Nav_Language"]    = "Idioma",
        ["Nav_DarkMode"]    = "Mode fosc",
        ["Nav_LightMode"]   = "Mode clar",
        ["Nav_Logout"]      = "Sortir",
        ["Nav_LogoutTitle"] = "Sortir ({0})",

        // Accions
        ["Action_Save"]    = "Desar",
        ["Action_Cancel"]  = "Cancel·lar",
        ["Action_Delete"]  = "Eliminar",
        ["Action_Add"]     = "Afegir",
        ["Action_Edit"]    = "Editar",
        ["Action_Close"]   = "Tancar",
        ["Action_Open"]    = "Obrir",
        ["Action_Import"]  = "Importar",
        ["Action_Export"]  = "Exportar",
        ["Action_Send"]    = "Enviar",
        ["Action_Confirm"] = "Confirmar",

        // Etiquetes
        ["Label_Student"]     = "Alumne",
        ["Label_Teacher"]     = "Professor",
        ["Label_Admin"]       = "Administrador",
        ["Label_Class"]       = "Classe",
        ["Label_Group"]       = "Grup",
        ["Label_Activity"]    = "Activitat",
        ["Label_Module"]      = "Mòdul",
        ["Label_Results"]     = "Resultats",
        ["Label_Name"]        = "Nom",
        ["Label_Surname"]     = "Cognoms",
        ["Label_Description"] = "Descripció",

        // Missatges
        ["Msg_Loading"] = "Carregant...",
        ["Msg_NoData"]  = "Sense dades",
        ["Msg_Saved"]   = "Desat correctament.",
        ["Msg_Deleted"] = "Eliminat correctament.",
        ["Msg_Error"]   = "S'ha produït un error.",

        // Idiomes
        ["Lang_Catalan"] = "Català",
        ["Lang_Spanish"] = "Español",
    };

    // ── Castellà ─────────────────────────────────────────────────────────────
    private static readonly Dictionary<string, string> Es = new()
    {
        // Autenticació
        ["Login_Teacher_Title"]          = "Acceso Profesor",
        ["Login_Student_Title"]          = "Acceso Alumno",
        ["Login_Subtitle"]               = "AutoCo · Evaluación entre iguales",
        ["Login_Email"]                  = "Correo electrónico",
        ["Login_Password"]               = "Contraseña",
        ["Login_Submit"]                 = "Entrar",
        ["Login_ForgotPassword"]         = "¿Olvidaste tu contraseña?",
        ["Login_Back"]                   = "← Volver",
        ["Login_Error_EmptyFields"]      = "Rellena todos los campos.",
        ["Login_Error_InvalidCredentials"] = "Credenciales incorrectas.",

        // Navegació
        ["Nav_MyProfile"]   = "Mi perfil",
        ["Nav_ColorTheme"]  = "Tema de color",
        ["Nav_Language"]    = "Idioma",
        ["Nav_DarkMode"]    = "Modo oscuro",
        ["Nav_LightMode"]   = "Modo claro",
        ["Nav_Logout"]      = "Salir",
        ["Nav_LogoutTitle"] = "Salir ({0})",

        // Accions
        ["Action_Save"]    = "Guardar",
        ["Action_Cancel"]  = "Cancelar",
        ["Action_Delete"]  = "Eliminar",
        ["Action_Add"]     = "Añadir",
        ["Action_Edit"]    = "Editar",
        ["Action_Close"]   = "Cerrar",
        ["Action_Open"]    = "Abrir",
        ["Action_Import"]  = "Importar",
        ["Action_Export"]  = "Exportar",
        ["Action_Send"]    = "Enviar",
        ["Action_Confirm"] = "Confirmar",

        // Etiquetes
        ["Label_Student"]     = "Alumno",
        ["Label_Teacher"]     = "Profesor",
        ["Label_Admin"]       = "Administrador",
        ["Label_Class"]       = "Clase",
        ["Label_Group"]       = "Grupo",
        ["Label_Activity"]    = "Actividad",
        ["Label_Module"]      = "Módulo",
        ["Label_Results"]     = "Resultados",
        ["Label_Name"]        = "Nombre",
        ["Label_Surname"]     = "Apellidos",
        ["Label_Description"] = "Descripción",

        // Missatges
        ["Msg_Loading"] = "Cargando...",
        ["Msg_NoData"]  = "Sin datos",
        ["Msg_Saved"]   = "Guardado correctamente.",
        ["Msg_Deleted"] = "Eliminado correctamente.",
        ["Msg_Error"]   = "Se ha producido un error.",

        // Idiomes
        ["Lang_Catalan"] = "Català",
        ["Lang_Spanish"] = "Español",
    };

    // ── Implementació IStringLocalizer ────────────────────────────────────────

    private static Dictionary<string, string> GetDict()
    {
        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return lang == "es" ? Es : Ca;
    }

    public LocalizedString this[string name]
    {
        get
        {
            var dict  = GetDict();
            var found = dict.TryGetValue(name, out var value);
            return new LocalizedString(name, value ?? name, resourceNotFound: !found);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var dict  = GetDict();
            var found = dict.TryGetValue(name, out var template);
            var value = found ? string.Format(template!, arguments) : name;
            return new LocalizedString(name, value, resourceNotFound: !found);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        => GetDict().Select(kv => new LocalizedString(kv.Key, kv.Value));
}
