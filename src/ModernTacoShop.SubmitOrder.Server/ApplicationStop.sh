#!/bin/sh

serviceName="moderntacoshop_submitorder"

if systemctl --all --type service | grep -q "$serviceName";then
    systemctl stop $serviceName
    systemctl disable $serviceName
fi
