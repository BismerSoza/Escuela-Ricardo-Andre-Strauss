using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Escuela_API.Services;

namespace Escuela_API.Hubs
{
    [Authorize]
    public class ChatHub(IUtilesService _utiles) : Hub
    {
        // Cada usuario se une a un grupo propio ("usuario-{id}") para poder
        // recibir notificaciones de mensajes nuevos dirigidos a él.
        public override async Task OnConnectedAsync()
        {
            var consecutivo = _utiles.ObtenerConsecutivoToken();
            await Groups.AddToGroupAsync(Context.ConnectionId, $"usuario-{consecutivo}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var consecutivo = _utiles.ObtenerConsecutivoToken();
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"usuario-{consecutivo}");
            await base.OnDisconnectedAsync(exception);
        }
    }
}
