#!/bin/bash
if [ "$#" -ne 1 ]; then
    echo "Please supply IP address of IHC controller as argument"
    exit 1
fi

cd wsdl
ip=$1
wget=/usr/local/wget/wget

echo Downloading WSDL from IHC controller at $ip

$wget http://$ip/wsdl/authentication.wsdl
$wget http://$ip/wsdl/configuration.wsdl
$wget http://$ip/wsdl/controller.wsdl
$wget http://$ip/wsdl/messagecontrollog.wsdl
$wget http://$ip/wsdl/module.wsdl
$wget http://$ip/wsdl/notificationmanager.wsdl
$wget http://$ip/wsdl/resourceinteraction.wsdl
$wget http://$ip/wsdl/timemanager.wsdl
$wget http://$ip/wsdl/usermanager.wsdl
$wget http://$ip/wsdl/openapi.wsdl
$wget http://$ip/wsdl/airlinkmanagement.wsdl
$wget http://$ip/wsdl/smsmodem.wsdl
$wget http://$ip/wsdl/testihc.wsdl

cd ..
