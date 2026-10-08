import sys
import threading
import tkinter as tk
from tkinter import messagebox, ttk

from launcher.config import (
    KDS_AMBER_600,
    KDS_BLUE_50,
    KDS_BLUE_600,
    KDS_BLUE_800,
    KDS_GRAY_100,
    KDS_GRAY_300,
    KDS_GRAY_50,
    KDS_GRAY_700,
    KDS_GRAY_900,
    KDS_GREEN_600,
    KDS_RED_500,
    KDS_WHITE,
    WEB_URL,
)
from launcher.services import ServiceManager
from launcher.updater import UpdateService
from launcher.version import __version__


class DogracLauncherApp:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.root.title(f"dograc — 데스크톱 런처 (v{__version__})")
        self.root.geometry("720x620")
        self.root.minsize(660, 540)
        self.root.configure(bg=KDS_GRAY_50)

        self.service_manager = ServiceManager(log_callback=self.append_log)
        self.update_service = UpdateService(current_version=__version__)
        self.status_labels: dict[str, tuple[tk.Label, tk.Label]] = {}

        self._build_ui()
        self.refresh_status_async()

        # Handle window close
        self.root.protocol("WM_DELETE_WINDOW", self.on_close)

    def _build_ui(self) -> None:
        # 1. Header Frame
        header = tk.Frame(self.root, bg=KDS_WHITE, bd=0, highlightthickness=1, highlightbackground=KDS_GRAY_300)
        header.pack(fill="x", padx=16, pady=(16, 8))

        header_inner = tk.Frame(header, bg=KDS_WHITE)
        header_inner.pack(fill="x", padx=20, pady=16)

        left_header = tk.Frame(header_inner, bg=KDS_WHITE)
        left_header.pack(side="left", fill="both", expand=True)

        title = tk.Label(
            left_header,
            text=f"dograc",
            font=("Noto Sans KR", 20, "bold"),
            fg=KDS_BLUE_600,
            bg=KDS_WHITE,
        )
        title.pack(anchor="w")

        subtitle = tk.Label(
            left_header,
            text="폐쇄망 및 온프레미스 환경을 위한 데스크톱-웹 RAG 런처",
            font=("Noto Sans KR", 10),
            fg=KDS_GRAY_700,
            bg=KDS_WHITE,
        )
        subtitle.pack(anchor="w", pady=(2, 0))

        # Right Header: Version Badge & Update Button
        right_header = tk.Frame(header_inner, bg=KDS_WHITE)
        right_header.pack(side="right", anchor="e")

        version_badge = tk.Label(
            right_header,
            text=f"v{__version__}",
            font=("Consolas", 10, "bold"),
            fg=KDS_BLUE_600,
            bg=KDS_BLUE_50,
            padx=8,
            pady=3,
        )
        version_badge.pack(anchor="e", pady=(0, 4))

        self.btn_update = tk.Button(
            right_header,
            text="업데이트 확인",
            command=self.check_update_async,
            font=("Noto Sans KR", 9),
            bg=KDS_WHITE,
            fg=KDS_GRAY_700,
            relief="solid",
            bd=1,
            padx=8,
            pady=2,
            cursor="hand2",
        )
        self.btn_update.pack(anchor="e")

        # 2. Service Status Card
        card = tk.Frame(self.root, bg=KDS_WHITE, bd=0, highlightthickness=1, highlightbackground=KDS_GRAY_300)
        card.pack(fill="x", padx=16, pady=8)

        card_inner = tk.Frame(card, bg=KDS_WHITE)
        card_inner.pack(fill="x", padx=20, pady=16)

        card_title = tk.Label(
            card_inner,
            text="서비스 컴포넌트 상태",
            font=("Noto Sans KR", 12, "bold"),
            fg=KDS_GRAY_900,
            bg=KDS_WHITE,
        )
        card_title.pack(anchor="w", pady=(0, 10))

        services = [
            ("docker", "인프라 스토리지 (Docker - Postgres/Qdrant/MinIO)"),
            ("ollama", "로컬 생성 LLM (Ollama - Qwen2.5:7b)"),
            ("backend", "백엔드 API 서버 (FastAPI - Port 8000)"),
            ("frontend", "프론트엔드 웹 UI (Next.js - Port 3000)"),
        ]

        for key, name in services:
            row = tk.Frame(card_inner, bg=KDS_WHITE)
            row.pack(fill="x", pady=4)

            indicator = tk.Label(
                row,
                text="●",
                font=("Arial", 11),
                fg=KDS_GRAY_300,
                bg=KDS_WHITE,
                width=2,
            )
            indicator.pack(side="left")

            label_name = tk.Label(
                row,
                text=name,
                font=("Noto Sans KR", 10),
                fg=KDS_GRAY_900,
                bg=KDS_WHITE,
                width=42,
                anchor="w",
            )
            label_name.pack(side="left")

            msg_label = tk.Label(
                row,
                text="확인 중...",
                font=("Noto Sans KR", 9),
                fg=KDS_GRAY_700,
                bg=KDS_WHITE,
                anchor="w",
            )
            msg_label.pack(side="left", fill="x", expand=True)

            self.status_labels[key] = (indicator, msg_label)

        # 3. Action Buttons Frame
        btn_frame = tk.Frame(self.root, bg=KDS_GRAY_50)
        btn_frame.pack(fill="x", padx=16, pady=8)

        self.btn_start = tk.Button(
            btn_frame,
            text="원클릭 서비스 시작 및 브라우저 열기",
            command=self.start_all_async,
            font=("Noto Sans KR", 11, "bold"),
            bg=KDS_BLUE_600,
            fg=KDS_WHITE,
            activebackground=KDS_BLUE_800,
            activeforeground=KDS_WHITE,
            relief="flat",
            padx=16,
            pady=8,
            cursor="hand2",
        )
        self.btn_start.pack(side="left", padx=(0, 8))

        self.btn_browser = tk.Button(
            btn_frame,
            text="웹 브라우저 열기",
            command=self.service_manager.open_browser,
            font=("Noto Sans KR", 10),
            bg=KDS_WHITE,
            fg=KDS_BLUE_600,
            activebackground=KDS_BLUE_50,
            relief="solid",
            bd=1,
            padx=12,
            pady=8,
            cursor="hand2",
        )
        self.btn_browser.pack(side="left", padx=(0, 8))

        self.btn_refresh = tk.Button(
            btn_frame,
            text="상태 새로고침",
            command=self.refresh_status_async,
            font=("Noto Sans KR", 10),
            bg=KDS_WHITE,
            fg=KDS_GRAY_700,
            relief="solid",
            bd=1,
            padx=12,
            pady=8,
            cursor="hand2",
        )
        self.btn_refresh.pack(side="left", padx=(0, 8))

        self.btn_stop = tk.Button(
            btn_frame,
            text="전체 중지",
            command=self.stop_all_async,
            font=("Noto Sans KR", 10),
            bg=KDS_WHITE,
            fg=KDS_RED_500,
            relief="solid",
            bd=1,
            padx=12,
            pady=8,
            cursor="hand2",
        )
        self.btn_stop.pack(side="right")

        # 4. Console Log Card
        log_card = tk.Frame(self.root, bg=KDS_WHITE, bd=0, highlightthickness=1, highlightbackground=KDS_GRAY_300)
        log_card.pack(fill="both", expand=True, padx=16, pady=(8, 16))

        log_inner = tk.Frame(log_card, bg=KDS_WHITE)
        log_inner.pack(fill="both", expand=True, padx=16, pady=12)

        log_title = tk.Label(
            log_inner,
            text="실시간 실행 로그",
            font=("Noto Sans KR", 10, "bold"),
            fg=KDS_GRAY_900,
            bg=KDS_WHITE,
        )
        log_title.pack(anchor="w", pady=(0, 6))

        self.log_text = tk.Text(
            log_inner,
            wrap="word",
            bg=KDS_GRAY_100,
            fg=KDS_GRAY_900,
            font=("Consolas", 9),
            bd=0,
            relief="flat",
        )
        self.log_text.pack(fill="both", expand=True)

    def append_log(self, text: str) -> None:
        def _update() -> None:
            self.log_text.insert(tk.END, text + "\n")
            self.log_text.see(tk.END)

        self.root.after(0, _update)

    def update_status_indicator(self, key: str, is_ready: bool, message: str) -> None:
        def _update() -> None:
            if key in self.status_labels:
                ind, lbl = self.status_labels[key]
                ind.config(fg=KDS_GREEN_600 if is_ready else KDS_RED_500)
                lbl.config(text=message)

        self.root.after(0, _update)

    def refresh_status_async(self) -> None:
        def _task() -> None:
            docker_st = self.service_manager.check_docker()
            self.update_status_indicator("docker", docker_st.is_ready, docker_st.message)

            ollama_st = self.service_manager.check_ollama()
            self.update_status_indicator("ollama", ollama_st.is_ready, ollama_st.message)

            backend_st = self.service_manager.check_backend()
            self.update_status_indicator("backend", backend_st.is_ready, backend_st.message)

            frontend_st = self.service_manager.check_frontend()
            self.update_status_indicator("frontend", frontend_st.is_ready, frontend_st.message)

        threading.Thread(target=_task, daemon=True).start()

    def check_update_async(self) -> None:
        self.btn_update.config(state="disabled", text="확인 중...")

        def _task() -> None:
            result = self.update_service.check_for_updates()
            self.append_log(f"[업데이트] {result.message}")

            def _show_result() -> None:
                self.btn_update.config(state="normal", text="업데이트 확인")
                if result.has_update:
                    if messagebox.askyesno(
                        "새 버전 출시 알림",
                        f"새로운 버전 {result.latest_version}이 게시되었습니다!\n\n"
                        f"릴리스 노트 요약:\n{result.release_notes}\n\n"
                        "GitHub 릴리스 다운로드 페이지를 여시겠습니까?",
                    ):
                        self.update_service.open_download_page(result.download_url)
                else:
                    messagebox.showinfo("업데이트 상태", result.message)

            self.root.after(0, _show_result)

        threading.Thread(target=_task, daemon=True).start()

    def start_all_async(self) -> None:
        self.btn_start.config(state="disabled")

        def _task() -> None:
            self.append_log("=== 원클릭 오케스트레이션 시작 ===")
            # 1. Start Docker
            self.service_manager.start_docker_infrastructure()
            # 2. Check Ollama
            ollama_st = self.service_manager.check_ollama()
            if not ollama_st.is_ready:
                self.append_log("[안내] Ollama 서비스가 미응답 상태입니다. 로컬 Ollama를 실행해 주세요.")
            # 3. Start Backend
            self.service_manager.start_backend()
            # 4. Start Frontend
            self.service_manager.start_frontend()

            # 5. Wait for readiness
            ready = self.service_manager.wait_for_services(timeout_sec=35)
            self.refresh_status_async()

            # 6. Open browser
            self.service_manager.open_browser()
            self.append_log("=== 서비스 준비 완료 및 브라우저 오픈 완료 ===")
            self.root.after(0, lambda: self.btn_start.config(state="normal"))

        threading.Thread(target=_task, daemon=True).start()

    def stop_all_async(self) -> None:
        def _task() -> None:
            self.service_manager.stop_all()
            self.refresh_status_async()

        threading.Thread(target=_task, daemon=True).start()

    def on_close(self) -> None:
        if messagebox.askokcancel("종료", "dograc 런처를 종료하시겠습니까? (백그라운드 서비스가 정리됩니다)"):
            self.service_manager.stop_all()
            self.root.destroy()
            sys.exit(0)


def run_gui() -> None:
    root = tk.Tk()
    DogracLauncherApp(root)
    root.mainloop()
