#!/bin/bash

serviceName="moderntacoshop_trackorder"
systemctl enable $serviceName
systemctl start $serviceName
