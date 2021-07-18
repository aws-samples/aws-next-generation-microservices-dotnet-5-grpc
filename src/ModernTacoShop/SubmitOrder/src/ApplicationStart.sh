#!/bin/bash

serviceName="moderntacoshop_submitorder"
systemctl enable $serviceName
systemctl start $serviceName
