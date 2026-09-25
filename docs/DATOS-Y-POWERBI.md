# Datos locales y análisis en Power BI

La app no envía datos a ningún servidor. Cuando un usuario pulsa **Compartir diagnóstico**, se crea
un `.zip` con:

```
resumen.html      reporte legible de las últimas 24 horas
resumen.txt       resumen del momento
historial/*.csv   un archivo por día, una fila por minuto (últimos 7 días)
eventos.csv       cambios de estado
registro.txt      registro técnico reciente
equipo.json       equipo, red y adaptador en ese momento
```

Todos los equipos generan **exactamente el mismo formato**. Para analizar oficinas y tiendas basta
con guardar los .zip recibidos en una carpeta de SharePoint, extraerlos y combinarlos en Power BI.

## Consolidar en Power BI

1. *Obtener datos → Carpeta de SharePoint* (o *Carpeta*) → filtrar la ruta que contenga `\historial\`.
2. *Combinar archivos*. Origen: UTF-8, delimitador coma, primera fila como encabezados.
3. Tipos: `minute_local` y `minute_utc` como fecha/hora, métricas como número decimal (usan punto
   decimal: configura la configuración regional de la consulta como *Inglés (Estados Unidos)* al
   cambiar el tipo).
4. Quitar duplicados por `device` + `minute_utc`: el mismo equipo puede compartir varias veces.

Para identificar la **sede o tienda** de cada fila, usa `gateway_mac` (la MAC del router: no requiere
permisos y es estable) o `ssid`. Una tabla auxiliar `sedes` con `gateway_mac → nombre de sede` permite
agrupar todo por sede.

## Columnas de `historial/AAAA-MM-DD.csv`

| Columna | Descripción |
|---|---|
| `minute_local`, `minute_utc` | Inicio del minuto |
| `device`, `user`, `app_version` | Equipo, usuario de Windows y versión |
| `connection_type` | `WiFi`, `Ethernet`, `Cellular`, `Other` |
| `adapter`, `ssid`, `bssid` | Adaptador, red Wi-Fi y punto de acceso específico |
| `gateway_ip`, `gateway_mac` | Router de la red (identifica la sede) |
| `provider_hop` | IP del primer equipo del proveedor de internet |
| `vpn` | 1 si había VPN activa |
| `wifi_signal_pct`, `wifi_rssi_dbm`, `wifi_band`, `wifi_channel`, `link_rate_mbps` | Calidad del enlace |
| `router_*`, `provider_*`, `internet_*` | `sent`, `lost`, `avg_ms`, `max_ms`, `jitter_ms` de cada tramo |
| `cloud_sent`, `cloud_failed`, `cloud_avg_ms`, `cloud_max_ms` | Conexiones TCP a Microsoft 365 |
| `mos_avg`, `mos_min` | Calidad estimada para videollamadas (1 a 5) |
| `rx_mbps_avg`, `tx_mbps_avg` | Uso de red de todo el equipo |
| `in_call` | 1 si hubo una llamada (micrófono en uso) en ese minuto |
| `worst_severity`, `worst_diagnosis` | Peor estado del minuto y su causa |
| `seconds_good`, `seconds_fair`, `seconds_poor`, `seconds_down` | Segundos en cada estado |

Valores de `worst_diagnosis`: `AllGood`, `GoodButWeakLink`, `WeakWifi`, `CableIssue`, `LocalNetwork`,
`RouterUnreachable`, `DeviceBusy`, `ProviderIssue`, `ExternalIssue`, `Degraded`, `NoInternet`,
`NoConnection`, `CaptivePortal`, `CloudUnreachable`, `DnsIssue`.

## Ideas de medidas

- **% de minutos con problemas por sede**: `(seconds_poor + seconds_down) / total de segundos`.
- **Llamadas afectadas**: minutos con `in_call = 1` y `mos_min < 3.5`.
- **Causa dominante por sede**: conteo de `worst_diagnosis` distinto de `AllGood`. Si domina
  `WeakWifi`, hay que revisar la cobertura del punto de acceso. Si domina `ProviderIssue`, hay que
  revisar el canal de internet.
- **Puntos de acceso débiles**: promedio de `wifi_signal_pct` por `bssid`.
