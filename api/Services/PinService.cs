namespace AutoCo.Api.Services;

/// <summary>
/// Gestió de PINs d'alumnes: generació, hash BCrypt i verificació.
/// Compatibilitat retroactiva: detecta PINs antics en text pla (no comencen per "$2").
/// </summary>
public static class PinService
{
    /// <summary>Genera un PIN de 4 dígits aleatori (1000-9999).</summary>
    public static string Generate() => Random.Shared.Next(1000, 10000).ToString();

    /// <summary>Retorna el hash BCrypt d'un PIN en text pla.</summary>
    public static string Hash(string plainPin) => BCrypt.Net.BCrypt.HashPassword(plainPin);

    /// <summary>
    /// Verifica si el PIN introduït coincideix amb el valor emmagatzemat.
    /// Suporta tant hashes BCrypt (nous) com text pla (migració retroactiva).
    /// </summary>
    public static bool Verify(string enteredPin, string storedValue)
    {
        if (storedValue.StartsWith("$2"))         // BCrypt hash
            return BCrypt.Net.BCrypt.Verify(enteredPin, storedValue);
        return storedValue == enteredPin;          // text pla (registres antics)
    }
}
