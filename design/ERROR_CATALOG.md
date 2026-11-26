# dograc Error Catalog

> `dograc` Desktop(WPF) 및 Backend(FastAPI) 전 영역의 표준 오류 코드, 심각도, 원인 구분(확인됨/추정), 복구 액션, 재시도 정책 및 테스트 시나리오를 정의합니다.

---

## 심각도 및 UI 매핑 정책

| Severity | UI 표시 방식 | 용도 |
| :--- | :--- | :--- |
| `Information` | Toast / Inline Notice | 일반 안내, 검색 결과 없음, 이미 완료된 작업 |
| `Warning` | Inline Banner / Warning Indicator | 일부 서비스 미연결, 색인 미완료 문서, 복구 가능한 제한 |
| `Error` | Inline Error / ErrorCard + RecoveryActionButton | 요청 실패, 업로드/색인 실패, 모델 조회/답변 생성 실패 |
| `Critical` | ErrorDialog / Recovery Screen (Setup Wizard) | 필수 런타임 누락, 설정/데이터 손상 위험, 실행 불가 |

---

## A. Application Startup (`ENV-*`, `SET-*`)

| Error Code | Component | Trigger Condition | Severity | User Message | Possible Cause | Impact | Recovery Action | Retry Policy | Log Context | Test Scenario |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `ENV-001` | Startup | Python 가상환경(`.venv`), `uv`, `python` 모두 미발견 | Critical | 필수 백엔드 런타임(Python/uv)을 찾을 수 없습니다. | Python 3.12+ 또는 `uv` 미설치, 또는 `apps/api/.venv` 누락(확인됨) | FastAPI 백엔드 기동 불가 | Setup Wizard 열기, 공식 설치 가이드, 다시 검사 | 수동 재시도 | `project_root`, `checked_paths` | Python/uv 없는 환경에서 시작 점검 |
| `ENV-002` | Startup | `apps/api` 디렉터리 또는 진입 모듈 누락 | Critical | 백엔드 소스 경로를 찾을 수 없습니다. | 잘못된 프로젝트 루트 또는 배포 파일 손상(확인됨) | 백엔드 서버 실행 불가 | Setup Wizard 열기, 진단 보고서 내보내기 | 수동 재시도 | `project_root`, `api_dir` | 존재하지 않는 프로젝트 루트 지정 |
| `ENV-003` | Startup | `.env` 및 `.env.example` 모두 누락 또는 읽기 실패 | Warning | 환경 설정 파일(.env)을 구성하지 못했습니다. | 저장소 파일 누락 또는 디렉터리 권한 부족(추정) | 기본 환경 변수로 동작하거나 DB 연결 실패 가능 | Setup Wizard 열기, 다시 시도 | 1회 수동 재시도 | `env_path`, `io_error` | `.env` 접근 거부 시뮬레이션 |
| `ENV-004` | Startup | 동일 사용자 세션에서 앱 중복 실행 감지 (`Mutex`) | Information | dograc이 이미 실행 중입니다. | 앱이 이미 백그라운드 또는 다른 창에서 실행됨(확인됨) | 중복 프로세스 실행 차단 | 기존 창 확인 안내 | 재시도 없음 | `mutex_name` | Mutex 선점 상태에서 중복 실행 |
| `ENV-005` | Startup | 앱 데이터/로그/다운로드 경로 디스크 여유 공간 부족 | Critical | 디스크 저장 공간이 부족합니다. | 로컬 드라이브 여유 용량 임계치 미만(확인됨) | 문서 업로드, 로그 기록, 업데이트 불가 | 진단 정보 복사 | 수동 재시도 | `drive`, `free_bytes` | 디스크 공간 부족 예외 매핑 |
| `ENV-006` | Startup | 파일/폴더 쓰기 권한 부족 (`UnauthorizedAccessException`) | Error | 필요한 파일 또는 폴더에 접근할 권한이 없습니다. | Windows 폴더 권한 제한 또는 보안 프로그램 차단(추정) | 설정 저장, 로그 파일 기록, 파일 업로드 실패 | 다시 시도, 진단 정보 복사 | 수동 재시도 | `path`, `operation` | 읽기 전용 디렉터리 쓰기 시도 |
| `SET-001` | Settings | `settings.json` JSON 파싱 실패(손상) | Warning | 설정 파일이 손상되어 기본 설정으로 복원했습니다. | 비정상 종료 중 파일 기록 중단 또는 수동 편집 오류(확인됨) | 사용자 테마/모델 설정 초기화(손상본 `.corrupt.bak` 백업 보존) | 설정 페이지 열기, 진단 정보 복사 | 자동 백업 후 복원 | `settings_path`, `backup_path` | 깨진 JSON 설정 파일 로드 테스트 |
| `SET-002` | Settings | `settings.json` 저장 실패(권한/잠금) | Warning | 설정을 파일에 저장하지 못했습니다. | 다른 프로세스의 파일 잠금 또는 쓰기 권한 부족(추정) | 현재 세션은 유지되나 재시작 시 설정이 유지되지 않을 수 있음 | 다시 시도, 진단 정보 복사 | 1회 수동 재시도 | `settings_path`, `io_error` | 쓰기 불가 경로에 설정 저장 테스트 |

