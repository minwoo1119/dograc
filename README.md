# dograc

> 오픈소스 LLM과 LangChain을 기반으로 구축하는 모듈형 문서 질의응답 RAG 웹 시스템

`dograc`는 사용자가 PDF, DOCX, PPTX, TXT, Markdown 문서를 업로드하고, 해당 문서를 근거로 질문과 답변을 주고받을 수 있는 웹 애플리케이션입니다.

단순히 문서를 LLM에 전달하는 챗봇이 아니라, 문서 파싱부터 검색, 재정렬, 답변 생성, 인용 검증, 성능 평가까지의 전체 RAG 파이프라인을 직접 통제하고 실험할 수 있도록 설계합니다.

프로젝트의 핵심 목표는 NotebookLM과 같은 완성형 서비스를 복제하는 것이 아닙니다. 생성 LLM, 임베딩 모델, Reranker, 검색 전략을 자유롭게 교체하고, 특정 도메인에 맞게 파인튜닝하며, 그 결과를 정량적으로 비교할 수 있는 재현 가능한 오픈소스 RAG 플랫폼을 만드는 것이 목적입니다.

---

## 핵심 가치

- **모델 교체 가능성**: 생성 LLM, 임베딩 모델, Reranker를 설정만으로 교체
- **도메인 특화**: 논문, 사내 기술문서, 규정, 매뉴얼 등 특정 문서군에 최적화
- **근거 기반 답변**: 답변과 함께 파일명, 페이지, 원문 근거를 제공
- **RAG 파이프라인 투명성**: 검색된 Chunk, 검색 점수, Reranking 결과, Prompt를 추적
- **정량 평가**: 검색 품질과 답변 품질을 분리하여 측정
- **파인튜닝 지원**: LoRA 또는 QLoRA 기반 모델 개선
- **재현 가능한 배포**: 학습한 모델 또는 Adapter를 Hugging Face Hub에 배포
- **데이터 통제**: 문서 저장, 검색, 추론, 로그 정책을 직접 구성

---

## 주요 기능

### 문서 관리

- PDF, DOCX, PPTX, TXT, Markdown 업로드
- Workspace별 문서 분리
- 원본 파일 및 문서 메타데이터 저장
- 문서 처리 상태 확인
- 문서 삭제 및 재색인

### 문서 처리

- 문서 구조를 보존한 텍스트 파싱
- 페이지, 섹션, 제목 등의 메타데이터 유지
- 설정 가능한 Chunk 크기 및 Overlap
- 임베딩 생성 및 Vector DB 저장
- 파싱 실패 시 Fallback Parser 적용

### 질의응답

- 질문과 관련된 문서 Chunk 검색
- Dense Search 및 Hybrid Search 지원 예정
- 선택적 Reranking
- 검색된 문맥에 근거한 답변 생성
- 파일명, 페이지 번호, 근거 문장 인용
- 문서에서 확인되지 않은 내용에 대한 답변 제한
- 대화 기록 및 질의 Trace 저장

### 모델 및 평가

- 오픈소스 생성 LLM 교체
- 임베딩 모델 교체
- Reranker 교체
- Base 모델과 파인튜닝 모델 비교
- 검색 및 답변 평가 데이터셋 관리
- Retrieval 및 Generation 지표 측정
- Hugging Face Hub 모델 배포

---

## 시스템 아키텍처

```mermaid
flowchart LR
    U[사용자] --> W[Next.js Web]
    W --> A[FastAPI API]

    A --> S[(PostgreSQL)]
    A --> O[(MinIO / S3)]
    A --> R[RAG Core]

    R --> P[Document Parser]
    P --> C[Chunker]
    C --> E[Embedding Model]
    E --> V[(Qdrant)]

    R --> V
    V --> K[Retriever]
    K --> RR[Reranker]
    RR --> G[Generation LLM]
    G --> A

    G --> HF[Hugging Face Model Hub]
```

---

## 질의 처리 흐름

