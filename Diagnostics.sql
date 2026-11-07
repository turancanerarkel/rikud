/*
    RIKUDDB TANI SCRIPT'I
    SSMS'te RIKUDDB seciliyken calistir. Hicbir veriyi degistirmez, sadece okur.
    Her bolumun sonucunu (grid ciktisini) kopyalayip gonder.
*/

SET NOCOUNT ON;

/* ============ 1) SUNUCU / EDITION / CPU / BELLEK ============ */
SELECT
    SERVERPROPERTY('Edition')                AS Edition,
    SERVERPROPERTY('ProductVersion')         AS ProductVersion,
    si.cpu_count                             AS LogicalCPU,
    si.physical_memory_kb / 1024             AS ServerRamMB,
    (SELECT value_in_use FROM sys.configurations WHERE name = 'max server memory (MB)')        AS MaxServerMemoryMB,
    (SELECT value_in_use FROM sys.configurations WHERE name = 'max degree of parallelism')     AS MAXDOP,
    (SELECT value_in_use FROM sys.configurations WHERE name = 'cost threshold for parallelism')AS CostThreshold,
    (SELECT cntr_value / 1024 FROM sys.dm_os_performance_counters
      WHERE counter_name = 'Total Server Memory (KB)' AND object_name LIKE '%Memory Manager%') AS SqlKullandigiRamMB
FROM sys.dm_os_sys_info si;


/* ============ 2) TABLO SATIR SAYILARI VE BOYUTLARI ============ */
SELECT
    t.name                                                        AS TabloAdi,
    MAX(CASE WHEN i.index_id IN (0,1) THEN p.rows END)            AS SatirSayisi,
    CAST(SUM(a.total_pages) * 8.0 / 1024 AS DECIMAL(18,2))        AS ToplamMB,
    CAST(SUM(a.used_pages)  * 8.0 / 1024 AS DECIMAL(18,2))        AS KullanilanMB
FROM sys.tables t
JOIN sys.indexes i          ON i.object_id = t.object_id
JOIN sys.partitions p       ON p.object_id = i.object_id AND p.index_id = i.index_id
JOIN sys.allocation_units a ON a.container_id = p.partition_id
GROUP BY t.name
ORDER BY ToplamMB DESC;


/* ============ 3) DOVIZLER / DOVIZGECMISI INDEX'LERI ============ */
SELECT
    OBJECT_NAME(i.object_id) AS TabloAdi,
    ISNULL(i.name, '(HEAP)')  AS IndexAdi,
    i.type_desc               AS Tipi,
    STUFF((SELECT ', ' + c.name
           FROM sys.index_columns ic
           JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
           WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
           ORDER BY ic.key_ordinal
           FOR XML PATH('')), 1, 2, '') AS AnahtarKolonlar,
    STUFF((SELECT ', ' + c.name
           FROM sys.index_columns ic
           JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
           WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 1
           FOR XML PATH('')), 1, 2, '') AS IncludeKolonlar,
    us.user_seeks, us.user_scans, us.user_lookups, us.user_updates
FROM sys.indexes i
LEFT JOIN sys.dm_db_index_usage_stats us
       ON us.object_id = i.object_id AND us.index_id = i.index_id AND us.database_id = DB_ID()
WHERE i.object_id IN (OBJECT_ID('dbo.Dovizler'), OBJECT_ID('dbo.DovizGecmisi'))
ORDER BY TabloAdi, i.index_id;


/* ============ 4) EN COK CPU YIYEN 20 SORGU ============ */
SELECT TOP 20
    CAST(qs.total_worker_time / 1000.0 AS DECIMAL(18,1))                        AS ToplamCPU_ms,
    qs.execution_count                                                          AS CalismaSayisi,
    CAST(qs.total_worker_time / qs.execution_count / 1000.0 AS DECIMAL(18,2))   AS OrtCPU_ms,
    qs.total_logical_reads / qs.execution_count                                 AS OrtMantiksalOkuma,
    CAST(qs.total_elapsed_time / qs.execution_count / 1000.0 AS DECIMAL(18,2))  AS OrtSure_ms,
    qs.last_execution_time                                                      AS SonCalisma,
    SUBSTRING(st.text, (qs.statement_start_offset / 2) + 1,
        ((CASE qs.statement_end_offset WHEN -1 THEN DATALENGTH(st.text)
               ELSE qs.statement_end_offset END - qs.statement_start_offset) / 2) + 1) AS SorguMetni
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
ORDER BY qs.total_worker_time DESC;


/* ============ 5) EKSIK INDEX ONERILERI ============ */
SELECT TOP 15
    CAST(mid.statement AS NVARCHAR(200))                    AS Tablo,
    mid.equality_columns                                    AS EsitlikKolonlari,
    mid.inequality_columns                                  AS AralikKolonlari,
    mid.included_columns                                    AS IncludeKolonlari,
    migs.user_seeks + migs.user_scans                       AS KacKezIstendi,
    CAST(migs.avg_total_user_cost * migs.avg_user_impact
         * (migs.user_seeks + migs.user_scans) AS DECIMAL(18,2)) AS Etki
