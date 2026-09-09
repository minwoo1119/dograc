---
name: dograc
design_system_name: dograc Design System (based on KDS)
slug: dograc
category: enterprise-ai / document-rag
last_updated: "2026-09-09"
created_at: 2026-09-09
lang: ko
colors:
  # Primary · Blue (core = blue-700, Informative / Primary Action)
  blue-50: oklch(0.975 0.012 282)    # #F0F2FA — 연한 배경 / 선택 탭
  blue-100: oklch(0.949 0.015 282)   # #E4E7F6
  blue-200: oklch(0.876 0.036 282)   # #C8CEEC
  blue-300: oklch(0.774 0.066 282)   # #A3AEE0
  blue-400: oklch(0.669 0.096 281)   # #7D8DD4
  blue-500: oklch(0.586 0.117 280)   # #6071C8
  blue-600: oklch(0.531 0.134 278)   # #5055B1 CORE — UI 기본 액션 / 주 버튼
  blue-700: oklch(0.494 0.144 277)   # #5055B1 CORE
  blue-800: oklch(0.423 0.126 277)   # #3F4391 — 버튼 Hover / Pressed
  blue-900: oklch(0.339 0.096 278)   # #2D3068
  # Semantic · Green (core = green-600, Success / 색인 완료)
  green-50: oklch(0.980 0.015 135)   # #F2FAF0
  green-100: oklch(0.959 0.027 135)  # #E5F5E2
  green-200: oklch(0.904 0.066 136)  # #C6EBC0
  green-600: oklch(0.662 0.188 139)  # #4DAC27 CORE — Success / 색인 완료 배지
  green-700: oklch(0.662 0.188 139)  # #4DAC27
  # Semantic · Red (core = red-500/600, Negative / Error / 삭제)
  red-50: oklch(0.975 0.015 26)      # #FEF2F2
  red-100: oklch(0.949 0.021 14)     # #FEE2E2
  red-500: oklch(0.603 0.232 26)     # #EC1F2D CORE — Negative / 오류 배지
  red-600: oklch(0.571 0.216 26)     # #DA2128
  # Grayscale (50 → 900)
  gray-50: oklch(0.988 0.000 0)      # #FAFBFC — 메인 캔버스 배경 워시
  gray-100: oklch(0.970 0.003 265)   # #F4F5F7 — 컴포넌트 subtle 배경
  gray-200: oklch(0.940 0.004 271)   # #EAEBEE — 연한 구분선 / hover 배경
  gray-300: oklch(0.912 0.006 265)   # #E0E2E6 — 1px divider / 테두리 헤어라인
  gray-400: oklch(0.845 0.009 265)   # #C9CCD2 — 비활성 아이콘 / placeholder
  gray-500: oklch(0.737 0.012 264)   # #A6AAB2 — 캡션 / 메타 텍스트
  gray-600: oklch(0.615 0.015 262)   # #80858E
  gray-700: oklch(0.434 0.014 264)   # #4D5159 — 본문 보조 텍스트
  gray-800: oklch(0.313 0.013 267)   # #2E3138
  gray-900: oklch(0.231 0.010 268)   # #1B1D22 — 제목 및 본문 기본 텍스트
  white: oklch(1.000 0.000 0)        # #FFFFFF — 카드 및 패널 배경
  black: oklch(0.000 0.000 0)        # #000000
typography:
  # 기본 폰트 스택: 국문 Noto Sans KR / 영문·숫자 Roboto / 코드 monospace
  font-family: "\"Noto Sans KR\", Roboto, -apple-system, \"Apple SD Gothic Neo\", sans-serif"
  letter-spacing: -0.01em
  title-l:
    fontSize: 24px
    fontWeight: 700
    lineHeight: 34px
  title-m:
    fontSize: 18px
    fontWeight: 700
    lineHeight: 26px
  title-s:
    fontSize: 14px
    fontWeight: 700
    lineHeight: 20px
  text-l:
    fontSize: 14px
    fontWeight: 400
    lineHeight: 22px
  text-m:
    fontSize: 13px
    fontWeight: 400
    lineHeight: 20px
  text-s:
    fontSize: 11px
    fontWeight: 400
    lineHeight: 16px
spacing:
  spacing-50: 2px
  spacing-100: 4px
  spacing-200: 8px
  spacing-300: 12px
  spacing-400: 16px
  spacing-500: 20px
  spacing-600: 24px
  spacing-800: 32px
