#!/usr/bin/env bash
set -euo pipefail

api_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
repo_root="$(dirname -- "$api_root")"
publish_rid="${1:-win-x86}"
case "$publish_rid" in
  win-x64|win-x86) ;;
  *) echo 'Brug win-x64 eller win-x86, svarende til Simply-webhotellets app-pool.' >&2; exit 1 ;;
esac
publish_stamp="$(date -u +%Y%m%dT%H%M%SZ)-$$"
publish_root="$api_root/artifacts/simply-$publish_stamp"

dotnet publish "$api_root/LisbergThreads.Api.csproj" -c Release -r "$publish_rid" --self-contained true \
  -p:AspNetCoreHostingModel=OutOfProcess -o "$publish_root/site"
dotnet publish "$repo_root/LisbergThreads.Client/LisbergThreads.Client.csproj" -c Release \
  -o "$publish_root/client"
mkdir -p "$publish_root/site/wwwroot"
cp -a "$publish_root/client/wwwroot/." "$publish_root/site/wwwroot/"
# Static Blazor assets are served by ASP.NET Core; do not install a second IIS handler in wwwroot.
rm -f -- "$publish_root/site/wwwroot/web.config"

if find "$publish_root/site" -type f \( -name 'appsettings.Local.json' -o -name 'appsettings.json' -o -name 'appsettings.Development.json' \) -print -quit | read -r unexpected_file; then
  echo 'Publiceringen indeholder en privat konfigurationsfil. Upload er afbrudt.' >&2
  exit 1
fi
printf 'Publiceringsmappe: %s\n' "$publish_root/site"
printf '%s\n' 'Upload først efter opsætning af database, HTTPS og privat serverkonfiguration. Se DEPLOYMENT.md.'
