#!/usr/bin/env sh
set -e

dotnet publish -c Release -r win-x64 --self-contained=true -p:PublishSingleFile=true -o ./bin/publish/win-x64/
dotnet publish -c Release -r win-arm64 --self-contained=true -p:PublishSingleFile=true -o ./bin/publish/win-arm64/
dotnet publish -c Release -r linux-x64 --self-contained=true -p:PublishSingleFile=true -o ./bin/publish/linux-x64/
dotnet publish -c Release -r win-x64 --self-contained=true -p:PublishSingleFile=true -o ./bin/publish/win-x64/
dotnet publish -c Release -r linux-arm64 --self-contained=true -p:PublishSingleFile=true -o ./bin/publish/linux-arm64/
dotnet publish -c Release -r osx-x64 --self-contained=true -p:PublishSingleFile=true -o ./bin/publish/osx-x64/
dotnet publish -c Release -r osx-arm64 --self-contained=true -p:PublishSingleFile=true -o ./bin/publish/osx-arm64/
