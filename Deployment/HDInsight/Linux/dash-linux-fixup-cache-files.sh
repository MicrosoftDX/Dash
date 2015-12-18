#!/bin/bash
# Download DASH modified Azure Storage lib
newJar="dash-azure-storage-2.2.0.jar"
newJarLocation="/tmp/"
wget --no-check-certificate -O $newJarLocation$newJar  https://www.dash-update.net/client/latest/StorageSDK2.0/$newJar

# Pull down all the shared cache tarballs so that we can replace the Azure Storage jar
mkdir ~/hdp-cache
cd ~/hdp-cache
tarballs="$(hadoop fs -ls /hdp/apps/*/*/*.tar.gz | tr -s ' ' | cut -d ' ' -f8)"
for tar in $tarballs
do
	hadoop fs -copyToLocal $tar .
	tarname=$(basename $tar)
	tarfile="$(readlink -f $tarname)"
	tarprefix="${tarname%%.*}"
	mkdir $tarprefix
	tar -xzf $tarfile -C $tarprefix
	jarfiles="$(find . -name azure*storage*jar)"
	for jar in $jarfiles
	do
		dir=$(dirname $(readlink -f $jar))
		echo "Replacing Azure storage jar in $dir"
		cp -f $newJarLocation$newJar $dir
		rm -f $jar
	done
	cd $tarprefix
	tar -zcf $tarfile *
	cd ..
	hadoop fs -copyFromLocal -p -f $tarfile $tar 			
done


