#!/bin/bash

rm generatedsrc/*.cs

wsdl=$(find wsdl -mindepth 1 -maxdepth 2 -name '*.wsdl' | sort -n)

# echo $wsdl

for filepath in wsdl/*.wsdl; do
  filename=${filepath:5}
  filebase=${filename%.wsdl}
  fileNS=`echo ${filebase:0:1} | tr  '[a-z]' '[A-Z]'`${filebase:1}
  dotnet-svcutil  --serializer XmlSerializer --noStdLib --outputDir generatedSrc --outputFile $filebase --namespace *,Ihc.Soap.$fileNS $filepath
done

rm generatedsrc/*.json
