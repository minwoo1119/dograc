import asyncio
import os
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.request
import webbrowser
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

from launcher.config import (
    API_DIR,
    API_HEALTH_READY_URL,
    API_PORT,
    API_URL,
    COMPOSE_FILE,
    OLLAMA_TAGS_URL,
    ROOT_DIR,
    WEB_DIR,
    WEB_PORT,
    WEB_URL,
)


@dataclass
class ServiceStatus:
    name: str
    is_ready: bool
    message: str


def find_executable(name: str) -> str | None:
    """Find system executables with smart fallbacks for common user install locations."""
    # 1. Standard PATH
    found = shutil.which(name)
    if found:
        return found

    is_win = sys.platform.startswith("win")
    exts = [".exe", ".cmd", ".bat"] if is_win else [""]

    # 2. Well-known custom install locations
    custom_paths = []
    if name == "uv":
        custom_paths = [
            Path.home() / ".local" / "bin",
            Path.home() / ".cargo" / "bin",
            Path(os.environ.get("LOCALAPPDATA", "")) / "Programs" / "uv",
        ]
    elif name == "docker":
        custom_paths = [
            Path("C:/Program Files/Docker/Docker/resources/bin"),
            Path("C:/Program Files/Docker/Docker"),
        ]
    elif name in ("pnpm", "npx", "npm", "node"):
        custom_paths = [
            Path("C:/Program Files/nodejs"),
            Path(os.environ.get("LOCALAPPDATA", "")) / "pnpm",
            Path(os.environ.get("APPDATA", "")) / "npm",
        ]

    for p in custom_paths:
        for ext in exts:
            target = p / f"{name}{ext}"
            if target.exists():
                return str(target)

    return None