---

## B. Docker & Infrastructure (`DKR-*`)

| Error Code | Component | Trigger Condition | Severity | User Message | Possible Cause | Impact | Recovery Action | Retry Policy | Log Context | Test Scenario |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `DKR-001` | Docker | Docker 실행 파일은 존재하나 `docker info` 실패 | Warning | Docker 데몬에 연결할 수 없습니다. | Docker Desktop이 실행 중이지 않거나 WSL2 엔진이 중지됨(확인됨) | PostgreSQL/Qdrant/MinIO 컨테이너 자동 기동 불가(로컬 폴백 사용 가능) | Docker Desktop 실행, 다시 확인, Setup Wizard 열기 | 수동 재시도 | `exit_code`, `stderr` | Docker 종료 상태에서 앱 실행 |
| `DKR-002` | Docker | `docker compose up -d` 비정상 종료 | Error | 인프라 컨테이너(Docker Compose) 시작에 실패했습니다. | 포트 충돌(5432/6333/9000), 이미지 다운로드 실패, 볼륨 권한 오류(추정) | 영속 DB/벡터/스토리지 컨테이너 미기동 | 다시 시도, System 페이지 열기, 로그 복사 | 최대 1회 수동 재시도 | `compose_file`, `exit_code`, `stderr` | Compose 실패 종료 코드 주입 |
| `DKR-003` | Docker | `docker` 실행 파일 미설치 | Warning | Docker Desktop이 설치되어 있지 않습니다. | Docker Desktop 미설치 또는 PATH 미등록(확인됨) | 로컬 Docker 인프라 컨테이너 관리 불가 | 공식 설치 가이드, Setup Wizard 열기 | 재시도 없음 | `checked_cmd` | Docker 미설치 환경 점검 |
| `DKR-004` | Docker | Docker Engine 응답 시간 초과 | Warning | Docker 응답 대기 시간이 초과되었습니다. | Docker Desktop 초기화 중이거나 WSL2 응답 지연(추정) | 인프라 상태 확인 및 기동 지연 | 다시 확인, System 페이지 열기 | Timeout 5s / Up 45s | `timeout_seconds` | Docker 명령 타임아웃 주입 |
| `DKR-005` | Docker | PostgreSQL / Qdrant / MinIO 중 일부 포트 미응답 | Warning | 일부 인프라 서비스가 준비되지 않았습니다. | 개별 컨테이너가 시작 중이거나 비정상 종료됨(확인됨) | 색인/검색/저장이 제한되거나 로컬 폴백 모드로 동작 | 서비스 시작, System 페이지 열기 | 수동 재시도 | `postgres_ok`, `qdrant_ok`, `minio_ok` | Qdrant 포트 차단 상태 점검 |

---

## C. Ollama & AI Models (`LLM-*`)

