#!/bin/bash

mkdir -p wsdl/fixed
cp -v wsdl/orginal/*.wsdl wsdl/fixed

wsdl=$(find wsdl/fixed -mindepth 1 -maxdepth 2 -name '*.wsdl' | sort -n)

# echo $wsdl

# repleaces xsd:sequence with xsd:all since the controller does not respect the ordering in all cases. At least not for WSDate.

for filepath in wsdl/fixed/*.wsdl; do
  echo Fix not implemented - below code does not work yet so do by hand:
  # TODO Within <xsd:complexType name="WSDate"> replace <xsd:sequence> with <xsd:all> start+endtag
  #sed -E -i '' -e 's#<xsd:sequence>#<xsd:all>#g' -e 's#</xsd:sequence>#</xsd:all>#g' $filepath
done
