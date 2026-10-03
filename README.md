# sistema-estadistico-znuny-backend

API REST en **.NET 10 / ASP.NET Core** que lee la base PostgreSQL de Znuny (**solo lectura**) y calcula la
**eficacia, eficiencia y efectividad** del soporte y de cada agente. La consume el frontend
`dashboard-estadistico-tanstack`.

## Ejecutar

Requisitos: SDK de .NET 10 con el runtime de ASP.NET Core (en Arch/CachyOS: `aspnet-runtime-10.0` y `aspnet-targeting-pack-10.0`)
y la base del laboratorio levantada (`sistema-estadistico-znuny-lab`, puerto 5433).

```bash
dotnet run --project ZnunyStats.Api      # http://localhost:5080 (Swagger UI en la raíz, solo en desarrollo)
dotnet test                              # pruebas de las fórmulas (xUnit v3 + Microsoft Testing Platform)
```

Frontend: en `dashboard-estadistico-tanstack`, `npm run dev` (puerto 3001; en desarrollo usa un proxy de Vite hacia
`VITE_API_URL`, por defecto `http://localhost:5080`). Fuera de ese proxy, los orígenes permitidos se configuran en
`AllowedOrigins`.

La cadena de conexión de desarrollo está en `appsettings.Development.json`. En otro entorno, usá la variable
`ConnectionStrings__Znuny`. Mantené siempre `Options=-c default_transaction_read_only=on`: PostgreSQL rechaza
cualquier escritura de la sesión, además de que el código solo contiene `SELECT`.

## Cómo está organizado

Misma estructura que `sisprenic_backend`: un proyecto por responsabilidad y, dentro de la API, un módulo por
funcionalidad con una carpeta por caso de uso (`Endpoint.cs`, más `Handler.cs` / `Response.cs` cuando hacen falta).

```
ZnunyStats.slnx
Directory.Build.props / Directory.Packages.props   Framework y versiones de paquetes centralizadas
ZnunyStats.Domain/
  Entities/      TicketRecord, TicketStatus: un ticket ya clasificado.
  Metrics/       Kpis (las fórmulas, funciones puras), RateKpi, DurationKpi, Indicators, Outcome.
  ReadModels/    Respuestas compartidas por la API y los reportes (Overview, Agents, DataQuality, TicketRow…).
ZnunyStats.Reports/
  Abstractions/  IReportRenderer.
  Content/       ReportContent (todo lo que lleva un reporte) y ReportText (formatos compartidos).
  Infrastructure/ PdfReportRenderer (QuestPDF) y ExcelReportRenderer (ClosedXML).
  Extensions/    AddReporting().
ZnunyStats.Api/
  Program.cs     Serilog, servicios, middleware.
  Common/        StatsOptions, ZnunyClock (zona horaria), manejo global de errores, filtro de validación.
  Database/      ZnunyQueries (todo el SQL, solo lectura), filas y health check.
  Extensions/    Registro de servicios, mapeo de endpoints, errores, rate limiting, Swagger.
  Modules/
    Shared/      TicketQuery (+ validador), Period, TicketStore (lee y cachea Znuny), TicketAnalytics (piezas comunes).
    Analytics/GetOverview   Agents/GetAgents   Tickets/GetTickets, GetTicketTimeline
    Locations/GetLocations  Catalogs/GetCatalogs  DataQuality/GetDataQuality
    Reports/ExportReport    Sync/RefreshData
  appsettings.json  Reglas de negocio configurables (sección "Estadisticas").
ZnunyStats.UnitTests/   Pruebas de las fórmulas.
```

Flujo: **SQL → hechos por ticket → clasificación → fórmulas → JSON**. El volumen actual (cientos de tickets) se
procesa en memoria y se cachea 60 s. Si algún día hay cientos de miles de tickets, el paso natural es mover el
filtro por fecha al SQL; las fórmulas no cambian.

Los filtros se validan en un solo lugar (`TicketQueryValidator`, FluentValidation): un error responde 400 con el
mensaje en `detail`. Los errores no controlados pasan por `GlobalExceptionHandler` (ProblemDetails); si la base de
Znuny no responde, 503. Los reportes tienen un límite de 10 por minuto por IP.

## Indicadores

