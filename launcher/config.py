import os
from pathlib import Path

# Repository root path
ROOT_DIR = Path(__file__).resolve().parent.parent

# Apps and infra paths
API_DIR = ROOT_DIR / "apps" / "api"
WEB_DIR = ROOT_DIR / "apps" / "web"
INFRA_DIR = ROOT_DIR / "infra"
COMPOSE_FILE = INFRA_DIR / "compose.yaml"

# Ports and URLs
API_HOST = "127.0.0.1"
API_PORT = 8000
API_URL = f"http://{API_HOST}:{API_PORT}"
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
