#!/bin/bash
newJar="dash-azure-storage-2.2.0.jar"
newJarLocation="/tmp/"
hadoopDir="/usr/hdp/"

dirs="$(find $hadoopDir -name azure*storage*jar)" 
echo $dirs

i=0
retries=10
range=20

while true
do
  let "i=i+1"
  echo "Iteration $i"
  wget --no-check-certificate -O $newJarLocation$newJar  https://www.dash-update.net/client/latest/StorageSDK2.0/$newJar
  if [ $? -eq 0 ]
  then
    echo "Made $i attempts to download library https://www.dash-update.net/client/latest/StorageSDK2.0/$newJar. Last status: $?"
    break  
  fi
  
  if [$i -gt $retries]
  then
    echo "Failed to download library https://www.dash-update.net/client/latest/StorageSDK2.0/$newJar. Last status: $?"
	exit 1
  fi
  
  number=$RANDOM
  echo " $number mod $range"
  let "number %= $range"
  echo "Sleep for $number seconds"
  sleep $number
  
done

for destFile in $dirs
do
      dir=$(dirname $(readlink -f $destFile))
      echo "replacing $destFile in $dir";
      sudo cp -f $newJarLocation$newJar $dir
      sudo mv -f $destFile $destFile.old
done