dotnet publish src/KubeTools4Dev -c Release -r win-x64 -p:PublishSingleFile=true --self-contained
Write-Host "Publish complete. Output in src/KubeTools4Dev/bin/Release/net10.0/win-x64/publish/"
