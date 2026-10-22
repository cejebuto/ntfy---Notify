namespace Notify.Web.Services;

/// <summary>Preset notifications that mimic common DevOps events. Time and numbers vary so each push is distinguishable.</summary>
public sealed class SimulatedEvents
{
    public const string ServerDown = "server-down";
    public const string DbUpdated = "db-updated";
    public const string ProcessCompleted = "process-completed";

    private static readonly string[] AllKeys = [ServerDown, DbUpdated, ProcessCompleted];

    public IReadOnlyCollection<string> Keys => AllKeys;

    public bool TryGet(string key, out (string Title, string Message, int Priority, string[] Tags) preset)
    {
        var time = DateTime.Now.ToString("HH:mm:ss");
        switch (key)
        {
            case ServerDown:
                preset = (
                    "Servidor caído",
                    $"ALERTA CRÍTICA: el servidor web-01 (10.0.4.21) no responde desde las {time}. Health check fallido 3 veces consecutivas.",
                    5,
                    ["rotating_light", "skull"]);
                return true;
            case DbUpdated:
                preset = (
                    "Base de datos actualizada",
                    $"Migración {DateTime.Now:yyyy_MM_dd}_add_index aplicada en notify-db. {Random.Shared.Next(10, 5001)} filas afectadas a las {time}.",
                    3,
                    ["floppy_disk", "gear"]);
                return true;
            case ProcessCompleted:
                preset = (
                    "Proceso completado",
                    $"El job nightly-report terminó correctamente en {Random.Shared.Next(5, 121)}s a las {time}.",
                    4,
                    ["white_check_mark", "tada"]);
                return true;
            default:
                preset = default;
                return false;
        }
    }
}
