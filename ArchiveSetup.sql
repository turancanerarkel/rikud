/*
    RIKUDDB ARŞİV KURULUMU
    ======================
    Canlı tablolar:  son 7 gün  (Dovizler, DovizGecmisi)
    Arşiv tablolar:  7 günden eski (Dovizler_arc, DovizGecmisi_arc)

    Ne yapar?
      1) _arc tablolarını oluşturur (yoksa)
      2) Index'leri koyar
      3) usp_Rikud_ArchiveOldData SP'sini oluşturur
         (batch ile canlıdan arşive TAŞIR — silmez, kayıp yok)
      4) İlk arşivlemeyi başlatır (opsiyonel — aşağıda yorum)

    Çalıştırma: SSMS'te RIKUDDB seçiliyken bir kez çalıştır.
    Sonra WatchDog her gece SP'yi çağıracak; istersen SQL Agent
    ile de zamanlayabilirsin.
*/

SET NOCOUNT ON;
USE RIKUDDB;
GO

/* ========== 1) Dovizler_arc ========== */
IF OBJECT_ID('dbo.Dovizler_arc', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Dovizler_arc
    (
        Id           INT            NOT NULL,          -- canlıdaki orijinal Id
        AltinTipId   INT            NOT NULL,
        Alis         DECIMAL(18,4)  NOT NULL,
        Satis        DECIMAL(18,4)  NOT NULL,
        CreatedDate  DATETIME       NOT NULL,
        ArchivedDate DATETIME       NOT NULL CONSTRAINT DF_Dovizler_arc_ArchivedDate DEFAULT (GETDATE()),
        CONSTRAINT PK_Dovizler_arc PRIMARY KEY CLUSTERED (Id)
    );
    PRINT 'Dovizler_arc olusturuldu.';
END
ELSE PRINT 'Dovizler_arc zaten var.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dovizler_arc_CreatedDate' AND object_id = OBJECT_ID('dbo.Dovizler_arc'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Dovizler_arc_CreatedDate
        ON dbo.Dovizler_arc (CreatedDate DESC)
        INCLUDE (AltinTipId, Alis, Satis);
    PRINT 'IX_Dovizler_arc_CreatedDate olusturuldu.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dovizler_arc_AltinTipId_CreatedDate' AND object_id = OBJECT_ID('dbo.Dovizler_arc'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Dovizler_arc_AltinTipId_CreatedDate
        ON dbo.Dovizler_arc (AltinTipId, CreatedDate DESC)
        INCLUDE (Alis, Satis);
    PRINT 'IX_Dovizler_arc_AltinTipId_CreatedDate olusturuldu.';
END
GO

/* ========== 2) DovizGecmisi_arc ========== */
IF OBJECT_ID('dbo.DovizGecmisi_arc', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DovizGecmisi_arc
    (
        Id                   INT            NOT NULL,
        ModId                INT            NOT NULL,
        GramAltinAlis        DECIMAL(18,4)  NOT NULL,
        GramAltinSatis       DECIMAL(18,4)  NOT NULL,
        GramAltinSatisEFT    DECIMAL(18,4)  NOT NULL,
        AtaCeyrekAlis        DECIMAL(18,4)  NOT NULL,
        AtaCeyrekSatisEFT    DECIMAL(18,4)  NOT NULL,
        AtaCeyrekSatis       DECIMAL(18,4)  NOT NULL,
        AtaYarimAlis         DECIMAL(18,4)  NOT NULL,
        AtaYarimSatisEFT     DECIMAL(18,4)  NOT NULL,
        AtaYarimSatis        DECIMAL(18,4)  NOT NULL,
        AtaLiraAlis          DECIMAL(18,4)  NOT NULL,
        AtaLiraSatisEFT      DECIMAL(18,4)  NOT NULL,
        AtaLiraSatis         DECIMAL(18,4)  NOT NULL,
        YirmiIkiAyarAlis     DECIMAL(18,4)  NOT NULL,
        YirmiIkiAyarSatisEFT DECIMAL(18,4)  NOT NULL,
        YirmiIkiAyarSatis    DECIMAL(18,4)  NOT NULL,
        ZiynetCeyrekAlis     DECIMAL(18,4)  NOT NULL,
        ZiynetCeyrekSatisEFT DECIMAL(18,4)  NOT NULL,
        ZiynetCeyrekSatis    DECIMAL(18,4)  NOT NULL,
        ZiynetYarimAlis      DECIMAL(18,4)  NOT NULL,
        ZiynetYarimSatisEFT  DECIMAL(18,4)  NOT NULL,
        ZiynetYarimSatis     DECIMAL(18,4)  NOT NULL,
        ZiynetLiraAlis       DECIMAL(18,4)  NOT NULL,
        ZiynetLiraSatisEFT   DECIMAL(18,4)  NOT NULL,
        ZiynetLiraSatis      DECIMAL(18,4)  NOT NULL,
        CreatedDate          DATETIME       NOT NULL,
        ArchivedDate         DATETIME       NOT NULL CONSTRAINT DF_DovizGecmisi_arc_ArchivedDate DEFAULT (GETDATE()),
        CONSTRAINT PK_DovizGecmisi_arc PRIMARY KEY CLUSTERED (Id)
    );
    PRINT 'DovizGecmisi_arc olusturuldu.';
END
ELSE PRINT 'DovizGecmisi_arc zaten var.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DovizGecmisi_arc_CreatedDate' AND object_id = OBJECT_ID('dbo.DovizGecmisi_arc'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_DovizGecmisi_arc_CreatedDate
        ON dbo.DovizGecmisi_arc (CreatedDate DESC);
    PRINT 'IX_DovizGecmisi_arc_CreatedDate olusturuldu.';
END
GO

/* ========== 3) Arşivleme SP ========== */
CREATE OR ALTER PROCEDURE dbo.usp_Rikud_ArchiveOldData
    @RetentionDays INT = 7,
    @BatchSize     INT = 20000
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @RetentionDays < 1 SET @RetentionDays = 7;
    IF @BatchSize < 1000 OR @BatchSize > 100000 SET @BatchSize = 20000;

    DECLARE @cutoff DATETIME = DATEADD(DAY, -@RetentionDays, GETDATE());
    DECLARE @moved INT;
    DECLARE @totalDoviz INT = 0;
    DECLARE @totalGecmis INT = 0;

    PRINT CONCAT('Arsiv basladi. Cutoff=', CONVERT(VARCHAR(19), @cutoff, 120),
                 ' RetentionDays=', @RetentionDays, ' BatchSize=', @BatchSize);

    /* --- Dovizler -> Dovizler_arc --- */
    SET @moved = 1;
    WHILE @moved > 0
    BEGIN
        BEGIN TRAN;

        ;WITH cte AS
        (
            SELECT TOP (@BatchSize) *
            FROM dbo.Dovizler WITH (READPAST)
            WHERE CreatedDate < @cutoff
            ORDER BY CreatedDate, Id
        )
        DELETE FROM cte
        OUTPUT
            deleted.Id,
            deleted.AltinTipId,
            deleted.Alis,
            deleted.Satis,
            deleted.CreatedDate,
            GETDATE()
        INTO dbo.Dovizler_arc (Id, AltinTipId, Alis, Satis, CreatedDate, ArchivedDate);

        SET @moved = @@ROWCOUNT;
        SET @totalDoviz += @moved;

        COMMIT;

        IF @moved > 0
        BEGIN
            PRINT CONCAT('  Dovizler tasindi=', @moved, ' toplam=', @totalDoviz);
            WAITFOR DELAY '00:00:00.500';
        END
    END

    /* --- DovizGecmisi -> DovizGecmisi_arc --- */
    SET @moved = 1;
    WHILE @moved > 0
    BEGIN
        BEGIN TRAN;

        ;WITH cte AS
        (
            SELECT TOP (@BatchSize) *
            FROM dbo.DovizGecmisi WITH (READPAST)
            WHERE CreatedDate < @cutoff
            ORDER BY CreatedDate, Id
        )
        DELETE FROM cte
        OUTPUT
            deleted.Id,
            deleted.ModId,
            deleted.GramAltinAlis,
            deleted.GramAltinSatis,
            deleted.GramAltinSatisEFT,
            deleted.AtaCeyrekAlis,
            deleted.AtaCeyrekSatisEFT,
            deleted.AtaCeyrekSatis,
            deleted.AtaYarimAlis,
            deleted.AtaYarimSatisEFT,
            deleted.AtaYarimSatis,
            deleted.AtaLiraAlis,
            deleted.AtaLiraSatisEFT,
            deleted.AtaLiraSatis,
            deleted.YirmiIkiAyarAlis,
            deleted.YirmiIkiAyarSatisEFT,
            deleted.YirmiIkiAyarSatis,
            deleted.ZiynetCeyrekAlis,
            deleted.ZiynetCeyrekSatisEFT,
            deleted.ZiynetCeyrekSatis,
            deleted.ZiynetYarimAlis,
            deleted.ZiynetYarimSatisEFT,
            deleted.ZiynetYarimSatis,
            deleted.ZiynetLiraAlis,
            deleted.ZiynetLiraSatisEFT,
            deleted.ZiynetLiraSatis,
            deleted.CreatedDate,
            GETDATE()
        INTO dbo.DovizGecmisi_arc (
            Id, ModId,
            GramAltinAlis, GramAltinSatis, GramAltinSatisEFT,
            AtaCeyrekAlis, AtaCeyrekSatisEFT, AtaCeyrekSatis,
            AtaYarimAlis, AtaYarimSatisEFT, AtaYarimSatis,
            AtaLiraAlis, AtaLiraSatisEFT, AtaLiraSatis,
            YirmiIkiAyarAlis, YirmiIkiAyarSatisEFT, YirmiIkiAyarSatis,
            ZiynetCeyrekAlis, ZiynetCeyrekSatisEFT, ZiynetCeyrekSatis,
            ZiynetYarimAlis, ZiynetYarimSatisEFT, ZiynetYarimSatis,
            ZiynetLiraAlis, ZiynetLiraSatisEFT, ZiynetLiraSatis,
            CreatedDate, ArchivedDate);

        SET @moved = @@ROWCOUNT;
        SET @totalGecmis += @moved;

        COMMIT;

        IF @moved > 0
        BEGIN
            PRINT CONCAT('  DovizGecmisi tasindi=', @moved, ' toplam=', @totalGecmis);
            WAITFOR DELAY '00:00:00.500';
        END
    END

    PRINT CONCAT('Arsiv bitti. Dovizler=', @totalDoviz, ' DovizGecmisi=', @totalGecmis);

    SELECT
        @totalDoviz  AS MovedDovizler,
        @totalGecmis AS MovedDovizGecmisi,
        @cutoff      AS CutoffDate,
        (SELECT COUNT(*) FROM dbo.Dovizler)        AS LiveDovizler,
        (SELECT COUNT(*) FROM dbo.Dovizler_arc)    AS ArcDovizler,
        (SELECT COUNT(*) FROM dbo.DovizGecmisi)    AS LiveDovizGecmisi,
        (SELECT COUNT(*) FROM dbo.DovizGecmisi_arc) AS ArcDovizGecmisi;
END
GO

PRINT 'usp_Rikud_ArchiveOldData hazir.';
PRINT 'Ilk arsiv icin calistir:  EXEC dbo.usp_Rikud_ArchiveOldData @RetentionDays=7, @BatchSize=20000;';
GO
