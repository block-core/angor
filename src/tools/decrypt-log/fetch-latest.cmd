@echo off
rem Double-click to fetch + decrypt the latest Angor log export.
rem Requires fetch-config.json next to this file (see fetch-latest.mjs header).
cd /d "%~dp0"
if not exist node_modules call npm install --no-audit --no-fund
node fetch-latest.mjs %*
pause
