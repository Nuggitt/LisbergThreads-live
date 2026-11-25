#!/usr/bin/env python3
"""Upload a published ASP.NET Core site to Simply with explicit FTPS."""

import argparse
import ftplib
import io
import json
import os
from pathlib import Path, PurePosixPath
import ssl
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET


OFFLINE_NAME = "app_offline.htm"
OFFLINE_PAGE = (
    '<!doctype html><html lang="da"><meta charset="utf-8">'
    '<title>Lisberg Threads</title><h1>Vi opdaterer hjemmesiden</h1>'
    '<p>Vi er snart tilbage.</p></html>'
).encode("utf-8")


def publication_files(site):
    for required in ("LisbergThreads.Api.exe", "web.config", "wwwroot/index.html"):
        if not (site / required).is_file():
            raise ValueError(f"Publiceringen mangler {required}.")
    try:
        hosting = ET.parse(site / "web.config").find(".//aspNetCore")
    except ET.ParseError:
        raise ValueError("Publiceringens web.config er ugyldig.") from None
    if (
        hosting is None
        or hosting.get("hostingModel", "").casefold() != "outofprocess"
        or not hosting.get("processPath", "").endswith("LisbergThreads.Api.exe")
        or (site / "wwwroot/web.config").exists()
    ):
        raise ValueError("Publiceringens IIS-opsætning passer ikke til den selvstændige Windows-app.")
    files = []
    for path in sorted(site.rglob("*")):
        if path.is_symlink():
            raise ValueError("Publiceringen må ikke indeholde symbolske links.")
        if not path.is_file():
            continue
        relative = path.relative_to(site)
        parts = [part.casefold() for part in relative.parts]
        name = path.name.casefold()
        if any(ord(character) < 32 or ord(character) == 127 for character in relative.as_posix()):
            raise ValueError("Publiceringen indeholder et ugyldigt filnavn.")
        if (
            any(part in {"app_data", ".git", ".github"} for part in parts)
            or name == OFFLINE_NAME
            or (name.startswith("appsettings") and name.endswith(".json") and name != "appsettings.production.json")
            or name.startswith(".env")
            or path.suffix.casefold() in {".pem", ".key", ".pfx", ".p12", ".publishsettings"}
        ):
            raise ValueError("Publiceringen indeholder en privat fil eller en beskyttet mappe.")
        files.append((path, relative.as_posix()))
    return files


def private_settings(path):
    settings = json.loads(path.read_text(encoding="utf-8-sig"))
    if not settings.get("ConnectionStrings", {}).get("RemoteConnection"):
        raise ValueError("Serverkonfigurationen mangler databaseforbindelsen.")
    if len(settings.get("Jwt", {}).get("Key", "").encode("utf-8")) < 32:
        raise ValueError("Serverkonfigurationen mangler en login-nøgle på mindst 32 bytes.")
    return path.read_bytes()


def remote_size(ftp, name):
    try:
        return ftp.size(name)
    except ftplib.error_perm as error:
        if str(error).startswith("550"):
            return None
        raise


