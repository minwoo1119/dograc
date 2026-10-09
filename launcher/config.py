import os
import sys
from pathlib import Path


def is_valid_project_root(p: Path) -> bool:
    """Check if directory contains a valid dograc project with apps/api."""
    return (p / "apps" / "api").is_dir()


def is_active_dev_root(p: Path) -> bool:
    """Check if directory is a fully prepared development repository with dependencies."""
    if not is_valid_project_root(p):
        return False
    # Check for venv, node_modules, or git repository
    has_venv = (p / "apps" / "api" / ".venv").is_dir()
    has_node = (p / "apps" / "web" / "node_modules").is_dir()
    has_git = (p / ".git").is_dir()
    return has_venv or has_node or has_git


def find_project_root() -> Path:
    """Intelligently resolve repository/project root in source, bundled, or installed mode."""
    # 1. Explicit environment variable
    env_root = os.environ.get("DOGRAC_ROOT")
    if env_root:
        p = Path(env_root)
        if is_valid_project_root(p):
            return p

    # 2. Running directly from source code
    script_root = Path(__file__).resolve().parent.parent
    if is_active_dev_root(script_root):
        return script_root

    # 3. Current working directory if running in active repo
    cwd = Path.cwd()
    if is_active_dev_root(cwd):
        return cwd

    # 4. Common user clone locations (prioritize ones with dependencies/git)
    candidates = [
        Path.home() / "Documents" / "Github" / "dograc",
        Path.home() / "Documents" / "dograc",
        Path.home() / "Desktop" / "dograc",
        Path.home() / "dograc",
    ]
    for cand in candidates:
        if is_active_dev_root(cand):
            return cand

    # 5. Running from PyInstaller bundle with bundled apps
    if getattr(sys, "frozen", False):
        exe_dir = Path(sys.executable).parent
        if is_valid_project_root(exe_dir):
            return exe_dir
        if hasattr(sys, "_MEIPASS") and is_valid_project_root(Path(sys._MEIPASS)):
            return Path(sys._MEIPASS)

    # 6. Fallback to basic valid roots
    if is_valid_project_root(cwd):
        return cwd
    for cand in candidates:
        if is_valid_project_root(cand):
            return cand

    return script_root


ROOT_DIR = find_project_root()

# Apps and infra paths
API_DIR = ROOT_DIR / "apps" / "api"
WEB_DIR = ROOT_DIR / "apps" / "web"
INFRA_DIR = ROOT_DIR / "infra"
COMPOSE_FILE = INFRA_DIR / "compose.yaml"

def find_assets_dir() -> Path:
    """Find launcher assets directory in source, PyInstaller bundle, or installed mode."""
    if hasattr(sys, "_MEIPASS"):
        p = Path(sys._MEIPASS) / "launcher" / "assets"
        if p.exists():
            return p
    if getattr(sys, "frozen", False):
        exe_dir = Path(sys.executable).parent
        for cand in [
            exe_dir / "_internal" / "launcher" / "assets",
            exe_dir / "launcher" / "assets",
            exe_dir / "assets",
        ]:
            if cand.exists():
                return cand
    src_assets = Path(__file__).resolve().parent / "assets"
    if src_assets.exists():
        return src_assets
    return ROOT_DIR / "launcher" / "assets"


ASSETS_DIR = find_assets_dir()
ICON_ICO = ASSETS_DIR / "icon.ico"
ICON_PNG = ASSETS_DIR / "icon.png"
LOGO_40_PNG = ASSETS_DIR / "logo_40.png"

# Ports and URLs
API_HOST = "127.0.0.1"
API_PORT = 8000
API_URL = f"http://{API_HOST}:{API_PORT}"
API_HEALTH_LIVE_URL = f"{API_URL}/api/v1/health/live"
API_HEALTH_READY_URL = f"{API_URL}/api/v1/health/ready"

WEB_HOST = "127.0.0.1"
WEB_PORT = 3000
WEB_URL = f"http://{WEB_HOST}:{WEB_PORT}"

OLLAMA_HOST = "127.0.0.1"
OLLAMA_PORT = 11434
OLLAMA_TAGS_URL = f"http://{OLLAMA_HOST}:{OLLAMA_PORT}/api/tags"

QDRANT_HOST = "127.0.0.1"
QDRANT_PORT = 6333
QDRANT_URL = f"http://{QDRANT_HOST}:{QDRANT_PORT}"

# KDS Theme Colors for Launcher GUI
KDS_BLUE_600 = "#5055B1"
KDS_BLUE_800 = "#3F4391"
KDS_BLUE_50 = "#F0F2FA"
KDS_GRAY_50 = "#FAFBFC"
KDS_GRAY_100 = "#F4F5F7"
KDS_GRAY_300 = "#E0E2E6"
KDS_GRAY_700 = "#4D5159"
KDS_GRAY_900 = "#1B1D22"
KDS_GREEN_600 = "#4DAC27"
KDS_RED_500 = "#EC1F2D"
KDS_AMBER_600 = "#D97706"
KDS_WHITE = "#FFFFFF"