| Error Code | Component | Trigger Condition | Severity | User Message | Possible Cause | Impact | Recovery Action | Retry Policy | Log Context | Test Scenario |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `LLM-001` | Ollama | Ollama 엔드포인트 연결 실패 (`HttpRequestException` / 포트 닫힘) | Error | 로컬 Ollama 서버에 연결할 수 없습니다. | Ollama 앱이 실행되지 않았거나 지정된 포트가 다름(추정) | 모델 목록 조회 및 AI 답변 생성 불가 | Ollama 실행, 연결 설정 열기, 다시 시도 | 자동 1회 백오프 후 수동 | `endpoint_url`, `error_type` | Ollama 미실행 / 종료 상태 |
| `LLM-002` | Ollama | 선택된 모델이 설치 목록에 없거나 HTTP 404 반환 | Error | 선택한 AI 모델이 설치되어 있지 않습니다. | 해당 모델이 로컬 Ollama에 다운로드되지 않음(확인됨) | 현재 선택된 모델로 답변 생성 불가 | 모델 다운로드 명령 복사, Models 페이지 열기 | 재시도 없음 | `model_name`, `installed_count` | 존재하지 않는 모델 선택 |
| `LLM-003` | Ollama | 모델 추론 중 OOM / VRAM 부족 오류 메시지 감지 | Error | 메모리(RAM/VRAM)가 부족해 모델을 실행할 수 없습니다. | 시스템 메모리 또는 GPU VRAM이 선택한 모델 요구량보다 부족함(추정) | 해당 모델로 답변 생성 실패 | Models 페이지 열기(경량 모델 선택), 진단 정보 복사 | 재시도 없음 | `model_name`, `raw_error` | VRAM 부족 응답 매핑 |
| `LLM-004` | Ollama | 잘못된 Ollama Endpoint URL 형식 (`Uri.TryCreate` 실패) | Error | Ollama 연결 주소(URL) 형식이 올바르지 않습니다. | `http://` 누락 또는 유효하지 않은 호스트/포트 입력(확인됨) | Ollama 연결 및 모델 조회 불가 | 연결 설정 열기 | 재시도 없음 | `endpoint_url` | 잘못된 Ollama Endpoint 입력 |
| `LLM-005` | Ollama | Ollama 요청 시간 초과 (`TimeoutException` / `TaskCanceledException`) | Error | Ollama 모델 응답 대기 시간이 초과되었습니다. | 모델 첫 로딩 지연 또는 과도한 문맥 길이(추정) | 모델 목록 조회 또는 답변 생성 중단 | 다시 시도, Models 페이지 열기 | 목록 5s / 생성 120s | `timeout_seconds`, `model_name` | Ollama 타임아웃 주입 |
| `LLM-006` | Ollama | Ollama 미설치 (`ollama` 실행 파일 미발견 + 포트 미응답) | Warning | Ollama가 설치되어 있지 않습니다. | 로컬 단말기에 Ollama 미설치(확인됨) | 로컬 LLM 추론 불가 | 공식 설치 가이드, Setup Wizard 열기 | 재시도 없음 | `endpoint_url` | Ollama 미설치 환경 테스트 |
| `LLM-007` | Ollama | Context Length 초과 또는 모델 로딩 실패 | Error | 입력 문맥이 모델 허용 길이를 초과했거나 모델 로딩에 실패했습니다. | 질문·참고 청크 길이 초과 또는 모델 파일 손상(추정) | 현재 질문 답변 생성 실패 | 다시 시도, Models 페이지 열기 | 수동 재시도 | `model_name`, `status_code` | Context length 오류 응답 매핑 |

---

## D. FastAPI & Backend (`API-*`)