def wait_for_health(url):
    parsed = urllib.parse.urlsplit(url)
    if parsed.scheme != "https" or not parsed.hostname or parsed.username or parsed.password:
        raise ValueError("Helbredskontrollen kræver en HTTPS-adresse uden loginoplysninger.")
    separator = "&" if parsed.query else "?"
    request = urllib.request.Request(
        f"{url}{separator}deployment={time.time_ns()}", headers={"Cache-Control": "no-cache"}
    )
    for attempt in range(12):
        try:
            with urllib.request.urlopen(request, timeout=10) as response:
                if response.status == 200 and response.read(256).strip() == b"Healthy":
                    print("HTTPS /health: OK", flush=True)
                    return
        except (urllib.error.URLError, TimeoutError, OSError):
            pass
        if attempt != 11:
            time.sleep(5)
    raise RuntimeError("Filerne er uploadet, men HTTPS /health blev ikke klar. Kontrollér appens serverlog.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("site", type=Path, help="Mappen med den publicerede API og klient")
    parser.add_argument("--check-only", action="store_true", help="Kontrollér pakken uden netværksadgang")
    parser.add_argument("--settings", type=Path, help="Privat appsettings.Local.json, kun ved første opsætning eller en bevidst konfigurationsændring")
    args = parser.parse_args()
    files = publication_files(args.site.resolve(strict=True))
    settings = private_settings(args.settings) if args.settings else None
    print(f"Publiceringspakken er kontrolleret: {len(files)} filer.", flush=True)
    if args.check_only:
        return

    host = os.environ.get("SIMPLY_FTP_HOST", "ftp.simply.com")
    user = os.environ.get("SIMPLY_FTP_USERNAME", "")
    password = os.environ.get("SIMPLY_FTP_PASSWORD", "")
    if not host or not user or not password:
        raise ValueError("Udfyld SIMPLY_FTP_HOST, SIMPLY_FTP_USERNAME og SIMPLY_FTP_PASSWORD i GitHub Actions secrets eller lokalt i miljøet.")
    if any("\r" in value or "\n" in value for value in (host, user, password)):
        raise ValueError("FTP-oplysningerne må ikke indeholde linjeskift.")
    remote = PurePosixPath(os.environ.get("SIMPLY_REMOTE_DIR", "/public_html"))
    if not remote.is_absolute() or ".." in remote.parts or len(remote.parts) < 2 or remote.parts[1] != "public_html":
        raise ValueError("SIMPLY_REMOTE_DIR skal være /public_html eller en undermappe derunder.")
    health_url = os.environ.get("SIMPLY_HEALTH_URL", "https://lisbergthreads.dk/health")
    health = urllib.parse.urlsplit(health_url)
    if health.scheme != "https" or not health.hostname or health.username or health.password:
        raise ValueError("SIMPLY_HEALTH_URL skal være en HTTPS-adresse uden loginoplysninger.")

    stage = "forbindelse til FTPS-serveren"
    offline = False
    try:
        with ftplib.FTP_TLS(context=ssl.create_default_context(), timeout=60) as ftp:
            ftp.connect(host, 21)
            stage = "FTP-login"
            ftp.login(user, password)
            ftp.prot_p()
            ftp.set_pasv(True)
            stage = "valg af servermappe"
            ftp.cwd(str(remote))
            base = ftp.pwd()
            ftp.voidcmd("TYPE I")
            stage = "kontrol af serverkonfiguration"
            if settings is None and not remote_size(ftp, "appsettings.Local.json"):
                raise ValueError("Serveren mangler appsettings.Local.json. Upload den private serverkonfiguration ved første opsætning.")
            if remote_size(ftp, OFFLINE_NAME) is not None:
                current = io.BytesIO()
                ftp.retrbinary(f"RETR {OFFLINE_NAME}", current.write)
                if current.getvalue() != OFFLINE_PAGE:
                    raise ValueError("Serveren har allerede en anden app_offline.htm. Afklar den eksisterende vedligeholdelse før publicering.")
            stage = "pause af appen"
            ftp.storbinary(f"STOR {OFFLINE_NAME}", io.BytesIO(OFFLINE_PAGE))
            offline = True
            time.sleep(5)
            created = {"."}
            stage = "upload af applikationen"
            for index, (path, relative) in enumerate(files, start=1):
                parent = PurePosixPath(relative).parent
                for folder in reversed([parent, *parent.parents]):
                    folder_name = folder.as_posix()
                    if folder_name in created:
                        continue
                    try:
                        ftp.mkd(folder_name)
                    except ftplib.error_perm as error:
                        if not str(error).startswith("550"):
                            raise
                        ftp.cwd(folder_name)
                        ftp.cwd(base)
                    created.add(folder_name)
                for attempt in range(4):
                    try:
                        with path.open("rb") as stream:
                            ftp.storbinary(f"STOR {relative}", stream, blocksize=256 * 1024)
                        break
                    except ftplib.error_perm as error:
                        if not str(error).startswith("550") or attempt == 3:
                            raise
                        time.sleep(5)
                if index % 100 == 0 or index == len(files):
                    print(f"Uploadet {index}/{len(files)} filer.", flush=True)
            if settings is not None:
                stage = "upload af privat serverkonfiguration"
                ftp.storbinary("STOR appsettings.Local.json", io.BytesIO(settings))
            stage = "genstart af appen"
            ftp.delete(OFFLINE_NAME)
            offline = False
    except (ftplib.Error, OSError, EOFError, ValueError) as error:
        if offline:
            print("Appen forbliver på vedligeholdelsessiden efter den afbrudte upload. Kør publiceringen igen for at færdiggøre den.", file=sys.stderr)
        if isinstance(error, ValueError):
            raise
        raise RuntimeError(f"Publiceringen fejlede under {stage} ({type(error).__name__}). Kontrollér forbindelsen og loginoplysningerne.") from None
    wait_for_health(health_url)


if __name__ == "__main__":
    try:
        main()
    except (ValueError, RuntimeError, OSError) as error:
        print(f"Fejl: {error}", file=sys.stderr)
        sys.exit(1)
