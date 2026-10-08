#!/usr/bin/env python3
"""Desktop packaging script for dograc launcher.

Creates a standalone executable and portable ZIP package using PyInstaller.
"""
import os
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

ROOT_DIR = Path(__file__).resolve().parent.parent
DIST_DIR = ROOT_DIR / "dist"
BUILD_DIR = ROOT_DIR / "build"
RELEASES_DIR = ROOT_DIR / "artifacts" / "releases"


def build_standalone() -> None:
    print("=" * 60)
    print("dograc 데스크톱 애플리케이션 패키징 시작")
    print("=" * 60)

    # 1. Check or install PyInstaller
    venv_pyinstaller = Path(sys.executable).parent / ("pyinstaller.exe" if sys.platform.startswith("win") else "pyinstaller")
    if venv_pyinstaller.exists():
        pyinstaller_cmd = str(venv_pyinstaller)
    else:
        pyinstaller_cmd = shutil.which("pyinstaller")

    if not pyinstaller_cmd:
        print("[안내] PyInstaller를 설치합니다...")
        uv_cmd = shutil.which("uv")
        if uv_cmd:
            subprocess.run([uv_cmd, "pip", "install", "pyinstaller"], cwd=str(ROOT_DIR / "apps" / "api"), check=True)
        else:
            subprocess.run([sys.executable, "-m", "pip", "install", "pyinstaller"], check=True)
        pyinstaller_cmd = str(venv_pyinstaller) if venv_pyinstaller.exists() else (shutil.which("pyinstaller") or "pyinstaller")

    # 2. Clean previous build artifacts
    if DIST_DIR.exists():
        shutil.rmtree(DIST_DIR)
    if BUILD_DIR.exists():
        shutil.rmtree(BUILD_DIR)
    RELEASES_DIR.mkdir(parents=True, exist_ok=True)

    # 3. Run PyInstaller
    entry_point = ROOT_DIR / "run_launcher.py"
    cmd = [
        sys.executable,
        "-m",
        "PyInstaller",
        "--noconfirm",
        "--onedir",
        "--windowed",
        "--name=dograc-launcher",
        f"--paths={ROOT_DIR}",
        f"--add-data={ROOT_DIR / 'infra'}{os.pathsep}infra",
        f"--add-data={ROOT_DIR / '.env.example'}{os.pathsep}.",
        str(entry_point),
    ]

    print(f"PyInstaller 명령 실행: {' '.join(cmd)}")
    subprocess.run(cmd, cwd=str(ROOT_DIR), check=True)

    # 4. Create Portable ZIP
    package_dir = DIST_DIR / "dograc-launcher"
    if package_dir.exists():
        # Copy dograc.bat and readme
        if (ROOT_DIR / "dograc.bat").exists():
            shutil.copy(ROOT_DIR / "dograc.bat", package_dir)

        zip_output = RELEASES_DIR / "dograc-win-Portable.zip"
        print(f"\n휴대용 배포본(Portable ZIP) 생성 중: {zip_output}")
        with zipfile.ZipFile(zip_output, "w", zipfile.ZIP_DEFLATED) as zf:
            for file_path in package_dir.rglob("*"):
                zf.write(file_path, arcname=file_path.relative_to(package_dir))

        print(f"\n[성공] 패키징 완료:")
        print(f" - 실행 폴더: {package_dir}")
        print(f" - 릴리스 파일: {zip_output}")
    else:
        print("[경고] 빌드 결과 디렉터리를 찾을 수 없습니다.")


if __name__ == "__main__":
    build_standalone()