```text
사용자 질문
  ↓
질문 정규화 및 Workspace 필터 적용
  ↓
질문 임베딩 생성
  ↓
Vector DB에서 관련 Chunk 검색
  ↓
선택적 Reranking
  ↓
상위 Context와 질문을 Prompt로 구성
  ↓
오픈소스 LLM이 답변 생성
  ↓
인용 및 근거 검증
  ↓
답변 + 파일명 + 페이지 + 근거 문장 반환
```

RAG 파이프라인은 **검색 단계**와 **생성 단계**를 분리합니다.

- 관련 문서를 찾지 못한 경우: Chunking, Embedding, Retriever, Reranker 개선
- 관련 문서는 찾았지만 답변이 부정확한 경우: Prompt, 생성 LLM, 파인튜닝 개선

---

## 기술 스택

### Frontend

- Next.js
- TypeScript
- Tailwind CSS
- TanStack Query
- Zustand

### Backend

- Python 3.12+
- FastAPI
- Pydantic v2
- SQLAlchemy 2.x
- Alembic
- LangChain
- LangGraph는 상태 기반 워크플로가 필요한 경우에만 사용

### 문서 처리

- Docling
- PyMuPDF
- python-docx
- python-pptx

### 저장소

- PostgreSQL: 사용자, Workspace, 문서, 대화, Trace 메타데이터
- Qdrant: 문서 Chunk 및 임베딩
- MinIO 또는 S3: 원본 문서

### 모델

- 생성 LLM: Qwen, Gemma, Llama, Mistral 계열
- 기본 임베딩 모델: `BAAI/bge-m3`
- 선택적 Reranker: BGE Reranker 또는 Cross-Encoder 계열
- GPU Serving: vLLM
- 로컬 개발 Fallback: Hugging Face Transformers 또는 Ollama 호환 API

### 학습 및 평가

- Hugging Face Transformers
- PEFT
- TRL
- bitsandbytes
- Datasets
- Accelerate
- RAGAS 또는 자체 평가 파이프라인

### 개발 환경

- `pnpm`
- `uv`
- Docker Compose
- Ruff
- mypy 또는 pyright
- pytest
- Vitest
- React Testing Library
- Playwright

---

## 저장소 구조

```text
dograc/
├─ apps/
│  ├─ web/                       # Next.js 프론트엔드
│  └─ api/                       # FastAPI 백엔드
├─ packages/
│  ├─ rag-core/                  # 프레임워크 독립적인 RAG 도메인 로직
│  ├─ model-providers/           # LLM, Embedding, Reranker Adapter
│  ├─ document-processing/       # 파싱 및 Chunking
│  ├─ evaluation/                # 검색 및 답변 평가
│  └─ shared-types/              # 공용 Schema 및 타입
├─ training/
│  ├─ datasets/                  # 학습 데이터셋 생성 및 전처리
│  ├─ configs/                   # LoRA/QLoRA 설정
│  ├─ scripts/                   # 학습, 평가, 배포 스크립트
│  └─ README.md
├─ infra/
│  ├─ docker/
│  └─ compose.yaml
├─ tests/
│  ├─ integration/
│  ├─ e2e/
│  └─ fixtures/
├─ docs/
│  ├─ architecture.md
│  ├─ rag-evaluation.md
│  ├─ model-publishing.md
│  └─ decisions/
├─ .env.example
├─ AGENTS.md
├─ README.md
└─ LICENSE
```

---

## 시작하기

> 현재 README는 목표 저장소 구조를 기준으로 작성되었습니다. 실제 구현 과정에서 명령어와 환경 변수는 저장소 구성에 맞게 갱신합니다.

### 1. 요구 사항

- Node.js 22+
- pnpm 10+
- Python 3.12+
- uv
- Docker 및 Docker Compose
- 선택 사항: CUDA를 지원하는 NVIDIA GPU

### 2. 저장소 복제

```bash
git clone https://github.com/<YOUR_GITHUB_ID>/dograc.git
cd dograc
```

### 3. 환경 변수 설정

```bash
cp .env.example .env
```

예상 환경 변수는 다음과 같습니다.