rounded:
  radius-4: 4px       # 기본 버튼, 배지, 인풋 (단정하고 정갈한 모서리)
  radius-6: 6px       # 카드, 모달, 패널
  radius-8: 8px       # 대형 컨테이너
  radius-round: 9999px # 완전 원형
---

# dograc Design System — DESIGN.md

## 1. System Identity & Philosophy

**dograc Design System**은 대규모 지식 문서 기반의 RAG(Retrieval-Augmented Generation) 시스템을 위한 Reading-First 미니멀 디자인 시스템입니다. 교보문고 디자인 시스템(Kyobo Design System - KDS)의 시각 언어(정갈한 헤어라인, 높은 텍스트 가독성, 지적인 Periwinkle Indigo 색상 체계)를 계승하여 엔터프라이즈 AI 환경에 최적화되었습니다.

### 핵심 원칙
1. **Reading-First & Minimalist**:
   - 긴 텍스트와 AI 생성 답변, 문서 본문을 편안하게 장시간 읽을 수 있는 서재·도서관 분위기의 차분한 UI를 구성합니다.
   - 트렌디한 네온 그라데이션, 두꺼운 그림자, 과도한 시각적 잡음을 철저히 배제합니다.
2. **1px Hairline Surfaces**:
   - 컴포넌트와 패널의 경계는 무거운 Drop Shadow 대신 1px 헤어라인(`border-kds-gray-300`, `#E0E2E6`)과 옅은 배경 워시(`bg-kds-gray-50`, `#F4F5F7`)를 통해 정갈하게 분할합니다.
3. **No Emoji / Iconographic Clarity**:
   - 이모지(🤖, ⚡, 💡, 📄 등)는 UI의 격을 떨어뜨리고 시야를 분산시키므로 일절 사용하지 않습니다.
   - 의미 전달은 1.5~1.75 stroke의 단정한 라인 아이콘(Lucide)과 직관적인 한국어 텍스트 라벨로 완결합니다.
4. **Data-Driven & Citation-First**:
   - AI 답변의 모든 근거 문서는 `[파일명, p.숫자]` 형태의 정갈한 인라인 출처 배지로 식별하며, 클릭 시 관련 청크와 원문으로 자연스럽게 안내합니다.

---

## 2. Color Palette & Roles

### 2.1 Core Action & Accent
- **Indigo Blue (`kds-blue-600`, `#5055B1`)**:
  - 시스템의 대표 액션 컬러로, 차갑지도 뜨겁지도 않은 지적이고 신뢰감 있는 분위기를 연출합니다.
  - 주요 CTA 버튼, 활성 탭 텍스트, 인용구 배지, 포커스 링에 적용됩니다.
  - Hover / Active 시에는 `kds-blue-800` (`#3F4391`)을 사용합니다.

### 2.2 Functional Grayscale
- **Canvas (`gray-50`, `#FAFBFC` / `#F4F5F7`)**: 배경 워시
- **Surface (`white`, `#FFFFFF`)**: 카드, 패널, 모달 본체
- **Border (`gray-300`, `#E0E2E6`)**: 1px 구분선 및 컨테이너 테두리
- **Text Primary (`gray-900`, `#1B1D22`)**: 제목, 강조 본문, 버튼 텍스트
- **Text Secondary (`gray-700`, `#4D5159`)**: 일반 본문 텍스트, 메시지 내용
- **Text Muted (`gray-500`, `#8B919D`)**: 캡션, 타임스탬프, 보조 설명 라벨

### 2.3 Semantic Status
- **색인 완료 (Ready)**: `green-100` 배경 + `green-600` (`#4DAC27`) 텍스트
- **처리 중 (Processing)**: `blue-50` 배경 + `blue-600` (`#5055B1`) 텍스트 (스피너 아이콘)
- **대기 중 (Pending)**: `amber-50` 배경 + `amber-700` 텍스트
- **오류 / 삭제 (Error / Danger)**: `red-50` 배경 + `red-500` (`#EC1F2D`) 텍스트

---

## 3. Typography & Micro-copy