FROM sys.dm_db_missing_index_details mid
JOIN sys.dm_db_missing_index_groups mig  ON mig.index_handle = mid.index_handle
JOIN sys.dm_db_missing_index_group_stats migs ON migs.group_handle = mig.index_group_handle
WHERE mid.database_id = DB_ID()
ORDER BY Etki DESC;


/* ============ 6) SU AN CALISAN SORGULAR ============ */
SELECT
    r.session_id, r.status, r.command, r.wait_type, r.wait_time, r.blocking_session_id,
    r.cpu_time, r.total_elapsed_time, r.reads, r.logical_reads,
    s.program_name, s.host_name, s.login_name,
    SUBSTRING(st.text, (r.statement_start_offset / 2) + 1,
        ((CASE r.statement_end_offset WHEN -1 THEN DATALENGTH(st.text)
               ELSE r.statement_end_offset END - r.statement_start_offset) / 2) + 1) AS SorguMetni
FROM sys.dm_exec_requests r
JOIN sys.dm_exec_sessions s ON s.session_id = r.session_id
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) st
WHERE r.session_id <> @@SPID AND s.is_user_process = 1;


/* ============ 7) ACIK OTURUM / BAGLANTI SAYISI (uygulama bazli) ============ */
SELECT
    s.program_name, s.host_name, s.login_name,
    COUNT(*)                AS OturumSayisi,
    SUM(s.cpu_time)         AS ToplamCPU_ms,
    SUM(s.reads)            AS ToplamOkuma
FROM sys.dm_exec_sessions s
WHERE s.is_user_process = 1
GROUP BY s.program_name, s.host_name, s.login_name
ORDER BY OturumSayisi DESC;


/* ============ 8) EN COK BEKLENEN 15 WAIT ============ */
SELECT TOP 15
    wait_type,
    CAST(wait_time_ms / 1000.0 AS DECIMAL(18,1))    AS ToplamBekleme_sn,
    CAST(signal_wait_time_ms / 1000.0 AS DECIMAL(18,1)) AS CPUBekleme_sn,
    waiting_tasks_count                             AS BeklemeSayisi
FROM sys.dm_os_wait_stats
WHERE wait_type NOT IN (
    'CLR_SEMAPHORE','LAZYWRITER_SLEEP','RESOURCE_QUEUE','SLEEP_TASK','SLEEP_SYSTEMTASK',
    'SQLTRACE_BUFFER_FLUSH','WAITFOR','LOGMGR_QUEUE','CHECKPOINT_QUEUE','REQUEST_FOR_DEADLOCK_SEARCH',
    'XE_TIMER_EVENT','BROKER_TO_FLUSH','BROKER_TASK_STOP','CLR_MANUAL_EVENT','CLR_AUTO_EVENT',
    'DISPATCHER_QUEUE_SEMAPHORE','FT_IFTS_SCHEDULER_IDLE_WAIT','XE_DISPATCHER_WAIT','XE_DISPATCHER_JOIN',
    'SQLTRACE_INCREMENTAL_FLUSH_SLEEP','DIRTY_PAGE_POLL','HADR_FILESTREAM_IOMGR_IOCOMPLETION',
    'SP_SERVER_DIAGNOSTICS_SLEEP','QDS_PERSIST_TASK_MAIN_LOOP_SLEEP','QDS_ASYNC_QUEUE',
    'QDS_SHUTDOWN_QUEUE','PREEMPTIVE_XE_GETTARGETSTATE','BROKER_EVENTHANDLER','SLEEP_DBSTARTUP',
    'SLEEP_MASTERDBREADY','SLEEP_MASTERMDREADY','SLEEP_MASTERUPGRADED','SLEEP_MSDBSTARTUP'
)
  AND waiting_tasks_count > 0
ORDER BY wait_time_ms DESC;


/* ============ 9) DOVIZLER TABLOSU VERI PROFILI ============ */
SELECT
    COUNT(*)                AS ToplamKayit,
    MIN(CreatedDate)        AS EnEskiKayit,
    MAX(CreatedDate)        AS EnYeniKayit,
    COUNT(DISTINCT AltinTipId) AS FarkliAltinTipi
FROM dbo.Dovizler;

SELECT AltinTipId, COUNT(*) AS Adet, MIN(CreatedDate) AS Ilk, MAX(CreatedDate) AS Son
FROM dbo.Dovizler
GROUP BY AltinTipId
ORDER BY Adet DESC;

-- Son 1 saatte kac kayit atilmis? (beklenen: 10 sn'de 1 -> ~360)
SELECT COUNT(*) AS SonBirSaatKayit FROM dbo.Dovizler WHERE CreatedDate >= DATEADD(HOUR, -1, GETDATE());
SELECT COUNT(*) AS SonBirSaatGecmis FROM dbo.DovizGecmisi WHERE CreatedDate >= DATEADD(HOUR, -1, GETDATE());


/* ============ 10) PARAMETRE DEGERI (hangi kaynak kullaniliyor) ============ */
SELECT * FROM dbo.Parametreler;
