#!/usr/bin/env bash

#exit if any command fails
set -e

artifactsFolder="./artifacts"

if [ -d $artifactsFolder ]; then
  rm -R $artifactsFolder
fi

dotnet restore thrift.sln
dotnet build  -c Release thrift.sln
cd ./test/JSON/
dotnet test -c Release JSON.csproj
cd ../..
dotnet pack ./src/thrift-netcore/thrift-netcore.csproj -c Release -o ./artifacts