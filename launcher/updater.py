import json
import os
import subprocess
import sys
import tempfile
import urllib.error
import urllib.request
import webbrowser
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable

from launcher.version import __version__, GITHUB_REPO, RELEASES_API_URL, RELEASES_PAGE_URL

try:
    import velopack
    from velopack import GithubSource, UpdateManager
    HAS_VELOPACK = True
except Exception:
    HAS_VELOPACK = False


@dataclass
class UpdateCheckResult:
    has_update: bool
    current_version: str
    latest_version: str
    release_notes: str
    download_url: str
    message: str
    installer_url: str | None = None
    update_info: Any = None


def parse_version_tuple(version_str: str) -> tuple[int, ...]:
    clean = version_str.lstrip("v").strip()
    parts = []
    for part in clean.split("."):
        try:
            parts.append(int(part))
        except ValueError:
            parts.append(0)
    return tuple(parts)


class UpdateService:
    def __init__(self, current_version: str = __version__) -> None:
        self.current_version = current_version
        self.velopack_manager: Any = None

        if HAS_VELOPACK:
            try:
                source = GithubSource(f"https://github.com/{GITHUB_REPO}")
                self.velopack_manager = UpdateManager(source)
            except Exception:
                # Velopack locator might fail in unpackaged / dev environments
                self.velopack_manager = None

    def check_for_updates(self, timeout: float = 4.0) -> UpdateCheckResult:
        # 1. Try Velopack first if available
        if self.velopack_manager is not None:
            try:
                update_info = self.velopack_manager.check_for_updates()
                if update_info is not None:
                    target = update_info.TargetFullRelease
                    target_ver = str(target.Version)
                    return UpdateCheckResult(
                        has_update=True,
                        current_version=self.current_version,
                        latest_version=f"v{target_ver}",
                        release_notes=target.NotesMarkdown or "새로운 버전이 준비되었습니다.",
                        download_url=RELEASES_PAGE_URL,
                        message=f"Velopack: 새 버전 (v{target_ver})이 출시되었습니다!",
                        installer_url=f"https://github.com/{GITHUB_REPO}/releases/download/v{target_ver}/dograc-win-Setup.exe",
                        update_info=update_info,
                    )
                return UpdateCheckResult(
                    has_update=False,
                    current_version=self.current_version,
                    latest_version=self.current_version,
                    release_notes="",
                    download_url=RELEASES_PAGE_URL,
                    message="현재 최신 버전을 사용하고 있습니다. (Velopack 최신 상태)",
                )
            except Exception:
                # Fall back to GitHub API check
                pass

        # 2. Fallback to GitHub Releases API (Dev / Unpackaged / IDE mode)
        try:
            req = urllib.request.Request(
                RELEASES_API_URL,
                headers={
                    "User-Agent": "dograc-launcher-updater",
                    "Accept": "application/vnd.github.v3+json",
                },
            )
            with urllib.request.urlopen(req, timeout=timeout) as response:
                if response.status != 200:
                    return UpdateCheckResult(
                        has_update=False,
                        current_version=self.current_version,
                        latest_version=self.current_version,
                        release_notes="",
                        download_url=RELEASES_PAGE_URL,
                        message=f"업데이트 서버 응답 코드: {response.status}",
                    )
                data = json.loads(response.read().decode("utf-8"))

            latest_tag = data.get("tag_name", "").strip()
            release_body = data.get("body", "").strip()
            html_url = data.get("html_url", RELEASES_PAGE_URL)

            # Find Windows Setup.exe installer asset
            installer_url = None
            assets = data.get("assets", [])
            for asset in assets:
                name = asset.get("name", "")
                if name.endswith("-Setup.exe") or (name.endswith(".exe") and "Setup" in name):
                    installer_url = asset.get("browser_download_url")
                    break

            latest_tuple = parse_version_tuple(latest_tag)
            current_tuple = parse_version_tuple(self.current_version)

            if latest_tuple > current_tuple:
                return UpdateCheckResult(
                    has_update=True,
                    current_version=self.current_version,
                    latest_version=latest_tag,
                    release_notes=release_body[:500] if release_body else "새로운 릴리스가 게시되었습니다.",
                    download_url=html_url,
                    message=f"새 버전 ({latest_tag})이 출시되었습니다!",
                    installer_url=installer_url,
                )
            return UpdateCheckResult(
                has_update=False,
                current_version=self.current_version,
                latest_version=latest_tag or self.current_version,
                release_notes="",
                download_url=html_url,
                message="현재 최신 버전을 사용하고 있습니다.",
                installer_url=installer_url,
            )

        except urllib.error.URLError:
            return UpdateCheckResult(
                has_update=False,
                current_version=self.current_version,
                latest_version=self.current_version,
                release_notes="",
                download_url=RELEASES_PAGE_URL,
                message="오프라인 폐쇄망 환경이거나 GitHub 연결을 건너뜁니다.",
            )
        except Exception as e:
            return UpdateCheckResult(
                has_update=False,
                current_version=self.current_version,
                latest_version=self.current_version,
                release_notes="",
                download_url=RELEASES_PAGE_URL,
                message=f"업데이트 검사 중 오류: {e}",
            )

    def download_and_restart(
        self,
        result: UpdateCheckResult,
        on_progress: Callable[[float], None] | None = None,
    ) -> None:
        """Download update and restart application using Velopack or installer."""
        # Velopack auto update & restart
        if self.velopack_manager is not None and result.update_info is not None:
            def _vpk_progress(percent_int: int) -> None:
                if on_progress:
                    on_progress(min(1.0, max(0.0, percent_int / 100.0)))

            self.velopack_manager.download_updates(result.update_info, _vpk_progress)
            self.velopack_manager.apply_updates_and_restart(result.update_info)
            return

        # Fallback installer download and run
        if result.installer_url:
            installer_path = self.download_installer(result.installer_url, on_progress=on_progress)
            self.launch_installer_and_exit(installer_path)
            return

        # Fallback to browser
        self.open_download_page(result.download_url)

    def download_installer(
        self,
        installer_url: str,
        dest_path: Path | None = None,
        on_progress: Callable[[float], None] | None = None,
        chunk_size: int = 65536,
    ) -> Path:
        """Download installer file with progress callback (0.0 to 1.0)."""
        if dest_path is None:
            temp_dir = Path(tempfile.gettempdir()) / "dograc-updater"
            temp_dir.mkdir(parents=True, exist_ok=True)
            dest_path = temp_dir / "dograc-win-Setup.exe"

        req = urllib.request.Request(
            installer_url,
            headers={"User-Agent": "dograc-launcher-updater"},
        )
        with urllib.request.urlopen(req) as resp, open(dest_path, "wb") as f:
            total_size = int(resp.headers.get("Content-Length", 0))
            downloaded = 0
            while True:
                chunk = resp.read(chunk_size)
                if not chunk:
                    break
                f.write(chunk)
                downloaded += len(chunk)
                if on_progress and total_size > 0:
                    on_progress(min(1.0, downloaded / total_size))

        if on_progress:
            on_progress(1.0)
        return dest_path

    def launch_installer_and_exit(self, installer_path: Path) -> None:
        """Launch installer executable and exit current process."""
        if not installer_path.exists():
            raise FileNotFoundError(f"설치 프로그램을 찾을 수 없습니다: {installer_path}")

        if sys.platform.startswith("win"):
            os.startfile(str(installer_path))
        else:
            subprocess.Popen([str(installer_path)])
        sys.exit(0)

    def open_download_page(self, url: str | None = None) -> None:
        target = url or RELEASES_PAGE_URL
        webbrowser.open(target)