```dotenv
# Application
APP_ENV=development
APP_NAME=dograc
WEB_URL=http://localhost:3000
API_URL=http://localhost:8000

# Database
POSTGRES_HOST=localhost
POSTGRES_PORT=5432
POSTGRES_DB=dograc
POSTGRES_USER=dograc
POSTGRES_PASSWORD=dograc
DATABASE_URL=postgresql+asyncpg://dograc:dograc@localhost:5432/dograc

# Qdrant
QDRANT_URL=http://localhost:6333
QDRANT_API_KEY=
QDRANT_COLLECTION=dograc_documents

# Object Storage
S3_ENDPOINT_URL=http://localhost:9000
S3_ACCESS_KEY=dograc
S3_SECRET_KEY=dograc-secret
S3_BUCKET=dograc-documents
S3_REGION=ap-northeast-2

# Model Providers
GENERATION_PROVIDER=vllm
GENERATION_MODEL=<MODEL_ID>
EMBEDDING_PROVIDER=huggingface
EMBEDDING_MODEL=BAAI/bge-m3
RERANKER_ENABLED=false
RERANKER_MODEL=

# vLLM
VLLM_BASE_URL=http://localhost:8001/v1
VLLM_API_KEY=local

# Hugging Face
HF_TOKEN=
HF_HOME=.cache/huggingface

# RAG
CHUNK_SIZE=800
CHUNK_OVERLAP=120
RETRIEVAL_TOP_K=10
RERANK_TOP_K=5
MAX_CONTEXT_CHUNKS=5
```

민감한 값은 Git에 커밋하지 않습니다.

### 4. 인프라 실행

```bash
docker compose -f infra/compose.yaml up -d
```

기본적으로 다음 서비스가 실행됩니다.

- PostgreSQL
- Qdrant
- MinIO

### 5. Backend 설치 및 실행

```bash
cd apps/api
uv sync
uv run alembic upgrade head
uv run uvicorn app.main:app --reload --host 0.0.0.0 --port 8000
```

API 문서:

```text
http://localhost:8000/docs
```

### 6. Frontend 설치 및 실행

```bash
cd apps/web
pnpm install
pnpm dev
```

웹 애플리케이션:

```text
http://localhost:3000
```

### 7. 전체 테스트

```bash
# Backend
cd apps/api
uv run pytest

# Frontend
cd apps/web
pnpm test

# E2E
pnpm exec playwright test
```

---

## 모델 교체

생성 LLM, 임베딩 모델, Reranker는 애플리케이션 도메인 로직과 분리된 Adapter로 구현합니다.

예시 설정:

```dotenv
GENERATION_PROVIDER=vllm
GENERATION_MODEL=<HUGGING_FACE_MODEL_ID>

EMBEDDING_PROVIDER=huggingface
EMBEDDING_MODEL=BAAI/bge-m3

RERANKER_ENABLED=true
RERANKER_MODEL=<RERANKER_MODEL_ID>
```

모델을 교체할 때는 다음 항목을 함께 확인해야 합니다.

- Chat Template
- Context Length
- Tokenizer
- GPU 메모리 요구량
- 양자화 지원 여부
- 한국어 문서 질의응답 성능
- 라이선스 및 배포 조건

---

## 파인튜닝 워크플로

`dograc`는 문서 내용을 모델에 지속적으로 암기시키기 위한 파인튜닝을 지향하지 않습니다. 변경되는 문서 정보는 RAG를 통해 제공하고, 파인튜닝은 모델의 응답 행동과 도메인 적합성을 개선하는 데 사용합니다.

### 권장 진행 순서

1. 기본 모델로 RAG MVP 구축
2. 평가용 질문·정답·근거 데이터셋 생성
3. 검색 실패와 생성 실패를 분리하여 분석
4. Prompt 및 검색 파이프라인 개선
5. 남아 있는 생성 문제를 대상으로 LoRA/QLoRA 학습
6. Base 모델과 동일한 평가 데이터셋으로 비교
7. Adapter 또는 병합 모델을 Hugging Face Hub에 배포
8. 배포 모델을 RAG 생성 모델로 연결

