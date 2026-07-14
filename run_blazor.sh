#!/bin/bash
cd src/Kairos.Web
dotnet run > blazor_output.log 2>&1 &
echo $! > blazor.pid
