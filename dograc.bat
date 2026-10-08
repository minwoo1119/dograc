@echo off
chcp 65001 > nul
title dograc — 데스크톱 런처

echo ========================================================
echo  dograc: 폐쇄망 및 로컬 환경을 위한 데스크톱-웹 RAG 런처
echo ========================================================
echo.

set ROOT_DIR=%~dp0
cd /d "%ROOT_DIR%"

if exist "apps\api\.venv\Scripts\python.exe" (
    set PYTHON_CMD="apps\api\.venv\Scripts\python.exe"
) else (
    set PYTHON_CMD=python
)

echo 데스크톱 런처를 실행합니다...
%PYTHON_CMD% -m launcher

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [안내] GUI 런처 실행 중 오류가 발생했습니다. CLI 모드로 재시도합니다.
    %PYTHON_CMD% -m launcher --cli
)
pause
