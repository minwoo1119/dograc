# AGENTS.md — 개발자 및 AI 에이전트 지침서

> 본 문서는 `dograc` 프로젝트를 유지보수하거나 기능을 확장하는 AI 코딩 에이전트 및 개발자를 위한 아키텍처 원칙, 개발 지침, 코딩 컨벤션 및 평가 가이드를 정의합니다.

---

## 1. 프로젝트 철학 및 설계 원칙

1. **RAG 파이프라인의 투명성 (Transparency First)**
   - 검색(Retrieval)과 생성(Generation)을 하나의 불투명한 블랙박스 체인으로 결합하지 않습니다.
   - 검색 단계와 생성 단계의 입출력, 지연 시간, 검색된 청크의 점수 및 프롬프트 전문은 반드시 `Trace` 레코드로 기록되어야 합니다.
   - 실패 분석 시 검색 실패(문서를 못 찾음)와 생성 실패(문서는 찾았으나 환각 발생)를 명확히 분리할 수 있어야 합니다.

2. **도메인 계층의 프레임워크 독립성 (Decoupled Core)**
   - LangChain 등 특정 프레임워크의 내부 객체나 인터페이스가 도메인 엔티티 및 비즈니스 로직 전체에 직접 침투하지 않도록 Protocol/추상화 계층 뒤에 배치합니다.
   - LLM, Embedding, Reranker, Vector DB 공급자는 설정(`config.py`) 기반으로 유연하게 교체 가능한 Adapter 패턴을 적용합니다.

3. **신뢰 기반 인라인 인용 (Grounded Citations)**
   - 답변에 포함되는 모든 주장은 문서의 근거 청크에 기반해야 합니다.
   - 답변 생성 시 `[파일명, p.숫자]` 형태의 정규화된 인용 표기를 준수하며, 프론트엔드는 이를 사용자 친화적 인라인 배지로 파싱하여 렌더링합니다.
   - 검색된 문맥에 답이 없는 경우 무리하게 답변을 지어내지 않고 정중하게 답변 불가를 선언해야 합니다.

4. **엄격한 데이터 격리 및 보안 (Strict Isolation & Privacy)**
   - 모든 쿼리와 저장소 작업은 `user_id`와 `workspace_id`를 기준으로 논리적으로 완전 격리되어야 합니다.
   - API 키, 비밀 토큰, 문서 원문 및 민감 프롬프트는 로그나 에러 메시지에 노출되지 않아야 합니다.

---

## 2. 작업 및 커밋 컨벤션

### 2.1 Git 커밋 메시지 규칙
모든 커밋 메시지는 다음 형식을 엄격히 준수합니다:
```text
feat:개발내용
fix:수정내용
refactor:리팩토링내용
test:테스트내용
docs:문서작업내용
```
- 예시: `feat:KDS 디자인 시스템 기반 웹 UI 전면 개편 및 DESIGN.md 정비`
- 영어 커밋인 경우에도 일관된 접두사를 유지합니다.

### 2.2 테스트 작성 의무
- 기능 추가 또는 수정 시 반드시 해당 기능에 대한 테스트 코드를 작성하거나 기존 테스트를 갱신합니다.
- 백엔드 테스트: `apps/api`에서 `uv run pytest` (모든 테스트 통과 필수).
- 프론트엔드 테스트: `apps/web`에서 `npx pnpm build` (TypeScript 타입 오류 및 빌드 경고 0건 유지).

---

## 3. 저장소 구조 및 모듈 분리

```text
dograc/
├─ apps/
│  ├─ web/                       # Next.js 14 App Router 기반 SPA 프론트엔드 (KDS 디자인 시스템 준수)
│  │  ├─ DESIGN.md               # 프론트엔드 UI/UX 디자인 사양서 (Reading-First, KDS 기반)
│  │  ├─ src/app/                # Next.js 라우트 및 레이아웃
│  │  ├─ src/components/         # Navbar, DocumentPanel, ChatPanel, TraceDrawer 등
│  │  └─ src/lib/                # API 클라이언트 및 상태 저장소(Zustand, React Query)
│  └─ api/                       # FastAPI 기반 고성능 비동기 백엔드
│     ├─ app/api/v1/             # 엔드포인트 라우터 (workspaces, documents, conversations, traces)
│     ├─ app/core/               # 설정(Config), 데이터베이스 세션, CORS 미들웨어
│     ├─ app/models/             # SQLAlchemy 비동기 ORM 엔티티
│     ├─ app/schemas/            # Pydantic v2 직렬화/검증 스키마
│     ├─ app/services/           # RAG 파이프라인(Ingestion, Retriever, Generator, Trace)
│     └─ tests/                  # pytest 단위 및 통합 테스트 모음
├─ infra/
│  └─ compose.yaml               # PostgreSQL, Qdrant, MinIO 오케스트레이션
├─ docs/                         # 아키텍처 다이어그램 및 상세 기술 문서
├─ AGENTS.md                     # 본 개발자 및 AI 에이전트 지침서
├─ README.md                     # 외부 공개용 제품 소개 및 퀵스타트 가이드
└─ .env.example                  # 환경 변수 템플릿
```

