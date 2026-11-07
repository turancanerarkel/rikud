/*
    ESKİ EmergencyCleanup.sql YERİNE ARTIK ArchiveSetup.sql KULLAN.

    Veri silinmez — 7 günden eski kayıtlar _arc tablolarına TAŞINIR.
    Geçmiş ekranı canlı + arc birleştirerek okur.

    Adımlar:
      1) ArchiveSetup.sql çalıştır (tablolar + SP)
      2) İlk taşıma:
           EXEC dbo.usp_Rikud_ArchiveOldData @RetentionDays=7, @BatchSize=20000;
      3) WatchDog gece 03:00'te otomatik tekrarlar.
*/
PRINT 'Bu script yerine ArchiveSetup.sql kullan. Veri silme yerine arşive taşıyoruz.';
GO
