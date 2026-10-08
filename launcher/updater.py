import json
import urllib.error
import urllib.request
import webbrowser
from dataclasses import dataclass

from launcher.version import __version__, GITHUB_REPO, RELEASES_API_URL, RELEASES_PAGE_URL


@dataclass
class UpdateCheckResult:
    has_update: bool
    current_version: str
    latest_version: str
    release_notes: str
    download_url: str
    message: str


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

    def check_for_updates(self, timeout: float = 3.0) -> UpdateCheckResult:
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
                )
            return UpdateCheckResult(
                has_update=False,
                current_version=self.current_version,
                latest_version=latest_tag or self.current_version,
                release_notes="",
                download_url=html_url,
                message="현재 최신 버전을 사용하고 있습니다.",
            )

        except urllib.error.URLError:
            # Air-gapped / offline environment or connection failure
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

    def open_download_page(self, url: str | None = None) -> None:
        target = url or RELEASES_PAGE_URL
        webbrowser.open(target)
