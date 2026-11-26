# dograc Unified WPF Desktop Migration & Feature Parity Report

> 기존 Python Tkinter Launcher(`launcher/`)와 Next.js Web Frontend(`apps/web/`)를 하나의 WPF 데스크톱 애플리케이션(`apps/desktop/`)으로 통합한 아키텍처, 기능 동등성 검증 및 패키징 가이드입니다.

---

## 1. 솔루션 아키텍처

```text
apps/desktop/
├─ Dograc.sln
├─ src/
│  ├─ Dograc.Core/              # 순수 도메인 모델, DTO, 파서(CitationParser), 서비스 인터페이스
│  ├─ Dograc.Infrastructure/    # FastAPI HTTP 클라이언트, LocalServiceManager, Ollama/Update/Settings 구현
│  └─ Dograc.App/               # WPF UI (KDS Design Tokens, Main Shell, 6 Pages, Context Panel 3 Tabs)
└─ tests/
   └─ Dograc.Core.Tests/        # xUnit 단위·통합 테스트 (DI, API, 소유권, 인용 파싱, 설정, Ollama, Updater, E2E)
```

### 아키텍처 제약 준수 현황
- **FastAPI Backend / REST API / Docker Compose / Ollama 유지**: 기존 `apps/api/` 계약 및 스키마 무변경.
- **DB 직접 접근 금지**: 모든 워크스페이스·문서·대화·Trace 데이터는 `IDogracApiClient`를 통해 REST API로만 통신.
- **MVVM + DI**: `CommunityToolkit.Mvvm` + `Microsoft.Extensions.Hosting` / `DependencyInjection` 기반 구성.
- **레거시 보존**: 기존 `launcher/` 및 `apps/web/`은 최종 운영 전환 승인 전까지 삭제하지 않고 그대로 유지.

---

## 2. 기존 기능 동등성 매트릭스 (Launcher + Web vs WPF)

| 기능 영역 | 기존 구현 (`launcher` / `apps/web`) | 통합 WPF 구현 (`apps/desktop`) | 동등성 / 개선 사항 |
| --- | --- | --- | --- |
| **로컬 서비스 점검 & 자동 기동** | `launcher/services.py` (Docker, Ollama, FastAPI, Next.js) | `LocalServiceManager` + `StartupOrchestrator` (`SystemView`) | Next.js 제거, `External` vs `Managed` 소유권 구분, 앱 시작 시 Shell 즉시 표시 후 백그라운드 기동 |
| **최초 환경 구성 안내** | 로그 오류 확인 | `SetupWizardWindow` | 필수 환경 미충족 시에만 자동 표시 |
| **실시간 시스템 로그** | Tkinter `Text` 로그창 | `SystemView` Monospace 로그 뷰어 | 2,000건 링 버퍼 + 로그 지우기 지원 |
| **워크스페이스 관리** | `Navbar.tsx` 드롭다운 + 생성 모달 | Sidebar 선택기 + `WorkspacesView` | 목록/생성/선택/삭제 완전 동등 |
| **문서 업로드·색인·삭제** | `DocumentPanel.tsx` | Context Panel `Documents` 탭 + `DocumentsView` | 탐색기 드래그앤드롭, 다중 파일 선택, 업로드 후 자동 색인 및 로컬 경로 자동 매핑 |
| **대화 세션 관리** | `ChatPanel.tsx` 상단 가로 탭 | Sidebar 세로 대화 목록 + 실시간 검색 | 검색 필터 및 세로 목록으로 확장 |
| **RAG 질의응답 & Markdown** | `ChatPanel.tsx` + `MarkdownRenderer.tsx` | `ChatView` + `MarkdownFlowControl` | Reading-First KDS 렌더링, 클릭 가능한 `[파일명, p.숫자]` 배지, `CancellationToken` 생성 중지, 재시도/입력 복원 |
| **근거 분석 (Trace)** | `TraceDrawer.tsx` | Context Panel `Evidence` 탭 | 모델명·지연시간·유사도·발췌 원문 + 클릭한 인용 청크 강조 + 프롬프트 토글 |
| **PDF 원문 미리보기** | *(신규)* | Context Panel `Preview` 탭 (`WebView2`) | 인용 페이지(`#page=N`) 이동, 로컬 파일 연결, 외부 앱 열기, TXT/발췌 Fallback |
| **Ollama 모델 관리** | `OllamaGuideModal.tsx` | `ModelsView` (`OllamaService`) | `/api/tags` 자동 모델 감지, 원클릭 활성화(`ChatView` 동기화), 추천 명령어 복사 |
| **테마 & 설정 영속화** | `localStorage` (Light 단일) | `SettingsView` + `JsonSettingsStore` | `System` / `Light` / `Dark` 동적 ResourceDictionary 토큰 전환 + `%LOCALAPPDATA%\dograc\settings.json` 영속화 |
| **업데이트 확인** | `launcher/updater.py` | `GitHubUpdateService` (`SettingsView`) | `minwoo1119/dograc` GitHub Releases 버전 비교 + 폐쇄망 오프라인 안전 처리 |

---

## 3. 개발·테스트·패키징 명령어

```powershell
# 1. 전체 솔루션 빌드 (Release)
dotnet build apps/desktop/Dograc.sln --configuration Release

# 2. 단위 및 통합 테스트 실행
dotnet test apps/desktop/Dograc.sln --configuration Release --no-build

# 3. 로컬 실행
dotnet run --project apps/desktop/src/Dograc.App/Dograc.App.csproj

# 4. Windows 포터블 패키지 생성 (artifacts/wpf/dograc-wpf-win-x64.zip)
powershell -ExecutionPolicy Bypass -File scripts/package_wpf.ps1
```
