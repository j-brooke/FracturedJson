#!/usr/bin/env sh
set -e

# Make a .nupkg file usable as a .NET global tool.
dotnet pack -c Release -o ./bin/publish/

# Make a self-contained, no-dotnet-required executable for each platform.
dotnet publish -c Release -r win-x64 -o ./bin/publish/win-x64/
cd ./bin/publish/win-x64
zip -r ../fracjson-win-x64.zip .
cd ../../../

dotnet publish -c Release -r win-arm64 -o ./bin/publish/win-arm64/
cd ./bin/publish/win-arm64
zip -r ../fracjson-win-arm64.zip .
cd ../../../

dotnet publish -c Release -r linux-x64 -o ./bin/publish/linux-x64/
cd ./bin/publish/linux-x64
tar -czf ../fracjson-linux-x64.tar.gz .
cd ../../../

dotnet publish -c Release -r linux-arm64 -o ./bin/publish/linux-arm64/
cd ./bin/publish/linux-arm64
tar -czf ../fracjson-linux-arm64.tar.gz .
cd ../../../

dotnet publish -c Release -r osx-x64 -o ./bin/publish/osx-x64/
cd ./bin/publish/osx-x64
tar -czf ../fracjson-osx-x64.tar.gz .
cd ../../../

dotnet publish -c Release -r osx-arm64 -o ./bin/publish/osx-arm64/
cd ./bin/publish/osx-arm64
tar -czf ../fracjson-osx-arm64.tar.gz .
cd ../../../