| Error Code | Component | Trigger Condition | Severity | User Message | Possible Cause | Impact | Recovery Action | Retry Policy | Log Context | Test Scenario |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `API-001` | Backend | FastAPI 엔드포인트 연결 거부 / 네트워크 실패 | Error | 로컬 백엔드(FastAPI) 서버에 연결할 수 없습니다. | 백엔드 서버가 실행 중이 아니거나 방금 종료됨(확인됨) | 워크스페이스·문서·대화 모든 API 기능 불가 | 서비스 시작, System 페이지 열기, 다시 시도 | GET 최대 2회(지수 백오프) | `url`, `method`, `correlation_id` | 백엔드 강제 종료 후 요청 |
| `API-002` | Backend | API 포트(8000) 충돌로 백엔드 프로세스 시작 실패 | Critical | 백엔드 포트(8000)가 이미 다른 프로그램에서 사용 중입니다. | 다른 프로세스가 8000 포트를 점유하고 있음(추정) | FastAPI 백엔드 기동 실패 | System 페이지 열기, Setup Wizard 열기, 로그 복사 | 수동 재시도 | `port`, `exit_code`, `stderr` | 포트 충돌 오류 감지 테스트 |
| `API-003` | Backend | 백엔드 기동 후 헬스체크 대기 시간 초과 | Error | 백엔드 서버 준비 대기 시간이 초과되었습니다. | 임베딩 모델 초기 로딩 지연 또는 DB 연결 대기(추정) | API 기능 일시 사용 불가 | 다시 확인, System 페이지 열기 | 35s 대기 후 수동 재시도 | `timeout_seconds`, `api_url` | 기동 타임아웃 테스트 |
| `API-004` | Backend | HTTP 요청 시간 초과 | Error | 백엔드 응답 대기 시간이 초과되었습니다. | 대용량 색인 또는 로컬 LLM 생성 시간이 제한을 초과함(추정) | 요청한 작업 미완료 | 다시 시도, System 페이지 열기 | 수동 재시도 | `url`, `timeout_seconds`, `correlation_id` | API Timeout 주입 테스트 |
| `API-005` | Backend | JSON 역직렬화 실패 또는 예상하지 못한 응답 구조 | Error | 백엔드 응답 형식을 해석할 수 없습니다. | 백엔드와 데스크톱 클라이언트 버전 불일치 또는 프록시 간섭(추정) | 해당 화면 데이터 로드 실패 | 다시 시도, 진단 정보 복사 | 재시도 없음 | `url`, `status_code`, `correlation_id` | 잘못된 API JSON 응답 주입 |
| `API-006` | Backend | HTTP 400 / 422 요청 데이터 검증 실패 | Error | 요청 입력값이 올바르지 않습니다. | 필수 입력 누락 또는 형식 오류(확인됨) | 요청 거부됨 | 입력 확인 후 다시 시도 | 재시도 없음 | `status_code`, `backend_code`, `correlation_id` | 422 검증 오류 응답 매핑 |
| `API-007` | Backend | HTTP 401 / 403 인증·권한 오류 | Error | 요청을 실행할 권한이 없습니다. | 사용자 식별자 또는 접근 권한 불일치(확인됨) | 해당 리소스 접근 차단 | 다시 시도, System 페이지 열기 | 재시도 없음 | `status_code`, `correlation_id` | 401/403 응답 매핑 |
| `API-008` | Backend | HTTP 404 리소스 없음 | Error | 요청한 항목을 찾을 수 없습니다. | 이미 삭제된 워크스페이스·문서·대화·Trace(확인됨) | 해당 항목 조회·수정 불가 | 목록 새로고침 | 재시도 없음 | `url`, `status_code`, `correlation_id` | 삭제된 리소스 404 처리 |
| `API-009` | Backend | HTTP 409 충돌 / HTTP 413 파일 크기 초과 / HTTP 429 요청 제한 | Error | 요청 충돌 또는 크기·호출 제한을 초과했습니다. | 중복 리소스, 파일 용량 초과 또는 일시적 과부하(확인됨) | 요청 거부됨 | 조건 조정 후 다시 시도 | 429만 백오프 허용 | `status_code`, `backend_code` | 409/413/429 매핑 테스트 |
| `API-010` | Backend | HTTP 500 / 502 / 503 / 504 서버 내부·게이트웨이 오류 | Error | 백엔드 서버에서 내부 오류가 발생했습니다. | 백엔드 의존 서비스(DB/Qdrant/Ollama) 장애 또는 예외(추정) | 요청 처리 실패 | 다시 시도, System 페이지 열기, 진단 정보 복사 | 수동 재시도 | `status_code`, `backend_code`, `correlation_id` | 500/503 응답 매핑 테스트 |
| `API-011` | Backend | `/health/live`는 응답하나 `/health/ready` 실패 | Warning | 백엔드는 실행 중이나 일부 하위 저장소(DB/Qdrant/Storage)가 준비되지 않았습니다. | PostgreSQL/Qdrant/MinIO 중 하나 이상이 비정상 상태(확인됨) | 문서 색인 또는 검색이 실패할 수 있음 | 서비스 시작, System 페이지 열기 | 수동 재시도 | `live_ok`, `ready_ok` | `/health/ready` 실패 Degraded 테스트 |

