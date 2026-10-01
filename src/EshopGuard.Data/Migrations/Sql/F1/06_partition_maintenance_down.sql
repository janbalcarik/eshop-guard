-- Monthly partitions stay; they are dropped with their parents by the Down of F1DataModel.
DROP FUNCTION IF EXISTS ops.ensure_monthly_partitions(integer);
