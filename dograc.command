#!/bin/bash
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$DIR"

echo "========================================================"
echo " dograc: 폐쇄망 및 로컬 환경을 위한 데스크톱-웹 RAG 런처"
echo "========================================================"
echo ""

if [ -f "apps/api/.venv/bin/python" ]; then
    PYTHON_CMD="apps/api/.venv/bin/python"
elif command -v python3 &> /dev/null; then
    PYTHON_CMD="python3"
else
    PYTHON_CMD="python"
fi

echo "데스크톱 런처를 실행합니다..."
$PYTHON_CMD -m launcher

if [ $? -ne 0 ]; then
    echo ""
    echo "[안내] GUI 런처 실행 실패. CLI 모드로 실행합니다..."
    $PYTHON_CMD -m launcher --cli
fi
