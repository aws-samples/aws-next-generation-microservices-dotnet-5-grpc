#!/bin/sh

serviceName="moderntacoshop_trackorder"

if systemctl --all --type service | grep -q "$serviceName";then
    systemctl stop $serviceName
    systemctl disable $serviceName
fi