La **población** son los tickets **creados en el período** (fechas en hora de Managua) que cumplen los filtros.

| Indicador | Pregunta | Fórmula |
|---|---|---|
| **Eficacia** | ¿Se logró el resultado? | tickets resueltos con éxito ÷ tickets a cargo |
| **Eficiencia** | ¿Con cuánto tiempo? | mediana (y p90) de creación → primer cierre exitoso; mediana de creación → primera acción de un agente |
| **Efectividad** | ¿Se logró bien y a tiempo? | resueltos dentro del objetivo de su prioridad **y** sin reapertura en 7 días ÷ tickets con resultado conocido |
| Reapertura | ¿Las soluciones se sostienen? | resueltos que volvieron a estar activos en 7 días ÷ resueltos hace al menos 7 días |

Reglas:

- **Agente responsable**: propietario del ticket al primer cierre exitoso (si era una cuenta de sistema, quien cerró).
  Si el ticket sigue abierto, su propietario actual. Una sola regla para todos los indicadores.
- **Resultado conocido** (efectividad): resuelto, cerrado sin éxito, o abierto más allá de su objetivo. Los abiertos
  dentro del plazo son *pendientes* y no cuentan ni a favor ni en contra.
- **Cierres masivos**: tickets cerrados con la acción masiva de Znuny (evento `Bulk` del mismo agente hasta 30 min antes
  del cierre). Cuentan para eficacia, pero se **excluyen** de tiempos y efectividad: su duración no refleja trabajo real.
- **Primera atención**: excluye los tickets que abrió el propio agente (llamada, atención presencial), porque no hubo
  espera que medir.
- **Objetivos por prioridad**: Znuny no tiene SLA configurado, así que se usan referencias internas
  (Muy alta 4 h, Alta 8 h, Normal 24 h, Baja 48 h, Muy baja 72 h), editables en `appsettings.json`.
- Un valor no calculable se devuelve como `null`, nunca como 0. Cada indicador trae numerador, denominador,
  pendientes y excluidos para que se pueda verificar.
- Mediana en lugar de promedio: unos pocos tickets muy largos no distorsionan el valor típico.

## Lo que se encontró en la base (02/10/2026)

- 659 tickets (abril–septiembre 2026); 580 son una carga del 22/09 («Verificación del número de serie… COMTECH»),
  y unos 340 se cerraron en lote. Por eso existe la regla de cierres masivos.
- No hay SLA, servicios ni registro de tiempo trabajado (`sla`, `service` y `time_accounting` vacíos): no se puede
  medir esfuerzo, solo tiempo transcurrido.
- Las fechas se guardan en UTC (`timestamp` sin zona); se presentan en `America/Managua`.
- La mayoría de los clientes no tienen empresa/área asociada, así que «Área solicitante» aporta poco por ahora.
- Las colas siguen el patrón `Unidad::Categoría`; el recinto (RUSB, RUPAP) se deduce del nombre de la cola.
- Se excluyen colas técnicas (Raw, Postmaster, Junk, Misc, trash) y los avisos automáticos del planificador.

## Endpoints (`/api/v1`)

Filtros comunes: `from`, `to` (yyyy-MM-dd, inclusivos; por defecto, últimos 30 días), `area`, `unit`, `category`,
`status`, `agentId`, `priorityId`, `location`.

| Método | Ruta | Uso |
|---|---|---|
| GET | `/catalogs` | Opciones de filtros, rango de datos, objetivos |
| GET | `/analytics/overview` | Módulo Análisis |
| GET | `/agents` | Módulo Agentes |
| GET | `/tickets?view=all\|attention&page=&pageSize=` | Tickets que explican las cifras |
| GET | `/tickets/{id}/timeline` | Historial legible de un ticket |
| GET | `/locations` | Módulo Mapa institucional |
| GET | `/data-quality` | Limitaciones de los datos |
| GET | `/reports/{servicio\|atencion\|agentes}?format=xlsx\|pdf` | Excel para analizar o PDF para compartir |
| POST | `/refresh` | Descarta la caché (no escribe en Znuny) |
| GET | `/health` | Estado de la conexión (`Healthy` / `Unhealthy`) |

En desarrollo, Swagger UI está en la raíz (`/`) y el documento OpenAPI en `/swagger/v1/swagger.json`.