### 3.1 Font Family & Letter Spacing
- 국문 텍스트: **Noto Sans KR**
- 영문 / 숫자: **Roboto**
- 코드 / 수치 / 메타데이터: **font-mono** (Consolas, Menlo, Monaco)
- 전 스케일 자간(Letter Spacing): **`-0.01em`** (한글 텍스트 밀도와 판독성을 최적화)

### 3.2 Tone of Voice & Copywriting
- 사용자를 존중하며 명확한 정보를 전달하는 **해요체**를 기본으로 사용합니다.
  - 예: *"워크스페이스를 먼저 선택해 주세요"*, *"문서를 드래그하여 업로드하세요"*
- CTA 버튼은 명확한 동작 동사(`-하기`, `-보기`)로 작성하며 간결함을 유지합니다.
  - 예: *"질문하기"*, *"분석 보기"*, *"새 워크스페이스 만들기"*

---

## 4. Component Patterns

### 4.1 Navigation Bar (`Navbar.tsx`)
- 고정 높이 56px (`h-14`), 상단 1px 보더 (`border-b border-kds-gray-300`).
- 좌측: 브랜드 워드마크 (`dograc`, Indigo Blue 액션 포인트).
- 중앙: 현재 워크스페이스 드롭다운 셀렉터 (간결한 팝오버 메뉴).
- 우측: "새 워크스페이스" 생성 버튼 (단정한 다이얼로그 모달 트리거).

### 4.2 Document Panel (`DocumentPanel.tsx`)
- 문서 보관함 및 드래그앤드롭 업로드 영역.
- 점선 테두리 (`border-dashed border-kds-gray-300`)와 깔끔한 업로드 가이드.
- 문서 아이템 리스트:
  - 파일명, 확장자, 업로드 시각 표기.
  - 22px 높이의 고정 규격 상태 배지 (`badge-basic`).
  - 즉시 색인 요청 (`Play`) 및 삭제 (`Trash2`) 인라인 액션 버튼.

### 4.3 Chat Panel (`ChatPanel.tsx`)
- 상단 대화 세션 탭 (가로 스크롤 가능한 단정한 언더라인 탭 구조).
- 메시지 스레드:
  - 사용자 질문: 부드러운 인디고 배경 (`bg-kds-blue-50 border border-kds-blue-100`)의 우측 배치.
  - AI 어시스턴트 답변: 화이트 배경 (`bg-white border border-kds-gray-300`)의 좌측 배치.
  - 인용 배지: `[파일명, p.숫자]`를 자동으로 파싱하여 가독성 높은 인라인 배지로 렌더링.
  - RAG 추적 버튼: 답변 하단에 "검색 및 실행 분석 보기" 라인 버튼 제공.
- 하단 인풋 영역:
  - 2행 멀티라인 텍스트필드, Enter로 전송 (Shift+Enter 줄바꿈).
  - 전송 버튼: 단정한 화살표 아이콘 (`Send`, stroke 1.5).

### 4.4 Trace Drawer (`TraceDrawer.tsx`)
- RAG 파이프라인의 내부 실행 상태를 검증하는 500px 너비의 슬라이드인 패널.
- 상단 요약 카드: 모델명, 응답 레이턴시(ms).
- 검색 청크 카드: 유사도 스코어(소수점 3자리), 문서명, 페이지 번호, 매칭 본문 미리보기.
- 프롬프트 아코디언: LLM에 실제 주입된 프롬프트 전문 토글 뷰어.

---

## 5. Do's and Don'ts

| 구분 | Do (권장) | Don't (금지) |
| --- | --- | --- |
| **장식 & 그래픽** | 1px 헤어라인, 은은한 배경 워시, 1.5 stroke 라인 아이콘 | 형광 그라데이션, 유리 모피즘(glassmorphism), 일러스트 |
| **이모지** | 단정한 텍스트 및 Lucide 아이콘 사용 | 🤖, 📄, 💡, ⚡ 등 유치한 이모티콘 일절 금지 |
| **색상** | Periwinkle Indigo (`#5055B1`)를 단일 액션 포인트로 절제 | 알록달록한 다중 강조색 남용 |
| **모서리 곡률** | 4px (`rounded`), 6px (`rounded-md`)의 단정한 직사각 위주 | 과장된 곡률 (`rounded-2xl`, `rounded-3xl` 등) 남용 |
| **상태 전달** | 텍스트 라벨과 색상, 아이콘을 병기 (접근성 준수) | 색상만으로 성공/실패를 암시하는 디자인 |
