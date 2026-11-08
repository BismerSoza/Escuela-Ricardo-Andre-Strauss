namespace Escuela_API.Services
{
    public interface IUtilesService
    {
        string GenerarToken(int consecutivo, string nombre, string rol, string? seccion);

        string GenerarContrasennaTemporal();

        int ObtenerConsecutivoToken();

        string ObtenerRolToken();

        string ObtenerNombreToken();

        string? ObtenerSeccionToken();
    }
}