### 학습 데이터 예시

```json
{
  "instruction": "제공된 문서 문맥만을 근거로 질문에 답하세요. 문맥에 없는 내용은 확인할 수 없다고 답하세요.",
  "context": "검색된 문서 Chunk 내용",
  "question": "이 문서에서 제시한 핵심 결론은 무엇인가요?",
  "answer": "문서에 따르면 핵심 결론은 ...입니다. 근거는 ... 페이지에서 확인할 수 있습니다."
}
```

### 파인튜닝 대상

- 문서에 없는 내용을 생성하지 않는 행동
- 답변 형식 준수
- 인용 표기 방식
- 전문 용어 사용
- 도메인 특화 설명 방식
- 정보가 부족할 때의 적절한 답변 거절

---

## 성능 평가

단순히 답변이 자연스러운지만 확인하지 않고, 검색과 생성 성능을 분리해 평가합니다.

### Retrieval 평가

- Recall@K
- Precision@K
- MRR
- nDCG
- Context Relevance

### Generation 평가

- Answer Correctness
- Faithfulness
- Citation Accuracy
- Citation Completeness
- Unsupported Claim Rate
- Answer Relevance
- 적절한 답변 거절 정확도

### 비교 실험 예시

| 실험 항목 | 비교 대상 |
|---|---|
| Chunking | 고정 길이 vs 문단·섹션 기반 |
| Embedding | BGE-M3 vs 다른 다국어 임베딩 모델 |
| Retrieval | Dense vs Hybrid Search |
| Reranking | 적용 전 vs 적용 후 |
| Generation | Qwen vs Gemma vs Llama 계열 |
| Fine-tuning | Base 모델 vs LoRA 모델 |
| Quantization | 원본 모델 vs 4-bit 모델 |
| Prompt | 일반 Prompt vs 근거 제한 Prompt |

모든 실험은 가능한 한 동일한 평가 데이터셋과 설정을 사용하여 재현할 수 있어야 합니다.

---

## API 초안

아래 API는 초기 설계 기준이며 구현 과정에서 변경될 수 있습니다.

```text
POST   /api/v1/workspaces
GET    /api/v1/workspaces
GET    /api/v1/workspaces/{workspace_id}
DELETE /api/v1/workspaces/{workspace_id}

POST   /api/v1/workspaces/{workspace_id}/documents
GET    /api/v1/workspaces/{workspace_id}/documents
GET    /api/v1/documents/{document_id}
DELETE /api/v1/documents/{document_id}
POST   /api/v1/documents/{document_id}/reindex

POST   /api/v1/workspaces/{workspace_id}/conversations
GET    /api/v1/conversations/{conversation_id}
POST   /api/v1/conversations/{conversation_id}/messages

GET    /api/v1/traces/{trace_id}
POST   /api/v1/evaluations/run
GET    /api/v1/evaluations/{evaluation_id}
```

---

## 개발 로드맵

### Phase 1. RAG MVP

- [ ] 프로젝트 모노레포 구성
- [ ] Workspace 및 문서 업로드
- [ ] PDF 파싱
- [ ] Chunking 및 임베딩
- [ ] Qdrant 색인
- [ ] 기본 질의응답
- [ ] 파일명 및 페이지 인용
- [ ] 기본 Trace 저장

### Phase 2. 검색 품질 개선

- [ ] DOCX, PPTX, Markdown 지원
- [ ] 구조 기반 Chunking
- [ ] Hybrid Search
- [ ] Reranker 적용
- [ ] Query Rewriting
- [ ] Retrieval 평가 파이프라인
- [ ] 검색 결과 분석 화면

### Phase 3. 생성 품질 개선

- [ ] Prompt 버전 관리
- [ ] 답변 거절 정책
- [ ] 인용 검증
- [ ] Generation 평가 파이프라인
- [ ] 모델별 성능 비교
- [ ] 응답 시간 및 토큰 사용량 분석

