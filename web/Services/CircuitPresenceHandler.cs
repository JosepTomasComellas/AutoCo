using Microsoft.AspNetCore.Components.Server.Circuits;

namespace AutoCo.Web.Services;

/// <summary>
/// Atura el heartbeat de presència quan el navegador es desconnecta del circuit
/// (Ctrl+F5, tancament de pestanya, pèrdua de xarxa). Evita entrades duplicades
/// a la llista de connexions actives durant el període de retenció del circuit.
/// </summary>
public sealed class CircuitPresenceHandler(OnlinePresenceService presence) : CircuitHandler
{
    public override async Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        await presence.StopAsync();
    }
}
