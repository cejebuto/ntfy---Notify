# Notify · ntfy tester

## Qué es

Panel web local para probar notificaciones push con [ntfy.sh](https://ntfy.sh). Backend ASP.NET Core 9 (Minimal API) + frontend HTML/CSS/JS sin frameworks. Escucha solo en `http://localhost:4444`.

Permite configurar el topic, enviar notificaciones personalizadas, simular eventos (servidor caído, base de datos actualizada, proceso completado) y ver el historial con la respuesta cruda de ntfy.sh.

## Requisitos

- .NET 9 SDK **o** Docker.

## Ejecutar

```bash
# Con .NET
dotnet run --project src/Notify.Web
# → http://localhost:4444

# Con Docker
docker compose up -d --build
# → http://localhost:4444 (solo accesible desde esta máquina)
```

## Suscribirse desde iOS

1. Instalá la app **ntfy** desde el App Store (gratis).
2. Abrila y tocá **+** (Suscribirse a un topic).
3. Pegá el topic exactamente como aparece en el panel.
4. Dejá el servidor por defecto **ntfy.sh** y tocá **Suscribirse**.
5. Permití las notificaciones cuando iOS lo pida.

Si regenerás el topic, tenés que volver a suscribirte en el iPhone. El panel recuerda el topic en el navegador (`localStorage`) y se lo reenvía al backend al arrancar, así que reiniciar la app no te obliga a re-suscribirte.

## Troubleshooting iOS: el mensaje aparece en la app pero el iPhone no notifica

Si el historial del panel muestra **Enviado** con un id de ntfy, el servicio ya aceptó el mensaje. Que el mensaje aparezca en la app solo al abrirla (típicamente con 30 s o más de demora) y sin banner, sonido ni badge es un bug conocido y todavía abierto de la app oficial de iOS: la suscripción a Firebase/APNs se pierde o no se registra, y la app únicamente muestra mensajes cuando consulta por su cuenta.

Pasos que los usuarios reportan que funcionan, en orden:

1. En la app ntfy, borrá la suscripción al topic y volvé a agregarla. Dispará un evento simulado desde el panel.
2. En Ajustes de iOS → ntfy, apagá y volvé a prender **Actualización en segundo plano** y **Notificaciones**. Verificá que Pantalla bloqueada, Centro de notificaciones y Tiras estén activados, y que no esté en "Entregar en silencio" ni en Resumen programado.
3. Desinstalá la app, instalala de nuevo y suscribite otra vez. Es el arreglo más reportado, aunque a algunos usuarios se les vuelve a romper con el tiempo.
4. Revisá que no haya un modo Concentración activo.

Para descartar este proyecto: abrí `https://ntfy.sh/<tu-topic>` en el navegador de la computadora y dispará un evento. Si ahí llega al instante, el problema está entre Firebase y la app de iOS, no en el envío.

Seguimiento del bug: [ntfy#1796](https://github.com/binwiederhier/ntfy/issues/1796), [ntfy#1305](https://github.com/binwiederhier/ntfy/issues/1305) y la página de [known issues](https://docs.ntfy.sh/known-issues/).

## Endpoints

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `/api/health` | Estado del backend |
| GET | `/api/config` | Topic actual y URLs |
| PUT | `/api/config` | Cambia el topic `{ "topic": "..." }` (400 si es inválido) |
| POST | `/api/config/regenerate` | Genera un topic aleatorio nuevo |
| POST | `/api/notifications` | Envía `{ title?, message, priority 1-5, tags? }` |
| POST | `/api/simulate/{event}` | `server-down`, `db-updated` o `process-completed` |
| GET | `/api/history` | `{ items, sent, failed }` (máx. 200, más nuevas primero) |
| DELETE | `/api/history` | Vacía el historial |

`POST /api/notifications` y `/api/simulate/{event}` responden 200 con `success: true|false`; el fallo de ntfy.sh (rate limit, red caída) viaja dentro del resultado.

## Tests

```bash
dotnet test
```

## Notas

- **El topic es tu contraseña**: cualquiera que lo conozca puede leer y enviar notificaciones. Por eso se genera aleatorio (`notify-` + 24 caracteres).
- ntfy.sh público aplica rate limits (respuesta 429); si los alcanzás, el historial lo muestra como "Falló".
- Sin base de datos ni autenticación: el historial vive en memoria y se pierde al reiniciar.