---

## 4. RAG 모델 교체 및 Adapter 규격

생성 LLM과 임베딩 모델은 애플리케이션 서비스 로직과 독립적으로 동작하도록 설계됩니다.

### 4.1 모델 공급자 인터페이스 (`apps/api/app/services/rag/`)
- **Embedding Adapter**:
  - `embed_query(text: str) -> list[float]`
  - `embed_documents(texts: list[str]) -> list[list[float]]`
  - 지원: `BAAI/bge-m3`, HuggingFace Transformers, Ollama API
- **LLM Generator Adapter**:
  - `generate(prompt: str, stream: bool = False) -> RAGResponse`
  - 지원: vLLM (OpenAI-compatible), Ollama Local (`qwen2.5:7b`), HuggingFace Hub

### 4.2 모델 변경 시 점검 체크리스트
1. Chat Template 및 특수 토큰 일치 여부
2. 문맥 길이 (Context Window) 한계 및 Chunking Overlap 적합성
3. 한글 토크나이저 밀도 및 한국어 문서 질의응답 품질
4. GPU VRAM 소요량 및 양자화(AWQ, GPTQ, GGUF) 호환성

---

## 5. 파인튜닝 워크플로 및 평가 체계

`dograc`는 문서 내용을 파라미터에 단순 암기시키는 파인튜닝을 지양합니다. 지식은 RAG 검색을 통해 공급하고, 파인튜닝(LoRA/QLoRA)은 **인용 표기 형식 준수, 미확인 질문에 대한 정직한 답변 거절, 도메인 전문 어휘 활용** 등의 행동 교정에 집중합니다.

### 5.1 권장 프로세스
```text
1. 기본 모델로 RAG 베이스라인 구축
  ↓
2. 평가용 골든 데이터셋(질문-정답-인용근거) 수집
  ↓
3. Retrieval 실패(청크 검색)와 Generation 실패(환각) 분리 측정
  ↓
4. Prompt 튜닝 및 검색 전략(Top-K, Chunker) 최적화
  ↓
5. 잔여 생성 문제 해결을 위한 LoRA/QLoRA 파인튜닝
  ↓
6. 동일 벤치마크 데이터셋으로 Base vs Tuned 정량 비교
  ↓
7. Hugging Face Hub 모델 레지스트리 배포 및 RAG 서비스 연결
```

### 5.2 정량 평가 지표
- **검색(Retrieval) 평가**:
  - `Recall@K`, `nDCG@K`, `Context Relevance`
- **생성(Generation) 평가**:
  - `Faithfulness` (문맥에 기반한 충실도)
  - `Citation Precision & Recall` (인용 정확도)
  - `Unsupported Claim Rate` (근거 없는 환각 주장 비율)
  - `Refusal Accuracy` (답변 거절 상황 판별 정확도)

---

## 6. 개발 로드맵

- [x] **Phase 1. RAG MVP 완성**:
  - [x] Docker Compose 기반 인프라 (Postgres, Qdrant, MinIO)
  - [x] FastAPI 백엔드 (Workspace, Document Ingestion, PDF 파싱, Vector DB 색인)
  - [x] RAG 질의응답 파이프라인 (청크 검색, 로컬 Ollama/vLLM 연동, 인라인 인용)
  - [x] Trace 감사 로그 저장 및 조회 API
  - [x] Next.js 프론트엔드 웹 UI 구축 (KDS 가이드라인 적용, 이모지 배제, 1px 헤어라인)
- [ ] **Phase 2. 검색 품질 고도화**:
  - [ ] DOCX, PPTX, Markdown 멀티포맷 파서 확장
  - [ ] BM25 + Dense Hybrid Search (상호 순위 융합 RRF)
  - [ ] Cross-Encoder Reranker 적용
  - [ ] Query Decomposition / Rewriting
- [ ] **Phase 3. 생성 품질 및 파인튜닝**:
  - [ ] 프롬프트 버전 관리 및 AB 테스팅
  - [ ] LoRA / QLoRA 튜닝 스크립트 패키징
  - [ ] Hugging Face Hub 모델 배포 자동화
- [ ] **Phase 4. 엔터프라이즈 운영 기능**:
  - [ ] OAuth2 / JWT 사용자 인증 체계
  - [ ] 비동기 Ingestion 작업 큐 (Celery / Redis / ARQ)
  - [ ] 다중 워크스페이스 권한 제어 (RBAC)
