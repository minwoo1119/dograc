import argparse
import sys
import time

from launcher.services import ServiceManager


def run_cli_mode() -> None:
    print("=" * 60)
    print("dograc — 폐쇄망 및 로컬 환경을 위한 데스크톱-웹 RAG 런처 (CLI 모드)")
    print("=" * 60)

    manager = ServiceManager()
    print("\n1. 서비스 상태 검사 중...")
    print(f" - {manager.check_docker().name}: {manager.check_docker().message}")
    print(f" - {manager.check_ollama().name}: {manager.check_ollama().message}")
    print(f" - {manager.check_backend().name}: {manager.check_backend().message}")
    print(f" - {manager.check_frontend().name}: {manager.check_frontend().message}")

    print("\n2. 원클릭 서비스 구동 시작...")
    manager.start_docker_infrastructure()
    manager.start_backend()
    manager.start_frontend()

    ready = manager.wait_for_services(timeout_sec=35)
    if ready:
        print("\n모든 서비스가 준비되었습니다. 기본 웹 브라우저를 엽니다.")
        manager.open_browser()
    else:
        print("\n[경고] 서비스 준비 타임아웃. 상태를 수동 확인하세요.")

    print("\n실행을 중단하려면 Ctrl+C를 누르세요.")
    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        print("\n종료 신호 수신. 프로세스를 정리합니다...")
        manager.stop_all()
        print("정리 완료.")


def main() -> None:
    parser = argparse.ArgumentParser(description="dograc Desktop Launcher")
    parser.add_argument("--cli", action="store_true", help="Run in terminal CLI mode instead of GUI")
    args = parser.parse_args()

    if args.cli:
        run_cli_mode()
        return

    try:
        import tkinter
        from launcher.gui import run_gui
        run_gui()
    except (ImportError, Exception) as e:
        print(f"[안내] 데스크톱 GUI 초기화 실패 ({e}). CLI 모드로 전환합니다.")
        run_cli_mode()


if __name__ == "__main__":
    main()
