#!/bin/bash

rm generatedsrc/*.cs

wsdl=$(find wsdl/fixed -mindepth 1 -maxdepth 2 -name '*.wsdl' | sort -n)

# echo $wsdl

for filepath in wsdl/fixed/*.wsdl; do
  filename=${filepath:5}
  filebaseNoDir=$(basename ${filepath})
  filebase=${filebaseNoDir%.wsdl}
  fileNS=`echo ${filebase:0:1} | tr  '[a-z]' '[A-Z]'`${filebase:1}
  dotnet-svcutil  --serializer XmlSerializer --noStdLib --outputDir generatedSrc --outputFile $filebase --namespace *,Ihc.Soap.$fileNS $filepath
done

rm generatedsrc/*.json