---

## E. Documents & Indexing (`DOC-*`, `IDX-*`)

| Error Code | Component | Trigger Condition | Severity | User Message | Possible Cause | Impact | Recovery Action | Retry Policy | Log Context | Test Scenario |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `DOC-001` | Documents | `.pdf`, `.txt` 외 파일 업로드 시도 | Error | 지원하지 않는 파일 형식입니다. | 현재 지원 포맷(PDF, TXT)이 아닌 파일 선택(확인됨) | 업로드 차단 | 파일 다시 선택 | 재시도 없음 | `extension` | 미지원 확장자(`.docx`) 업로드 |
| `DOC-002` | Documents | 업로드 전 파일 삭제/이동 또는 접근 권한 없음 | Error | 선택한 파일을 열 수 없습니다. | 파일이 이동·삭제되었거나 다른 프로그램에서 잠금(확인됨) | 업로드 실패 | 파일 다시 선택, 다시 시도 | 수동 재시도 | `file_name`, `io_error` | 존재하지 않는 파일 업로드 |
| `DOC-003` | Documents | 빈 파일(0 byte) 또는 파일 크기 제한 초과 | Error | 빈 파일이거나 최대 허용 크기를 초과했습니다. | 내용이 없는 파일이거나 업로드 제한 용량 초과(확인됨) | 업로드 차단 | 파일 다시 선택 | 재시도 없음 | `file_name`, `size_bytes` | 빈 파일 업로드 검증 |
| `DOC-004` | Documents | 암호화된 PDF 감지 | Error | 암호로 보호된 PDF는 처리할 수 없습니다. | 비밀번호가 설정된 PDF 문서(확인됨) | 텍스트 추출 및 색인 불가 | 암호 해제 후 파일 다시 선택 | 재시도 없음 | `stage=text_extraction` | 암호화된 PDF 업로드 테스트 |
| `DOC-005` | Documents | 손상된 PDF 또는 스캔 이미지 전용 PDF(추출 텍스트 없음) | Error | PDF에서 텍스트를 추출할 수 없습니다. | 파일 손상 또는 OCR이 필요한 스캔 이미지 PDF(확인됨) | 문서 색인 실패(`텍스트 추출` 단계) | 파일 다시 선택, 진단 정보 복사 | 재시도 없음 | `stage=text_extraction`, `failure_code` | 손상된 PDF 업로드 테스트 |
| `DOC-006` | Documents | 문서 업로드 중 네트워크 연결 해제 | Error | 파일 업로드 중 서버 연결이 끊어졌습니다. | 업로드 도중 백엔드 종료 또는 네트워크 단절(추정) | 파일이 저장되지 않음 | 다시 시도, System 페이지 열기 | 수동 재시도 | `stage=upload`, `file_name` | 업로드 중 연결 해제 테스트 |
| `DOC-007` | Preview | PDF WebView2 렌더링 실패 또는 로컬 원본 미연결 | Warning | 내장 PDF 뷰어로 원문을 표시할 수 없습니다. | WebView2 런타임 오류 또는 로컬 파일 경로 이동(추정) | 인라인 PDF 미리보기 불가(발췌문 대체 표시) | 로컬 파일 연결, 외부 앱으로 열기 | 수동 재시도 | `file_name`, `page` | WebView2 오류 폴백 테스트 |
| `IDX-001` | Indexing | `storage_read` / `text_extraction` 단계 실패 | Error | 문서 색인 실패 [단계: 텍스트 추출] | 원본 파일 손상, 미지원 인코딩 또는 스토리지 읽기 실패(확인됨) | 해당 문서 검색 대상 제외 | 파일 다시 선택, 색인 다시 시작 | 수동 재시도 | `stage=text_extraction`, `document_id` | 손상 PDF 처리 단계 표시 |
| `IDX-002` | Indexing | `chunking` 단계 실패(청크 생성 0건) | Error | 문서 색인 실패 [단계: Chunking] | 추출 가능한 텍스트 본문이 비어 있음(확인됨) | 검색 청크 미생성 | 파일 다시 선택 | 재시도 없음 | `stage=chunking`, `document_id` | 공백 전용 TXT 색인 테스트 |
| `IDX-003` | Indexing | `embedding` 단계 실패 (`EmbeddingModelError`) | Error | 문서 색인 실패 [단계: Embedding] | 임베딩 모델 로딩 실패, 차원 불일치 또는 메모리 부족(추정) | 벡터 생성 중단 | 색인 다시 시작, System 페이지 열기 | 수동 재시도 | `stage=embedding`, `document_id` | 임베딩 실패 주입 테스트 |
| `IDX-004` | Indexing | `vector_indexing` 단계 실패 (`VectorStoreError`) | Error | 문서 색인 실패 [단계: Vector Indexing] | Qdrant 연결 중단 또는 컬렉션 저장 오류(확인됨) | 벡터 DB 색인 미반영(기존 부분 데이터 보상 삭제) | 서비스 시작, 색인 다시 시작 | 수동 재시도 | `stage=vector_indexing`, `document_id` | 색인 중 Qdrant 중단 테스트 |
| `IDX-005` | Indexing | `db_commit` 단계 실패 (`SQLAlchemyError`) | Error | 문서 색인 실패 [단계: DB 저장] | 데이터베이스 연결 중단 또는 트랜잭션 실패(확인됨) | 색인 상태 롤백 | 서비스 시작, 색인 다시 시작 | 수동 재시도 | `stage=db_commit`, `document_id` | DB 오류 롤백 테스트 |

