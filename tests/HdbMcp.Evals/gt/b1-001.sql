-- gt/b1-001.sql
-- Median resale price, 4 ROOM, Tampines, calendar 2024.
-- Registration-dated throughout, so no date basis issue.
SELECT TOP 1
    PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY resale_price)
        OVER () AS answer
FROM dbo.resale_transactions
WHERE town = 'TAMPINES'
  AND flat_type = '4 ROOM'
  AND month_text >= '2024-01'
  AND month_text <= '2024-12';