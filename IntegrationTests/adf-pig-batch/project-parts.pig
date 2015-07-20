-- This script performs a custom projection of the TPC-H Part table
 
-- Required parameters of this script and their example values: 
-- %default PARTINPUT 'wasb://adfwalkthrough@mystorage.blob.core.windows.net/logs/partitionedgameevents/yearno=2014/monthno=5/dayno=1/' 
-- %default PROJECTIONOUTPUT 'wasb://adfwalkthrough@mystorage.blob.core.windows.net/logs/enrichedgameevents/yearno=2014/monthno=5/dayno=1/' 
 
 
-- remove output if it already exists, to ensure idempotency if job is rerun 
fs -mkdir -p $PROJECTIONOUTPUT; 
fs -touchz $PROJECTIONOUTPUT/_tmp; 
fs -rmr -skipTrash $PROJECTIONOUTPUT; 
 
-- load raw stats from appropriate partition 
Parts = LOAD '$PARTINPUT' USING PigStorage('|') AS (P_PARTKEY:int, P_NAME:chararray, P_MFGR:chararray, P_BRAND:chararray, P_TYPE:chararray, P_SIZE:int, P_CONTAINER:chararray, P_RETAILPRICE:float, P_COMMENT:chararray); 
 
-- Custom projection
ProjectedParts = FOREACH Parts GENERATE P_PARTKEY, P_NAME; 
 
-- save results using a different delimiter 
STORE ProjectedParts INTO '$PROJECTIONOUTPUT' USING PigStorage (','); 