---

## F. RAG & Chat (`RAG-*`, `CHAT-*`)

| Error Code | Component | Trigger Condition | Severity | User Message | Possible Cause | Impact | Recovery Action | Retry Policy | Log Context | Test Scenario |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `RAG-001` | RAG | 검색 단계(`Retrieval`) 실패 | Error | 관련 문서 근거를 검색하는 중 오류가 발생했습니다. | Qdrant 또는 임베딩 모델 연결 실패(추정) | 답변 생성 중단(입력 질문 보존) | 다시 시도, System 페이지 열기 | 수동 재시도 | `conversation_id`, `correlation_id` | 검색 실패 주입 테스트 |
| `RAG-002` | RAG | 색인 완료(`ready`) 문서 0건 상태에서 질문 | Warning | 현재 워크스페이스에 색인 완료된 문서가 없습니다. | 문서가 아직 업로드/색인되지 않았거나 처리 중(확인됨) | 문서 근거 없는 답변만 가능 | Documents 열기, 파일 추가 | 재시도 없음 | `workspace_id`, `doc_count` | 빈 워크스페이스 안내 확인 |
| `RAG-003` | RAG | 검색은 정상 수행되었으나 관련 청크 0건 | Information | 질문과 일치하는 문서 근거를 찾지 못했습니다. | 업로드된 문서에 관련 내용이 없거나 검색어 표현 차이(확인됨) | 근거 청크 0개(근거 있는 것처럼 표시하지 않음) | 질문 표현 변경 또는 문서 추가 | 재시도 없음 | `workspace_id`, `hit_count=0` | 검색 결과 0건 구분 확인 |
| `CHAT-001` | Chat | 답변 생성 실패 (`GenerationFailedError` / Ollama 중단) | Error | AI 답변 생성에 실패했습니다. | 생성 중 Ollama 연결 해제, 모델 오류 또는 타임아웃(추정) | 답변 미생성(질문 내용 자동 보존) | 다시 시도, 입력창 복원, 연결 설정 열기 | 수동 재시도 | `model_name`, `correlation_id` | 답변 생성 중 Ollama 종료 테스트 |
| `CHAT-002` | Chat | 사용자에 의한 답변 생성 취소 (`OperationCanceledException`) | Information | 답변 생성을 중지했습니다. | 사용자가 중지 버튼으로 요청 취소(확인됨) | 부분 생성 중단(질문 내용 보존) | 다시 시도, 입력창 복원 | 수동 재시도 | `conversation_id` | 생성 중 사용자 취소 테스트 |
| `CHAT-003` | Chat | Trace / Evidence 조회 실패 | Error | 답변 근거(Evidence) 데이터를 불러오지 못했습니다. | Trace 누락 또는 백엔드 연결 일시 오류(추정) | 우측 Evidence 탭 표시 불가 | 다시 시도, System 페이지 열기 | 수동 재시도 | `trace_id`, `correlation_id` | Trace 404/오류 주입 테스트 |

