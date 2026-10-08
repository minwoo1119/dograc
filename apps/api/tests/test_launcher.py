import json
import sys
from pathlib import Path
from unittest.mock import MagicMock, patch

# Ensure root dir is in sys.path
ROOT_DIR = Path(__file__).resolve().parent.parent.parent.parent
if str(ROOT_DIR) not in sys.path:
    sys.path.insert(0, str(ROOT_DIR))

from launcher.config import API_URL, WEB_URL
from launcher.services import ServiceManager, ServiceStatus
from launcher.updater import UpdateService, parse_version_tuple


def test_service_manager_initialization():
    logs = []
    manager = ServiceManager(log_callback=lambda msg: logs.append(msg))
    manager.log("테스트 로그")
    assert len(logs) == 1
    assert "테스트 로그" in logs[0]


def test_url_accessible_mock():
    manager = ServiceManager()
    with patch("urllib.request.urlopen") as mock_urlopen:
        mock_resp = MagicMock()
        mock_resp.status = 200
        mock_resp.__enter__.return_value = mock_resp
        mock_urlopen.return_value = mock_resp

        assert manager.is_url_accessible("http://mock-service:8000") is True

    with patch("urllib.request.urlopen", side_effect=Exception("Connection refused")):
        assert manager.is_url_accessible("http://mock-service:8000") is False


def test_check_backend_status():
    manager = ServiceManager()
    with patch.object(manager, "is_url_accessible", return_value=True):
        st = manager.check_backend()
        assert st.is_ready is True
        assert "정상 준비 완료" in st.message

    with patch.object(manager, "is_url_accessible", return_value=False):
        st = manager.check_backend()
        assert st.is_ready is False
        assert "미가동" in st.message


def test_check_ollama_status():
    manager = ServiceManager()
    with patch.object(manager, "is_url_accessible", return_value=True):
        st = manager.check_ollama()
        assert st.is_ready is True
        assert "로컬 Ollama 서비스 연결됨" in st.message

    with patch.object(manager, "is_url_accessible", return_value=False):
        st = manager.check_ollama()
        assert st.is_ready is False
        assert "미응답" in st.message


def test_open_browser_hook():
    manager = ServiceManager()
    with patch("webbrowser.open") as mock_open:
        manager.open_browser()
        mock_open.assert_called_once_with(WEB_URL)


def test_version_tuple_parsing():
    assert parse_version_tuple("v0.1.0") == (0, 1, 0)
    assert parse_version_tuple("1.2.3") == (1, 2, 3)
    assert parse_version_tuple("v2.0") == (2, 0)


def test_updater_has_newer_version():
    updater = UpdateService(current_version="0.1.0")
    mock_payload = {
        "tag_name": "v0.2.0",
        "body": "새로운 기능 추가",
        "html_url": "https://github.com/minwoo1119/dograc/releases/tag/v0.2.0",
    }
    with patch("urllib.request.urlopen") as mock_urlopen:
        mock_resp = MagicMock()
        mock_resp.status = 200
        mock_resp.read.return_value = json.dumps(mock_payload).encode("utf-8")
        mock_resp.__enter__.return_value = mock_resp
        mock_urlopen.return_value = mock_resp

        result = updater.check_for_updates()
        assert result.has_update is True
        assert result.latest_version == "v0.2.0"
        assert "v0.2.0" in result.message


def test_updater_already_latest_version():
    updater = UpdateService(current_version="0.2.0")
    mock_payload = {
        "tag_name": "v0.2.0",
        "body": "동일 버전",
        "html_url": "https://github.com/minwoo1119/dograc/releases/tag/v0.2.0",
    }
    with patch("urllib.request.urlopen") as mock_urlopen:
        mock_resp = MagicMock()
        mock_resp.status = 200
        mock_resp.read.return_value = json.dumps(mock_payload).encode("utf-8")
        mock_resp.__enter__.return_value = mock_resp
        mock_urlopen.return_value = mock_resp

        result = updater.check_for_updates()
        assert result.has_update is False
        assert "최신 버전을 사용" in result.message
