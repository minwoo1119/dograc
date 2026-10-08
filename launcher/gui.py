import sys
import threading
import tkinter as tk
from tkinter import messagebox, ttk

from launcher.config import ICON_ICO, LOGO_40_PNG, WEB_URL
from launcher.services import ServiceManager
from launcher.updater import UpdateService
from launcher.version import __version__


class DogracLauncherApp:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.root.title(f"dograc 런처 (v{__version__})")
        self.root.geometry("660x560")
        self.root.minsize(580, 480)

        # Apply native Windows ttk styling
        self.style = ttk.Style()
        available_themes = self.style.theme_names()
        if "vista" in available_themes:
            self.style.theme_use("vista")
        elif "winnative" in available_themes:
            self.style.theme_use("winnative")

        # Set Window Titlebar and Taskbar Icon
        self.logo_img: tk.PhotoImage | None = None
        if ICON_ICO.exists():
            try:
                self.root.iconbitmap(str(ICON_ICO))
            except Exception:
                pass
        if LOGO_40_PNG.exists():
            try:
                self.logo_img = tk.PhotoImage(file=str(LOGO_40_PNG))
                self.root.iconphoto(True, self.logo_img)
            except Exception:
                self.logo_img = None

        self.service_manager = ServiceManager(log_callback=self.append_log)
        self.update_service = UpdateService(current_version=__version__)
        self.status_labels: dict[str, tuple[ttk.Label, ttk.Label]] = {}

        self._build_menu()
        self._build_ui()
        self.refresh_status_async()

        # Handle window close
        self.root.protocol("WM_DELETE_WINDOW", self.on_close)

    def _build_menu(self) -> None:
        menubar = tk.Menu(self.root)

        # File Menu
        file_menu = tk.Menu(menubar, tearoff=0)
        file_menu.add_command(label="전체 서비스 시작", command=self.start_all_async)
        file_menu.add_command(label="전체 서비스 중지", command=self.stop_all_async)
        file_menu.add_command(label="웹 브라우저 열기", command=self.service_manager.open_browser)
        file_menu.add_separator()
        file_menu.add_command(label="종료", command=self.on_close)
        menubar.add_cascade(label="파일(F)", menu=file_menu)

        # Tools Menu
        tools_menu = tk.Menu(menubar, tearoff=0)
        tools_menu.add_command(label="상태 새로고침", command=self.refresh_status_async)
        tools_menu.add_command(label="업데이트 확인", command=self.check_update_async)
        menubar.add_cascade(label="도구(T)", menu=tools_menu)

        # Help Menu
        help_menu = tk.Menu(menubar, tearoff=0)
        help_menu.add_command(
            label="dograc 정보",
            command=lambda: messagebox.showinfo(
                "dograc 정보",
                f"dograc 데스크톱-웹 런처\n\n버전: v{__version__}\n저장소: minwoo1119/dograc\n\n로컬 오픈소스 RAG 시스템 관리 도구입니다.",
            ),
        )
        menubar.add_cascade(label="도움말(H)", menu=help_menu)

        self.root.config(menu=menubar)

    def _build_ui(self) -> None:
        main_frame = ttk.Frame(self.root, padding=12)
        main_frame.pack(fill="both", expand=True)

        # 0. App Logo & Title Header
        header_frame = ttk.Frame(main_frame, padding=(2, 0, 2, 8))
        header_frame.pack(fill="x")

        if self.logo_img:
            logo_lbl = ttk.Label(header_frame, image=self.logo_img)
            logo_lbl.pack(side="left", padx=(0, 10))

        title_box = ttk.Frame(header_frame)
        title_box.pack(side="left", fill="y")

        app_title = ttk.Label(title_box, text=f"dograc  v{__version__}", font=("맑은 고딕", 12, "bold"))
        app_title.pack(anchor="w")

        app_desc = ttk.Label(
            title_box,
            text="사내 기밀 보호를 위한 로컬 오픈소스 RAG 서비스 관리자",
            font=("맑은 고딕", 8),
            foreground="#555555",
        )
        app_desc.pack(anchor="w", pady=(1, 0))

        header_sep = ttk.Separator(main_frame, orient="horizontal")
        header_sep.pack(fill="x", pady=(0, 10))

        # 1. Service Status Group (LabelFrame)
        status_group = ttk.LabelFrame(main_frame, text=" 서비스 컴포넌트 상태 ", padding=10)
        status_group.pack(fill="x", pady=(0, 8))

        services = [
            ("docker", "데이터베이스 및 인프라 (Docker: Postgres / Qdrant / MinIO)"),
            ("ollama", "로컬 LLM 서비스 (Ollama: Qwen2.5)"),
            ("backend", "RAG 백엔드 서버 (FastAPI: http://127.0.0.1:8000)"),
            ("frontend", "웹 사용자 인터페이스 (Next.js: http://127.0.0.1:3000)"),
        ]

        for row, (key, label_text) in enumerate(services):
            name_lbl = ttk.Label(status_group, text=label_text, font=("맑은 고딕", 9))
            name_lbl.grid(row=row, column=0, sticky="w", pady=3)

            sep_lbl = ttk.Label(status_group, text=":", font=("맑은 고딕", 9))
            sep_lbl.grid(row=row, column=1, padx=6, pady=3)

            val_lbl = ttk.Label(status_group, text="확인 중...", font=("맑은 고딕", 9))
            val_lbl.grid(row=row, column=2, sticky="w", pady=3)

            self.status_labels[key] = (name_lbl, val_lbl)

        status_group.columnconfigure(0, weight=1)

        # 2. Control Buttons Area
        btn_frame = ttk.Frame(main_frame, padding=(0, 4))
        btn_frame.pack(fill="x", pady=(0, 8))

        self.btn_start = ttk.Button(btn_frame, text="전체 서비스 시작", command=self.start_all_async)
        self.btn_start.pack(side="left", padx=(0, 6))

        self.btn_browser = ttk.Button(btn_frame, text="브라우저 열기", command=self.service_manager.open_browser)
        self.btn_browser.pack(side="left", padx=(0, 6))

        self.btn_refresh = ttk.Button(btn_frame, text="상태 새로고침", command=self.refresh_status_async)
        self.btn_refresh.pack(side="left", padx=(0, 6))

        self.btn_update = ttk.Button(btn_frame, text="업데이트 확인", command=self.check_update_async)
        self.btn_update.pack(side="left", padx=(0, 6))

        self.btn_stop = ttk.Button(btn_frame, text="전체 중지", command=self.stop_all_async)
        self.btn_stop.pack(side="right")

        # 3. Log Output Group (LabelFrame)
        log_group = ttk.LabelFrame(main_frame, text=" 실행 로그 ", padding=6)
        log_group.pack(fill="both", expand=True, pady=(0, 4))

        log_container = ttk.Frame(log_group)
        log_container.pack(fill="both", expand=True)

        scrollbar = ttk.Scrollbar(log_container, orient="vertical")
        self.log_text = tk.Text(
            log_container,
            wrap="word",
            bg="#FFFFFF",
            fg="#222222",
            font=("Consolas", 9),
            bd=1,
            relief="sunken",
            yscrollcommand=scrollbar.set,
        )
        scrollbar.config(command=self.log_text.yview)

        scrollbar.pack(side="right", fill="y")
        self.log_text.pack(side="left", fill="both", expand=True)

        # 4. Status Bar
        self.status_bar = ttk.Frame(self.root, padding=(8, 3))
        self.status_bar.pack(fill="x", side="bottom")

        sep = ttk.Separator(self.root, orient="horizontal")
        sep.pack(fill="x", side="bottom")

        self.status_msg = ttk.Label(self.status_bar, text="준비 완료", font=("맑은 고딕", 9))
        self.status_msg.pack(side="left")

        ver_label = ttk.Label(self.status_bar, text=f"버전: v{__version__}", font=("맑은 고딕", 9))
        ver_label.pack(side="right")

    def append_log(self, text: str) -> None:
        def _update() -> None:
            self.log_text.insert(tk.END, text + "\n")
            self.log_text.see(tk.END)

        self.root.after(0, _update)

    def set_status_text(self, text: str) -> None:
        self.root.after(0, lambda: self.status_msg.config(text=text))

    def update_status_indicator(self, key: str, is_ready: bool, message: str) -> None:
        def _update() -> None:
            if key in self.status_labels:
                _, val_lbl = self.status_labels[key]
                prefix = "● " if is_ready else "○ "
                val_lbl.config(text=f"{prefix}{message}")

        self.root.after(0, _update)

    def refresh_status_async(self) -> None:
        self.set_status_text("서비스 상태 확인 중...")

        def _task() -> None:
            docker_st = self.service_manager.check_docker()
            self.update_status_indicator("docker", docker_st.is_ready, docker_st.message)

            ollama_st = self.service_manager.check_ollama()
            self.update_status_indicator("ollama", ollama_st.is_ready, ollama_st.message)

            backend_st = self.service_manager.check_backend()
            self.update_status_indicator("backend", backend_st.is_ready, backend_st.message)

            frontend_st = self.service_manager.check_frontend()
            self.update_status_indicator("frontend", frontend_st.is_ready, frontend_st.message)

            self.set_status_text("상태 새로고침 완료")

        threading.Thread(target=_task, daemon=True).start()

    def check_update_async(self) -> None:
        self.btn_update.config(state="disabled")
        self.set_status_text("최신 버전 확인 중...")

        def _task() -> None:
            result = self.update_service.check_for_updates()
            self.append_log(f"[업데이트] {result.message}")

            def _show_result() -> None:
                self.btn_update.config(state="normal")
                self.set_status_text("최신 상태" if not result.has_update else "새 버전 발견")
                if result.has_update:
                    if messagebox.askyesno(
                        "새 버전 출시 알림",
                        f"새로운 버전 {result.latest_version}이 출시되었습니다.\n\n"
                        f"릴리스 내용:\n{result.release_notes}\n\n"
                        "업데이트를 지금 다운로드하고 설치하시겠습니까?\n"
                        "(다운로드 후 앱이 자동으로 재시작됩니다)",
                    ):
                        self._apply_update_async(result)
                else:
                    messagebox.showinfo("업데이트 확인", result.message)

            self.root.after(0, _show_result)

        threading.Thread(target=_task, daemon=True).start()

    def _apply_update_async(self, result) -> None:
        self.btn_update.config(state="disabled")
        self.set_status_text("새 버전 다운로드 중...")
        self.append_log(f"[업데이트] {result.latest_version} 다운로드 시작...")

        def _update_task() -> None:
            def _progress(pct: float) -> None:
                pct_str = f"{int(pct * 100)}%"
                self.set_status_text(f"업데이트 다운로드 중: {pct_str}")

            try:
                self.update_service.download_and_restart(result, on_progress=_progress)
                self.set_status_text("다운로드 완료. 재시작 중...")
            except Exception as e:
                self.append_log(f"[업데이트 오류] 다운로드 실패: {e}")
                self.set_status_text("업데이트 실패")
                self.root.after(
                    0,
                    lambda: (
                        self.btn_update.config(state="normal"),
                        messagebox.showerror("업데이트 오류", f"업데이트 중 오류가 발생했습니다: {e}\n\nGitHub 다운로드 페이지를 엽니다."),
                        self.update_service.open_download_page(result.download_url),
                    ),
                )

        threading.Thread(target=_update_task, daemon=True).start()

    def start_all_async(self) -> None:
        self.btn_start.config(state="disabled")
        self.set_status_text("서비스 시작 중...")

        def _task() -> None:
            self.append_log("=== 전체 서비스 가동 시작 ===")
            self.service_manager.start_docker_infrastructure()

            ollama_st = self.service_manager.check_ollama()
            if not ollama_st.is_ready:
                self.append_log("[안내] Ollama 서비스가 미응답 상태입니다. 로컬 Ollama를 실행해 주세요.")

            self.service_manager.start_backend()
            self.service_manager.start_frontend()

            ready = self.service_manager.wait_for_services(timeout_sec=35)
            self.refresh_status_async()

            if ready:
                self.service_manager.open_browser()
                self.append_log("=== 서비스 준비 완료 및 브라우저 열림 ===")
                self.set_status_text("전체 서비스 가동 완료 (브라우저 열림)")
            else:
                self.append_log("=== 서비스 준비 실패 (로그창 오류 확인 필요) ===")
                self.set_status_text("서비스 준비 실패")
            self.root.after(0, lambda: self.btn_start.config(state="normal"))

        threading.Thread(target=_task, daemon=True).start()

    def stop_all_async(self) -> None:
        self.set_status_text("전체 서비스 중지 중...")

        def _task() -> None:
            self.service_manager.stop_all()
            self.refresh_status_async()
            self.set_status_text("전체 서비스 중지 완료")

        threading.Thread(target=_task, daemon=True).start()

    def on_close(self) -> None:
        if messagebox.askokcancel("종료", "dograc 런처를 종료하시겠습니까?\n실행 중인 서비스 프로세스가 정리됩니다."):
            self.service_manager.stop_all()
            self.root.destroy()
            sys.exit(0)


def run_gui() -> None:
    root = tk.Tk()
    DogracLauncherApp(root)
    root.mainloop()