class ServiceManager:
    def __init__(self, log_callback: Callable[[str], None] | None = None) -> None:
        self.log_callback = log_callback or (lambda msg: print(msg))
        self.backend_process: subprocess.Popen | None = None
        self.frontend_process: subprocess.Popen | None = None

    def log(self, message: str) -> None:
        self.log_callback(f"[{time.strftime('%H:%M:%S')}] {message}")

    def ensure_env_file(self) -> None:
        env_file = ROOT_DIR / ".env"
        env_example = ROOT_DIR / ".env.example"
        if not env_file.exists() and env_example.exists():
            try:
                self.log("로컬 .env 파일이 없어 .env.example 복사본을 자동 생성합니다.")
                shutil.copy(env_example, env_file)
            except Exception as e:
                self.log(f"[경고] .env 복사 실패: {e}")

    def is_url_accessible(self, url: str, timeout: float = 2.0) -> bool:
        try:
            req = urllib.request.Request(url, headers={"User-Agent": "dograc-launcher"})
            with urllib.request.urlopen(req, timeout=timeout) as response:
                return 200 <= response.status < 400
        except Exception:
            return False

    def check_docker(self) -> ServiceStatus:
        docker_cmd = find_executable("docker")
        if not docker_cmd:
            return ServiceStatus(
                name="인프라 스토리지 (Docker)",
                is_ready=False,
                message="Docker를 찾을 수 없습니다. (Docker Desktop 설치 필요)",
            )
        try:
            res = subprocess.run(
                [docker_cmd, "info"],
                capture_output=True,
                text=True,
                timeout=5,
            )
            if res.returncode == 0:
                return ServiceStatus(
                    name="인프라 스토리지 (Docker)",
                    is_ready=True,
                    message="Docker 데몬 정상 가동 중",
                )
            return ServiceStatus(
                name="인프라 스토리지 (Docker)",
                is_ready=False,
                message="Docker 데몬 미응답 (Docker Desktop을 실행해 주세요).",
            )
        except Exception as e:
            return ServiceStatus(
                name="인프라 스토리지 (Docker)",
                is_ready=False,
                message=f"Docker 검사 오류: {e}",
            )

    def start_docker_infrastructure(self) -> bool:
        self.ensure_env_file()
        docker_cmd = find_executable("docker")
        if not docker_cmd:
            self.log("[안내] Docker Desktop이 감지되지 않아 로컬 인프라(DB/Qdrant) 컨테이너 기동을 건너뜁니다.")
            return False

        if not COMPOSE_FILE.exists():
            self.log(f"[안내] Docker compose 파일({COMPOSE_FILE})을 찾을 수 없습니다.")
            return False

        self.log("인프라 컨테이너(PostgreSQL, Qdrant, MinIO) 기동 중...")
        try:
            res = subprocess.run(
                [docker_cmd, "compose", "-f", str(COMPOSE_FILE), "up", "-d"],
                cwd=str(ROOT_DIR),
                capture_output=True,
                text=True,
                timeout=30,
            )
            if res.returncode == 0:
                self.log("인프라 컨테이너 기동 완료.")
                return True
            self.log(f"[경고] Docker Compose 기동 실패: {res.stderr.strip()}")
            return False
        except Exception as e:
            self.log(f"[오류] Docker Compose 실행 중 예외: {e}")
            return False

    def stop_docker_infrastructure(self) -> bool:
        docker_cmd = find_executable("docker")
        if not docker_cmd or not COMPOSE_FILE.exists():
            return False
        self.log("인프라 컨테이너 종료 중...")
        try:
            subprocess.run(
                [docker_cmd, "compose", "-f", str(COMPOSE_FILE), "down"],
                cwd=str(ROOT_DIR),
                capture_output=True,
                timeout=20,
            )
            self.log("인프라 컨테이너 종료 완료.")
            return True
        except Exception as e:
            self.log(f"[오류] Docker 종료 실패: {e}")
            return False

    def check_ollama(self) -> ServiceStatus:
        if self.is_url_accessible(OLLAMA_TAGS_URL):
            return ServiceStatus(
                name="로컬 생성 LLM (Ollama)",
                is_ready=True,
                message="로컬 Ollama 서비스 연결됨 (127.0.0.1:11434)",
            )
        return ServiceStatus(
            name="로컬 생성 LLM (Ollama)",
            is_ready=False,
            message="Ollama 서비스 미응답 (사전 설치 및 가동 권장: ollama run qwen2.5:7b)",
        )

    def check_backend(self) -> ServiceStatus:
        if self.is_url_accessible(API_HEALTH_READY_URL):
            return ServiceStatus(
                name="백엔드 API 서버 (FastAPI)",
                is_ready=True,
                message=f"정상 준비 완료 ({API_URL})",
            )
        if self.is_url_accessible(API_URL):
            return ServiceStatus(
                name="백엔드 API 서버 (FastAPI)",
                is_ready=False,
                message="서버 응답 중이나 종속성 준비 중",
            )
        return ServiceStatus(
            name="백엔드 API 서버 (FastAPI)",
            is_ready=False,
            message=f"미가동 ({API_URL})",
        )

    def check_frontend(self) -> ServiceStatus:
        if self.is_url_accessible(WEB_URL):
            return ServiceStatus(
                name="프론트엔드 웹 UI (Next.js)",
                is_ready=True,
                message=f"정상 서비스 중 ({WEB_URL})",
            )
        return ServiceStatus(
            name="프론트엔드 웹 UI (Next.js)",
            is_ready=False,
            message=f"미가동 ({WEB_URL})",
        )

    def start_backend(self) -> bool:
        if self.backend_process and self.backend_process.poll() is None:
            self.log("백엔드 API 서버가 이미 실행 중입니다.")
            return True

        if not API_DIR.exists():
            self.log(f"[오류] 백엔드 경로({API_DIR})를 찾을 수 없습니다. 프로젝트 소스 디렉터리를 확인해 주세요.")
            return False

        self.ensure_env_file()
        self.log("백엔드 API 서버 구동 시작...")

        # Find uv or python
        uv_cmd = find_executable("uv")
        venv_py = API_DIR / ".venv" / ("Scripts" / "python.exe" if sys.platform.startswith("win") else "bin" / "python")

        if uv_cmd:
            cmd = [uv_cmd, "run", "uvicorn", "app.main:app", "--host", "127.0.0.1", "--port", str(API_PORT)]
        elif venv_py.exists():
            cmd = [str(venv_py), "-m", "uvicorn", "app.main:app", "--host", "127.0.0.1", "--port", str(API_PORT)]
        else:
            python_cmd = sys.executable
            cmd = [python_cmd, "-m", "uvicorn", "app.main:app", "--host", "127.0.0.1", "--port", str(API_PORT)]

        try:
            self.backend_process = subprocess.Popen(
                cmd,
                cwd=str(API_DIR),
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                bufsize=1,
            )
            self.log(f"백엔드 프로세스 시작됨 (PID: {self.backend_process.pid})")
            return True
        except Exception as e:
            self.log(f"[오류] 백엔드 프로세스 시작 실패: {e}")
            return False

    def start_frontend(self) -> bool:
        if self.frontend_process and self.frontend_process.poll() is None:
            self.log("프론트엔드 웹 서버가 이미 실행 중입니다.")
            return True

        if not WEB_DIR.exists():
            self.log(f"[오류] 프론트엔드 경로({WEB_DIR})를 찾을 수 없습니다. 프로젝트 소스 디렉터리를 확인해 주세요.")
            return False

        self.log("프론트엔드 웹 UI 구동 시작...")
        pnpm_cmd = find_executable("pnpm")
        npx_cmd = find_executable("npx")
        npm_cmd = find_executable("npm")

        is_win = sys.platform.startswith("win")
        if pnpm_cmd:
            cmd = [pnpm_cmd, "run", "dev"]
        elif npx_cmd:
            cmd = [npx_cmd, "pnpm", "run", "dev"]
        elif npm_cmd:
            cmd = [npm_cmd, "run", "dev"]
        else:
            self.log("[오류] Node.js 패키지 매니저(pnpm/npx/npm)를 찾을 수 없습니다. Node.js 설치를 확인해 주세요.")
            return False

        try:
            self.frontend_process = subprocess.Popen(
                cmd,
                cwd=str(WEB_DIR),
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                bufsize=1,
                shell=is_win,
            )
            self.log(f"프론트엔드 프로세스 시작됨 (PID: {self.frontend_process.pid})")
            return True
        except Exception as e:
            self.log(f"[오류] 프론트엔드 프로세스 시작 실패: {e}")
            return False

    def wait_for_services(self, timeout_sec: int = 40) -> bool:
        self.log("서비스 준비 상태 헬스체크 대기 중...")
        start_time = time.time()

        while time.time() - start_time < timeout_sec:
            # Check if processes crashed prematurely
            if self.backend_process and self.backend_process.poll() is not None:
                out = self.backend_process.stdout.read() if self.backend_process.stdout else ""
                self.log(f"[오류] 백엔드 프로세스가 예기치 않게 종료되었습니다 (종료 코드: {self.backend_process.poll()}).")
                if out:
                    self.log(f"백엔드 출력: {out[:300].strip()}")
                return False

            if self.frontend_process and self.frontend_process.poll() is not None:
                self.log(f"[오류] 프론트엔드 프로세스가 예기치 않게 종료되었습니다 (종료 코드: {self.frontend_process.poll()}).")
                return False

            backend_ok = self.check_backend().is_ready
            frontend_ok = self.check_frontend().is_ready
            if backend_ok and frontend_ok:
                self.log("모든 서비스 준비 완료!")
                return True
            time.sleep(1.5)

        self.log("[경고] 서비스 준비 대기 시간 초과.")
        return False

    def open_browser(self) -> None:
        self.log(f"웹 브라우저 열기: {WEB_URL}")
        webbrowser.open(WEB_URL)

    def stop_all(self) -> None:
        self.log("실행 중인 모든 데몬 및 프로세스 정리 중...")
        if self.frontend_process and self.frontend_process.poll() is None:
            self.log("프론트엔드 프로세스 종료...")
            try:
                self.frontend_process.terminate()
            except Exception:
                pass
            self.frontend_process = None

        if self.backend_process and self.backend_process.poll() is None:
            self.log("백엔드 프로세스 종료...")
            try:
                self.backend_process.terminate()
            except Exception:
                pass
            self.backend_process = None

        self.stop_docker_infrastructure()
        self.log("모든 프로세스 정리 완료.")