### Phase 4. 파인튜닝 및 배포

- [ ] 도메인 질의응답 데이터셋 구축
- [ ] LoRA/QLoRA 학습
- [ ] Base 모델과 정량 비교
- [ ] Hugging Face Hub 배포
- [ ] 배포 모델을 RAG 시스템에 연결
- [ ] Model Card 작성

### Phase 5. 운영 기능

- [ ] 사용자 인증
- [ ] 사용자 및 Workspace별 데이터 격리
- [ ] 비동기 문서 처리 Queue
- [ ] 파일 저장 및 삭제 정책
- [ ] 모니터링 및 오류 추적
- [ ] GPU Serving 최적화

---

## 설계 원칙

- 검색과 생성을 하나의 불투명한 Chain으로 숨기지 않습니다.
- 답변에는 추적 가능한 출처가 있어야 합니다.
- 검색되지 않은 정보는 답변 근거로 사용하지 않습니다.
- 모델보다 먼저 데이터와 평가 기준을 정의합니다.
- 측정 가능한 기준선 없이 파인튜닝부터 진행하지 않습니다.
- LangChain 객체가 도메인 로직 전체에 직접 확산되지 않도록 합니다.
- 외부 모델과 저장소는 명시적인 Interface 또는 Protocol 뒤에 배치합니다.
- 비밀키, 사용자 문서 원문, 민감한 Prompt는 로그에 기록하지 않습니다.
- 사용자와 Workspace 간 문서 검색 범위를 엄격히 격리합니다.
- 변경된 코드에는 관련 테스트와 문서를 함께 반영합니다.

세부 개발 규칙은 [`AGENTS.md`](./AGENTS.md)를 참고합니다.

---

## 프로젝트 차별점

`dograc`는 NotebookLM보다 더 많은 범용 기능을 제공하는 것을 목표로 하지 않습니다. 대신 다음 항목을 프로젝트의 차별점으로 제시합니다.

1. 생성 LLM, 임베딩, Reranker, 검색 전략을 자유롭게 교체할 수 있는 모듈 구조
2. 검색 결과와 LLM 입력 Context를 직접 확인할 수 있는 투명한 RAG 실행 과정
3. 특정 도메인 문서에 최적화된 파싱, 검색, Prompt, 파인튜닝
4. 검색과 생성 성능을 분리해 측정하는 평가 체계
5. Base 모델과 파인튜닝 모델의 정량 비교
6. Hugging Face Hub를 통한 모델과 학습 결과 공개
7. 자체 인프라에서 문서와 모델을 운영할 수 있는 데이터 통제력

---

## 기여 방법

1. 이 저장소를 Fork합니다.
2. 작업 브랜치를 생성합니다.
3. 변경 사항과 테스트를 함께 작성합니다.
4. Lint, Type Check, Test를 실행합니다.
5. 변경 목적과 검증 결과를 포함해 Pull Request를 생성합니다.

대규모 구조 변경이나 모델·저장소 교체는 먼저 Issue 또는 Architecture Decision Record로 논의합니다.

---

## 라이선스

라이선스는 프로젝트 공개 범위와 사용하는 모델의 라이선스를 검토한 뒤 결정합니다.

오픈소스 모델을 사용할 때는 각 모델의 다음 조건을 반드시 확인해야 합니다.

- 상업적 이용 가능 여부
- 재배포 가능 여부
- 파생 모델 공개 조건
- 모델명 및 저작권 고지 의무
- 학습 데이터 관련 제한

프로젝트 라이선스가 결정되면 이 섹션과 `LICENSE` 파일을 함께 갱신합니다.

---

## 관련 문서

- [`AGENTS.md`](./AGENTS.md): AI 코딩 에이전트 및 개발자 작업 지침
- `docs/architecture.md`: 시스템 아키텍처 상세
- `docs/rag-evaluation.md`: 검색 및 답변 평가 기준
- `docs/model-publishing.md`: Hugging Face 모델 배포 절차
- `training/README.md`: 데이터셋 생성 및 파인튜닝 실행 방법
