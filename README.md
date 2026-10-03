# sistema-estadistico-znuny-backend

API REST en **.NET 10 / ASP.NET Core** que lee la base PostgreSQL de Znuny (**solo lectura**) y calcula la
**eficacia, eficiencia y efectividad** del soporte y de cada agente. La consume el frontend
`dashboard-estaditico-web`.

## Ejecutar

Requisitos: SDK de .NET 10 con el runtime de ASP.NET Core (en Arch/CachyOS: `aspnet-runtime-10.0` y `aspnet-targeting-pack-10.0`)
y la base del laboratorio levantada (`sistema-estadistico-znuny-lab`, puerto 5433).

```bash
dotnet run --project src/ZnunyStats.Api      # http://localhost:5080
dotnet test                                  # pruebas de las fórmulas
```

Frontend: en `dashboard-estaditico-web`, `pnpm dev` (usa `NEXT_PUBLIC_API_URL`, por defecto `http://localhost:5080`).

La cadena de conexión de desarrollo está en `appsettings.Development.json`. En otro entorno, usá la variable
`ConnectionStrings__Znuny`. Mantené siempre `Options=-c default_transaction_read_only=on`: PostgreSQL rechaza
cualquier escritura de la sesión, además de que el código solo contiene `SELECT`.

## Cómo está organizado

```
src/ZnunyStats.Api/
  Data/ZnunyQueries.cs          Todo el SQL. Una fila de "hechos" por ticket, reconstruida desde ticket_history.
  Analytics/TicketStore.cs      Clasifica cada ticket (estado, unidad, agente responsable, objetivo) y lo cachea.
  Analytics/Kpis.cs             Las fórmulas. Funciones puras, cubiertas por tests.
  Analytics/AnalyticsService.cs Filtros, períodos y respuestas de cada módulo.
  Endpoints/ApiEndpoints.cs     Rutas HTTP.
  Reports/ExcelReports.cs       Exportación a Excel.
  appsettings.json              Reglas de negocio configurables (sección "Estadisticas").
```

Flujo: **SQL → hechos por ticket → clasificación → fórmulas → JSON**. El volumen actual (cientos de tickets) se
procesa en memoria y se cachea 60 s. Si algún día hay cientos de miles de tickets, el paso natural es mover el
filtro por fecha al SQL; las fórmulas no cambian.

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
| GET | `/reports/{servicio\|atencion\|agentes}` | Excel con hoja de filtros |
| POST | `/refresh` | Descarta la caché (no escribe en Znuny) |
| GET | `/health` | Estado de la conexión |

En desarrollo, el documento OpenAPI está en `/openapi/v1.json`.