---

## G. Workspaces & Data Management (`WSP-*`)

| Error Code | Component | Trigger Condition | Severity | User Message | Possible Cause | Impact | Recovery Action | Retry Policy | Log Context | Test Scenario |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `WSP-001` | Workspaces | 빈 이름 또는 중복 워크스페이스 이름 입력 | Error | 워크스페이스 이름이 비어 있거나 이미 사용 중입니다. | 동일 이름의 워크스페이스가 이미 존재함(확인됨) | 워크스페이스 생성 불가 | 다른 이름으로 다시 입력 | 재시도 없음 | `workspace_name` | 중복/빈 이름 검증 테스트 |
| `WSP-002` | Workspaces | 워크스페이스 생성·전환·삭제 API 실패 | Error | 워크스페이스 작업을 완료하지 못했습니다. | 백엔드/DB 연결 오류 또는 이미 삭제된 워크스페이스(추정) | 변경 사항 미반영 | 다시 시도, 목록 새로고침 | 수동 재시도 | `workspace_id`, `operation` | 워크스페이스 API 오류 테스트 |

---

## H. Updates & Installation (`UPD-*`) & Global (`APP-*`)

| Error Code | Component | Trigger Condition | Severity | User Message | Possible Cause | Impact | Recovery Action | Retry Policy | Log Context | Test Scenario |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `UPD-001` | Updates | 인터넷 미연결 / GitHub Releases 조회 실패 | Warning | 업데이트 서버에 연결할 수 없습니다. | 오프라인 폐쇄망 환경이거나 일시적 네트워크 오류(추정) | 현재 설치 버전은 정상 사용 가능 | 다시 확인, 릴리스 페이지 열기 | 자동 재시도 안 함(수동 확인) | `release_url`, `error_type` | 오프라인 업데이트 확인 테스트 |
| `UPD-002` | Updates | 업데이트 패키지 다운로드 실패 / 연결 해제 / 무결성 실패 | Error | 업데이트 파일 다운로드 또는 검증에 실패했습니다. | 다운로드 중 네트워크 끊김, 디스크 부족 또는 파일 손상(추정) | **기존 설치 버전은 손상 없이 그대로 유지됨** | 다시 시도, 릴리스 페이지 열기, 진단 정보 복사 | 수동 재시도 | `target_version`, `progress` | 다운로드 실패 후 기존 버전 유지 |
| `UPD-003` | Updates | 설치 권한 부족 / 다른 프로세스 파일 잠금 / 포터블 모드 | Error | 자동 업데이트를 적용할 수 없습니다. | 포터블 실행 모드이거나 설치 폴더 권한/파일 잠금(확인됨) | 기존 버전 유지, 수동 설치기 이용 필요 | 릴리스 페이지 열기, 진단 정보 복사 | 재시도 없음 | `is_installed` | 포터블 모드 업데이트 안내 |
| `APP-999` | Application | 분류되지 않은 예기치 않은 예외 (Global Exception Handler) | Critical | 예상하지 못한 오류가 발생했습니다. | 처리되지 않은 내부 예외(확인됨) | 일부 작업 중단 또는 안전 종료 필요 | 오류 상세 정보 보기, 진단 보고서 내보내기, 계속/안전 종료 | 재시도 없음 | `exception_type`, `correlation_id`, `stack_trace` | 전역 예외 매핑 및 진단 저장 테스트 |
