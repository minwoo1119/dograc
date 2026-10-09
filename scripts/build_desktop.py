#!/usr/bin/env python3
"""Desktop packaging script for dograc launcher using PyInstaller and Velopack."""
import os
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

# Ensure UTF-8 output on Windows consoles/CI
if sys.stdout and hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass
if sys.stderr and hasattr(sys.stderr, "reconfigure"):
    try:
        sys.stderr.reconfigure(encoding="utf-8")
    except Exception:
        pass

ROOT_DIR = Path(__file__).resolve().parent.parent
DIST_DIR = ROOT_DIR / "dist"
BUILD_DIR = ROOT_DIR / "build"
RELEASES_DIR = ROOT_DIR / "artifacts" / "releases"

# Import version specification
sys.path.insert(0, str(ROOT_DIR))
from launcher.version import __version__ as APP_VERSION, APP_NAME


def find_vpk_tool() -> str | None:
    """Locate or install the vpk (Velopack CLI) tool."""
    vpk_cmd = shutil.which("vpk") or shutil.which("vpk.exe")
    if vpk_cmd:
        return vpk_cmd

    dotnet_tool_path = Path.home() / ".dotnet" / "tools" / ("vpk.exe" if sys.platform.startswith("win") else "vpk")
    if dotnet_tool_path.exists():
        return str(dotnet_tool_path)

    # Try installing vpk if dotnet is available
    if shutil.which("dotnet"):
        print("[안내] Velopack CLI(vpk)를 설치합니다...")
        try:
            subprocess.run(["dotnet", "tool", "install", "-g", "vpk"], check=True)
            if dotnet_tool_path.exists():
                return str(dotnet_tool_path)
        except Exception as e:
            print(f"[경고] vpk 자동 설치 실패: {e}")

    return None


def build_standalone() -> None:
    print("=" * 60)
    print(f"dograc v{APP_VERSION} 데스크톱 애플리케이션 빌드 및 Velopack 패키징 시작")
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
    if RELEASES_DIR.exists():
        shutil.rmtree(RELEASES_DIR)
    RELEASES_DIR.mkdir(parents=True, exist_ok=True)

    # 3. Run PyInstaller
    entry_point = ROOT_DIR / "run_launcher.py"
    icon_ico = ROOT_DIR / "launcher" / "assets" / "icon.ico"
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
        f"--add-data={ROOT_DIR / 'launcher' / 'assets'}{os.pathsep}launcher/assets",
        f"--add-data={ROOT_DIR / '.env.example'}{os.pathsep}.",
    ]
    if icon_ico.exists():
        cmd.append(f"--icon={icon_ico}")
    cmd.append(str(entry_point))

    print(f"PyInstaller 명령 실행: {' '.join(cmd)}")
    subprocess.run(cmd, cwd=str(ROOT_DIR), check=True)

    package_dir = DIST_DIR / "dograc-launcher"
    if not package_dir.exists():
        raise RuntimeError("빌드 결과 디렉터리를 찾을 수 없습니다.")

    # Copy dograc.bat and readme
    if (ROOT_DIR / "dograc.bat").exists():
        shutil.copy(ROOT_DIR / "dograc.bat", package_dir)

    # Bundle apps directory for standalone execution
    dest_apps = package_dir / "apps"
    if not dest_apps.exists():
        print("독립 실행을 위해 apps 소스 및 웹 산출물 복사 중...")
        shutil.copytree(
            ROOT_DIR / "apps" / "api",
            dest_apps / "api",
            ignore=shutil.ignore_patterns(".venv", "__pycache__", ".pytest_cache", "*.pyc"),
        )
        shutil.copytree(
            ROOT_DIR / "apps" / "web",
            dest_apps / "web",
            ignore=shutil.ignore_patterns("node_modules", ".next/cache"),
        )

    # Copy launcher assets directly to package_dir
    dest_assets = package_dir / "launcher" / "assets"
    if not dest_assets.exists():
        dest_assets.parent.mkdir(parents=True, exist_ok=True)
        shutil.copytree(ROOT_DIR / "launcher" / "assets", dest_assets)

    # 4. Velopack Packaging
    vpk_cmd = find_vpk_tool()
    if vpk_cmd:
        print(f"\n[Velopack] vpk를 사용하여 Windows 설치 프로그램 및 릴리스 패키징 진행: {vpk_cmd}")
        vpk_args = [
            vpk_cmd,
            "pack",
            "--packId", APP_NAME,
            "--packVersion", APP_VERSION,
            "--packDir", str(package_dir),
            "--mainExe", "dograc-launcher.exe",
            "--packTitle", APP_NAME,
            "--packAuthors", "minwoo1119",
            "--outputDir", str(RELEASES_DIR),
            "--channel", "win",
            "--runtime", "win-x64",
        ]
        if icon_ico.exists():
            vpk_args.extend(["--icon", str(icon_ico)])
        print(f"실행: {' '.join(vpk_args)}")
        subprocess.run(vpk_args, check=True)
        print("\n[성공] Velopack 패키징 완료:")
        for item in RELEASES_DIR.glob("*"):
            print(f" - {item.name} ({round(item.stat().st_size / 1024 / 1024, 2)} MB)")
    else:
        print("\n[안내] vpk를 찾을 수 없어 기본 Portable ZIP 생성을 진행합니다.")
        zip_output = RELEASES_DIR / "dograc-win-Portable.zip"
        with zipfile.ZipFile(zip_output, "w", zipfile.ZIP_DEFLATED) as zf:
            for file_path in package_dir.rglob("*"):
                zf.write(file_path, arcname=file_path.relative_to(package_dir))
        print(f" - 릴리스 파일: {zip_output}")


if __name__ == "__main__":
    build_standalone()
