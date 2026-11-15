# Foll — NotificationCommunication

Microservicio de notificaciones, tokens push, SMS, enlaces temporales y SignalR. La recepción de eventos es por RabbitMQ, siguiendo los contratos y consumidores de Care. La API de usuarios valida JWT localmente; no solicita tokens de servicio ni consulta a IAM para el nombre de quien cerró una emergencia.

## Entradas y salidas

| Flujo | Implementación |
|---|---|
| API `/api/notifications` y tokens push | JWT local, userId y comprobación de propiedad |
| Hub `/hubs/notifications` | JWT; grupo personal `user:{userId}` |
| Invitaciones de Care | Consume `care.invitation-created.v1`, `care.invitation-accepted.v1`, `care.invitation-rejected.v1` |
| Incidentes de Emergency | Consume apertura/cierre desde su outbox RabbitMQ |
| Alertas de Devices | Consume batería baja/recuperada, desconexión/reconexión |
| Telemetría de Devices | RabbitMQ → buffer efímero → SignalR; descarta datos de más de 30 segundos |
| Destinatarios de las alertas | HTTP a Care `/internal/patients/{id}/notification-recipients` |
| Ubicación de un enlace público | Valida token temporal y consulta Emergency por su ruta interna protegida |
| Entrega final | Push/SMS y SignalR, manteniendo reglas y fallback del monolito |

No hay endpoints HTTP `/internal/notifications/*` de recepción ni una API key general para invocar todo el microservicio. La autenticación de los eventos corresponde a las credenciales/permisos del broker. La llave compartida `InternalApi:BackgroundQueryKey` solo se envía en las consultas HTTP de destinatarios y ubicación, que no tienen sesión de usuario.

## Persistencia y confirmaciones

Colas durables:

- `notificationcommunication.invitation-events.v1`
- `notificationcommunication.emergency-events.v1`
- `notificationcommunication.device-events.v1`

Cada una conserva su propia suscripción. El consumidor valida versión, ID, correlación, routing key y cuerpo antes de aceptar. Para alertas/invitaciones confirma al broker después de persistir una inbox deduplicada; un worker procesa el evento después. La aceptación no equivale a notificación entregada al usuario. La telemetría no se persiste en esa inbox.

La adaptación de invitaciones conserva destinatario y estado del contrato actual de Care. El monolito desactiva por defecto su consumidor de la misma cola mediante `NotificationCommunication:LocalInvitationConsumerEnabled=false`; no deben competir dos hubs por esos mensajes.

`ClosedByName` llega en el evento de Emergency desde el JWT validado de quien hizo la operación. Los eventos antiguos sin ese campo continúan deserializándose con nombre nulo. No se propagan JWT en mensajes ni se consulta IAM desde el worker para ese nombre.

La inbox distingue procesamiento y resultado de entrega. Como en el monolito, fallos capturados de push/SMS se registran como Failed y no crean un sistema adicional de reenvío por destinatario. Se conserva el manejo de errores de cierre SignalR del monolito. El transporte sigue el patrón de reintento/cuarentena de Care; no garantiza entrega exactamente una vez ante efectos externos o varias réplicas.

## Desarrollo

`appsettings.json` contiene configuración local: Care :5245, Emergency :5255, PostgreSQL `foll_notifications`, clave JWT compartida con IAM, llave para consultas de contexto y credenciales RabbitMQ. El puerto del servicio es 5257.

`ApplyMigrationsOnStartup=true` en Development. La base local fue creada con su migración inicial, con cinco tablas del esquema `notification`: notificaciones, SMS, tokens push, enlaces e inbox. Las migraciones están desactivadas por defecto fuera de desarrollo.

Push y SMS permanecen en Fake: permiten desarrollar y registrar resultados sin envío externo. No se cambiaron a Firebase/Twilio ni se enviaron mensajes reales.

```powershell
dotnet tool restore
dotnet ef database update
dotnet run --launch-profile http
```

Levantar RabbitMQ y PostgreSQL para el procesamiento. El frontend utiliza `http://localhost:8000` para la API y `http://localhost:8000/hubs/notifications` para SignalR. Kong dirige esas rutas a este microservicio; :5257 queda como destino interno. `Notifications:PublicBaseUrl` usa el gateway para los enlaces nuevos de ubicación.

## Pruebas

```powershell
dotnet test tests/Foll.NotificationCommunication.Tests.csproj
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Smoke-Test.ps1 -CheckDatabase
```

La suite ya no referencia proxies que no existen en el monolito; verifica los emisores reales de Emergency/Devices contra el receptor y los contratos de Care. El smoke comprueba JWT, las tablas y el handshake WebSocket SignalR sin procesar alertas. Para aceptación/limpieza en PostgreSQL y transporte real RabbitMQ, usar `dotnet run --project tests/Integration.Checks/Integration.Checks.csproj -- --rabbitmq` desde Emergency; los recursos de la prueba son temporales.

## Alcance conservado

La eliminación de cuenta IAM → Notifications permanece excluida según el alcance previo. No se agregaron eventos de negocio salientes artificiales de Notifications: sus salidas actuales son proveedores y SignalR. No se migraron datos históricos ni se modificaron despliegues/redes.
