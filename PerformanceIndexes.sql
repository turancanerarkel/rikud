/*
    RIKUDDB performans index'leri.
    SSMS'te RIKUDDB seçiliyken bir kez çalıştır. Mevcut veriyi değiştirmez,
    sadece okuma sorgularını hızlandırır. Tekrar çalıştırmaya karşı korumalıdır.

    Hedef sorgular:
      - WatchDog:  Dovizler.OrderByDescending(CreatedDate).First()      (her 60 sn)
      - WebUI LivePrices: Dovizler.Where(CreatedDate>=...).GroupBy(AltinTipId)...latest
      - WebUI PricesAt10: Dovizler tarih aralığı sorguları
      - WebUI _ExchangeHistory: DovizGecmisi tarih aralığı sorgusu
*/

SET NOCOUNT ON;

-- 1) Dovizler: AltinTipId + CreatedDate (LivePrices: tip başına en yeni kayıt -> seek)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dovizler_AltinTipId_CreatedDate' AND object_id = OBJECT_ID('dbo.Dovizler'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Dovizler_AltinTipId_CreatedDate
        ON dbo.Dovizler (AltinTipId, CreatedDate DESC)
        INCLUDE (Alis, Satis);
    PRINT 'IX_Dovizler_AltinTipId_CreatedDate olusturuldu.';
END
ELSE PRINT 'IX_Dovizler_AltinTipId_CreatedDate zaten var.';

-- 2) Dovizler: CreatedDate (WatchDog son kayit + PricesAt10 tarih araligi)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Dovizler_CreatedDate' AND object_id = OBJECT_ID('dbo.Dovizler'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Dovizler_CreatedDate
        ON dbo.Dovizler (CreatedDate DESC);
    PRINT 'IX_Dovizler_CreatedDate olusturuldu.';
END
ELSE PRINT 'IX_Dovizler_CreatedDate zaten var.';

-- 3) DovizGecmisi: CreatedDate (_ExchangeHistory grid tarih araligi)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DovizGecmisi_CreatedDate' AND object_id = OBJECT_ID('dbo.DovizGecmisi'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_DovizGecmisi_CreatedDate
        ON dbo.DovizGecmisi (CreatedDate DESC);
    PRINT 'IX_DovizGecmisi_CreatedDate olusturuldu.';
END
ELSE PRINT 'IX_DovizGecmisi_CreatedDate zaten var.';
GO
