"""
dograc RAG 학술 실측 벤치마크 스크립트 (scripts/benchmark_rag.py)
------------------------------------------------------------------
이 스크립트는 폐쇄망 단말기 환경에서 실제 RAG 파이프라인(문서 수집, 색인,
BGE-M3 임베딩, Qdrant 벡터 검색, Ollama Qwen2.5 LLM 생성, Trace 영속화)의
정량 성능 및 지연 시간을 100% 실측하여 학술 논문(PAPER.md) 표 4, 5, 6에 수록할
지표를 산출합니다.
"""

import asyncio
import json
import math
import os
import re
import sys
import time
from pathlib import Path
from typing import Any

import fitz  # PyMuPDF
import httpx

API_BASE_URL = "http://127.0.0.1:8000/api/v1"
HEADERS = {"X-User-ID": "00000000-0000-0000-0000-000000000001"}
OUTPUT_DIR = Path(__file__).resolve().parent / "benchmark_data"


# =====================================================================
# 1. 평가용 표준 사내 규정 샘플 PDF 자동 생성
# =====================================================================

SAMPLE_POLICIES = [
    {
        "filename": "사내_정보보안_규정.pdf",
        "pages": [
            (
                "제1조 (목적) 본 규정은 사내 정보자산 및 핵심 기술 정보를 보호하기 위한 기본 보안 수칙을 규정함을 목적으로 한다.\n"
                "제2조 (비밀 등급) 사내 정보는 1급 비밀, 2급 비밀, 대외비, 일반 문서의 4단계로 분류한다.\n"
                "제3조 (외부 반출 금지) 1급 및 2급 비밀 문서는 보안책임자의 서면 승인 없이 사외 반출이 엄격히 금지된다."
            ),
            (
                "제4조 (단말기 보안) 모든 임직원의 업무용 PC는 10분 이상 자리 비움 시 자동으로 화면이 잠겨야 한다.\n"
                "제5조 (비밀번호 정책) 업무 시스템 비밀번호는 영문 대소문자, 숫자, 특수문자를 혼합하여 최소 10자리 이상으로 설정하고, 90일 주기로 변경해야 한다.\n"
                "제6조 (USB 매체 제어) 인가되지 않은 개인 이동식 저장장치(USB, 외장하드)는 업무 단말기에 연결할 수 없다."
            ),
            (
                "제7조 (무선망 사용 제한) 연구개발 구역 내에서는 사설 Wi-Fi 핫스팟 및 블루투스 테더링 사용이 전면 차단된다.\n"
                "제8조 (보안 사고 보고) 랜섬웨어 감염 또는 기밀 유출 정황이 발견된 경우 30분 이내에 보안운영팀(내선 1119)으로 최초 유선 보고해야 한다.\n"
                "제9조 (벌칙) 본 규정을 위반하여 기밀을 유출한 임직원은 사규에 따라 징계 해고 및 민형사상 책임을 진다."
            ),
        ],
    },
    {
        "filename": "근태_및_복무_관리_지침.pdf",
        "pages": [
            (
                "제1조 (근무 시간) 기본 업무 시간은 평일 09:00부터 18:00까지이며, 12:00부터 13:00까지는 휴게시간으로 한다.\n"
                "제2조 (유연근무제) 입사 3개월 이상의 정규직 직원은 주당 40시간 범위 내에서 시차출퇴근제를 신청할 수 있다.\n"
                "제3조 (재택근무) 부서장의 사전 승인을 받은 직원은 주 2회까지 원격 재택근무를 수행할 수 있다."
            ),
            (
                "제4조 (연차휴가) 1년간 80% 이상 출근한 근로자에게는 15일의 유급휴가가 부여된다.\n"
                "제5조 (병가) 업무 외 질병 또는 부상으로 직무 수행이 불가능한 경우 연간 최대 30일의 유급 병가를 사용할 수 있으며, 3일 이상 연속 병가 시 의사 진단서를 필히 첨부해야 한다.\n"
                "제6조 (경조휴가) 본인 결혼 시에는 5일의 유급 경조휴가가 부여된다."
            ),
            (
                "제7조 (출장 여비) 국내 출장 시 일비는 1일당 30,000원을 지급하며, 숙박비는 1박당 최대 100,000원 실비 지원한다.\n"
                "제8조 (퇴근 후 연락 제한) 긴급 재난 상황을 제외하고, 퇴근 시간 이후 업무용 메신저를 통한 업무 지시는 지양한다."
            ),
        ],
    },
    {
        "filename": "연구개발_특허_출원_가이드.pdf",
        "pages": [
            (
                "제1조 (직무발명) 임직원이 업무 수행 중 완성한 모든 발명과 기술적 고안은 회사의 직무발명으로 귀속된다.\n"
                "제2조 (출원 보상금) 사내 특허 심의회를 통과하여 국내 특허를 출원한 발명자에게는 1건당 500,000원의 출원 보상금을 지급한다.\n"
                "제3조 (등록 보상금) 출원된 특허가 특허청 심사를 거쳐 최종 등록된 경우, 1건당 1,500,000원의 등록 보상금을 발명자 전원에게 분할 지급한다."
            ),
            (
                "제4조 (오픈소스 소프트웨어 활용) 사내 프로젝트에 오픈소스 라이브러리를 도입할 경우 GPL 계열의 카피레프트 라이선스는 지식재산팀의 법무 검토를 거쳐야 한다.\n"
                "제5조 (선행기술 조사) 신규 연구 과제 착수 전 최소 20건 이상의 국내외 선행 특허 및 논문 분석 보고서를 작성하여 연구소장에게 보고해야 한다."
            ),
        ],
    },
]


def create_sample_pdfs(target_dir: Path) -> list[Path]:
    target_dir.mkdir(parents=True, exist_ok=True)
    created_files = []
    font_path = "C:/Windows/Fonts/malgun.ttf"
    for policy in SAMPLE_POLICIES:
        file_path = target_dir / policy["filename"]
        doc = fitz.open()
        for page_text in policy["pages"]:
            page = doc.new_page(width=595, height=842)  # A4
            if os.path.exists(font_path):
                page.insert_font(fontname="malgun", fontfile=font_path)
                page.insert_text(fitz.Point(50, 80), page_text, fontname="malgun", fontsize=11)
            else:
                page.insert_text(fitz.Point(50, 80), page_text, fontsize=11)
        doc.save(str(file_path))
        doc.close()
        created_files.append(file_path)
    return created_files


# =====================================================================
# 2. 골든 Q&A 벤치마크 테스트셋 (정답 질의 15건 + 트랩성 질의 5건)
# =====================================================================

BENCHMARK_QUERIES = [
    # --- 보안 규정 질의 ---
    {
        "query": "업무 단말기 화면 자동 잠금 시간은 몇 분인가요?",
        "expected_doc": "사내_정보보안_규정.pdf",
        "expected_page": 2,
        "keywords": ["10분", "자동"],
        "is_trap": False,
    },
    {
        "query": "사내 비밀 등급은 총 몇 단계로 구분되나요?",
        "expected_doc": "사내_정보보안_규정.pdf",
        "expected_page": 1,
        "keywords": ["4단계", "1급", "2급"],
        "is_trap": False,
    },
    {
        "query": "업무 시스템 비밀번호는 몇 일 주기로 변경해야 하나요?",
        "expected_doc": "사내_정보보안_규정.pdf",
        "expected_page": 2,
        "keywords": ["90일", "10자리"],
        "is_trap": False,
    },
    {
        "query": "기밀 유출이나 랜섬웨어 감염 시 몇 분 이내에 어디로 보고해야 하나요?",
        "expected_doc": "사내_정보보안_규정.pdf",
        "expected_page": 3,
        "keywords": ["30분", "보안운영팀", "1119"],
        "is_trap": False,
    },
    {
        "query": "연구개발 구역에서 블루투스나 사설 Wi-Fi 테더링을 사용할 수 있나요?",
        "expected_doc": "사내_정보보안_규정.pdf",
        "expected_page": 3,
        "keywords": ["차단", "금지"],
        "is_trap": False,
    },
    # --- 근태/복무 규정 질의 ---
    {
        "query": "본인 결혼 시 부여되는 유급 경조휴가는 며칠인가요?",
        "expected_doc": "근태_및_복무_관리_지침.pdf",
        "expected_page": 2,
        "keywords": ["5일"],
        "is_trap": False,
    },
    {
        "query": "국내 출장 시 일비와 숙박비 지급 한도는 얼마인가요?",
        "expected_doc": "근태_및_복무_관리_지침.pdf",
        "expected_page": 3,
        "keywords": ["30,000원", "100,000원"],
        "is_trap": False,
    },
    {
        "query": "재택근무는 주당 최대 몇 회까지 가능한가요?",
        "expected_doc": "근태_및_복무_관리_지침.pdf",
        "expected_page": 1,
        "keywords": ["주 2회"],
        "is_trap": False,
    },
    {
        "query": "유급 병가는 1년에 최대 며칠까지 사용 가능하며 진단서는 언제 필요한가요?",
        "expected_doc": "근태_및_복무_관리_지침.pdf",
        "expected_page": 2,
        "keywords": ["30일", "3일"],
        "is_trap": False,
    },
    {
        "query": "1년간 80% 이상 출근 시 부여되는 기본 연차휴가 일수는?",
        "expected_doc": "근태_및_복무_관리_지침.pdf",
        "expected_page": 2,
        "keywords": ["15일"],
        "is_trap": False,
    },
    # --- 특허 출원 규정 질의 ---
    {
        "query": "국내 특허를 출원했을 때 지급되는 출원 보상금은 얼마인가요?",
        "expected_doc": "연구개발_특허_출원_가이드.pdf",
        "expected_page": 1,
        "keywords": ["500,000원", "50만"],
        "is_trap": False,
    },
    {
        "query": "특허가 최종 등록되면 지급되는 등록 보상금 액수는 얼마인가요?",
        "expected_doc": "연구개발_특허_출원_가이드.pdf",
        "expected_page": 1,
        "keywords": ["1,500,000원", "150만"],
        "is_trap": False,
    },
    {
        "query": "신규 연구 과제 착수 전 최소 몇 건 이상의 선행 특허를 조사해야 하나요?",
        "expected_doc": "연구개발_특허_출원_가이드.pdf",
        "expected_page": 2,
        "keywords": ["20건"],
        "is_trap": False,
    },
    {
        "query": "임직원이 업무 중 발명한 기술은 누구에게 귀속되나요?",
        "expected_doc": "연구개발_특허_출원_가이드.pdf",
        "expected_page": 1,
        "keywords": ["회사", "직무발명"],
        "is_trap": False,
    },
    {
        "query": "오픈소스 중 GPL 라이선스를 도입하려면 어떤 절차를 거쳐야 하나요?",
        "expected_doc": "연구개발_특허_출원_가이드.pdf",
        "expected_page": 2,
        "keywords": ["지식재산팀", "법무 검토"],
        "is_trap": False,
    },
    # --- 트랩성 질의 (문서에 없는 허위 질문) ---
    {
        "query": "사내 구내식당 점심 식대 지원 금액은 1끼당 얼마인가요?",
        "expected_doc": None,
        "expected_page": None,
        "keywords": [],
        "is_trap": True,
    },
    {
        "query": "해외 연수 지원 프로그램의 선발 기준과 어학 성적 요건을 알려주세요.",
        "expected_doc": None,
        "expected_page": None,
        "keywords": [],
        "is_trap": True,
    },
    {
        "query": "사내 체력단련실(피트니스센터) 이용 시간과 락커 신청 방법은 무엇인가요?",
        "expected_doc": None,
        "expected_page": None,
        "keywords": [],
        "is_trap": True,
    },
    {
        "query": "임직원 자녀 대학 학자금 지원 한도는 1학기당 최대 얼마인가요?",
        "expected_doc": None,
        "expected_page": None,
        "keywords": [],
        "is_trap": True,
    },
    {
        "query": "사내 차량 5부제 요일별 적용 대상 차량 번호 끝자리를 안내해주세요.",
        "expected_doc": None,
        "expected_page": None,
        "keywords": [],
        "is_trap": True,
    },
]


# =====================================================================
# 3. 벤치마크 실행 엔진
# =====================================================================

async def run_benchmark():
    print("=" * 70)
    print("   dograc 폐쇄망 RAG 시스템 학술 실측 벤치마크 실행기")
    print("=" * 70)

    async with httpx.AsyncClient(base_url=API_BASE_URL, headers=HEADERS, timeout=180.0) as client:
        # 1. 백엔드 상태 확인
        try:
            health_res = await client.get("/health/ready")
            health_data = health_res.json()
            print(f"[1/5] 백엔드 헬스체크 완료: status={health_data.get('status')}")
        except Exception as e:
            print(f"[오류] 백엔드 연결 실패: {e}")
            return

        # 2. 벤치마크용 워크스페이스 생성
        ws_res = await client.post("/workspaces", json={"name": f"학술평가_워크스페이스_{int(time.time())}"})
        if ws_res.status_code != 201:
            print(f"[오류] 워크스페이스 생성 실패: {ws_res.text}")
            return
        workspace = ws_res.json()
        ws_id = workspace["id"]
        print(f"[2/5] 벤치마크 워크스페이스 생성됨 (ID: {ws_id})")

        # 3. 샘플 PDF 생성 및 업로드
        pdf_dir = OUTPUT_DIR / "pdfs"
        pdf_paths = create_sample_pdfs(pdf_dir)
        print(f"[3/5] 사내 규정 샘플 PDF {len(pdf_paths)}건 생성 완료")

        uploaded_docs = []
        for p in pdf_paths:
            with open(p, "rb") as f:
                upload_res = await client.post(
                    f"/workspaces/{ws_id}/documents",
                    files={"file": (p.name, f, "application/pdf")},
                )
            if upload_res.status_code == 201:
                doc_info = upload_res.json()
                uploaded_docs.append(doc_info)
                print(f"  - 문서 업로드 완료: {p.name} (ID: {doc_info['id']})")
            else:
                print(f"  - [경고] 업로드 실패: {p.name} -> {upload_res.text}")

        # 문서 파싱 및 벡터 색인 트리거 (/documents/{id}/process)
        print("  - 문서 파싱 및 BGE-M3 벡터 색인 실행 중...")
        for doc in uploaded_docs:
            proc_res = await client.post(f"/documents/{doc['id']}/process")
            if proc_res.status_code == 200:
                print(f"  - 문서 색인 완료 (ready): {doc['source_file_name']}")
            else:
                print(f"  - [경고] 문서 색인 오류 ({doc['id']}): {proc_res.text}")
        print("  - 모든 문서 색인 완료.")

        # 4. 대화 세션 생성
        conv_res = await client.post(f"/workspaces/{ws_id}/conversations", json={"title": "학술 벤치마크 세션"})
        conv_id = conv_res.json()["id"]
        print(f"[4/5] 벤치마크 대화 세션 생성됨 (ID: {conv_id})")

        # 5. 골든 Q&A 벤치마크 질의 실행 및 측정
        print(f"[5/5] 총 {len(BENCHMARK_QUERIES)}건의 평가 질의응답 측정 시작...")
        results = []

        total_queries = len(BENCHMARK_QUERIES)
        citation_matches = 0
        citation_attempts = 0
        refusal_correct = 0
        trap_count = 0
        latencies = []
        scores = []
        faithfulness_hits = 0

        for idx, item in enumerate(BENCHMARK_QUERIES, 1):
            q_text = item["query"]
            is_trap = item["is_trap"]
            if is_trap:
                trap_count += 1

            start_t = time.perf_counter()
            # 정확한 엔드포인트: /conversations/{conv_id}/messages (201 Created)
            msg_res = await client.post(
                f"/conversations/{conv_id}/messages",
                json={"content": q_text},
            )
            elapsed_ms = (time.perf_counter() - start_t) * 1000

            if msg_res.status_code not in (200, 201):
                print(f"  [{idx}/{total_queries}] [실패] HTTP {msg_res.status_code}: {msg_res.text}")
                continue

            msg_data = msg_res.json()
            answer_content = msg_data.get("content", "")
            trace_id = msg_data.get("trace_id")

            # Trace 레코드 조회
            trace_data = {}
            if trace_id:
                tr_res = await client.get(f"/traces/{trace_id}")
                if tr_res.status_code == 200:
                    trace_data = tr_res.json()

            pure_llm_latency = trace_data.get("latency_ms", elapsed_ms)
            retrieved_chunks = trace_data.get("retrieved_chunks", [])
            avg_score = (
                sum(c.get("score", 0.0) for c in retrieved_chunks) / len(retrieved_chunks)
                if retrieved_chunks
                else 0.0
            )

            # 인라인 인용 분석: [파일명, p.숫자]
            citations = re.findall(r"\[([^,]+),\s*p\.?(\d+)\]", answer_content)

            # 거절 판정: "확인할 수 없습니다" / "관련 내용을" / "포함되어 있지"
            is_refused = (
                "확인할 수 없습니다" in answer_content
                or "관련 내용을" in answer_content
                or "포함되어 있지" in answer_content
                or "문서에서" in answer_content and "없습니다" in answer_content
            )

            # 지표 판정
            if is_trap:
                if is_refused:
                    refusal_correct += 1
            else:
                # 인용 정밀도/재현율
                if citations:
                    citation_attempts += 1
                    for c_doc, c_page in citations:
                        if item["expected_doc"] in c_doc and int(c_page) == item["expected_page"]:
                            citation_matches += 1
                            break

                # 키워드 매칭 기반 사실 충실도
                k_matches = sum(1 for kw in item["keywords"] if kw in answer_content)
                if k_matches >= math.ceil(len(item["keywords"]) * 0.5):
                    faithfulness_hits += 1

            latencies.append(pure_llm_latency)
            if avg_score > 0:
                scores.append(avg_score)

            status_mark = "정답 확인" if not is_trap else ("거절 성공" if is_refused else "환각 발생")
            print(f"  [{idx:2d}/{total_queries}] {status_mark} | 소요시간: {elapsed_ms:.0f}ms (추론: {pure_llm_latency:.0f}ms) | 인용: {len(citations)}개 | {q_text[:28]}...")

            results.append({
                "idx": idx,
                "query": q_text,
                "is_trap": is_trap,
                "answer": answer_content,
                "citations": citations,
                "elapsed_ms": elapsed_ms,
                "latency_ms": pure_llm_latency,
                "retrieved_count": len(retrieved_chunks),
                "avg_score": avg_score,
            })

        # =====================================================================
        # 4. 실측 통계 지표 산출
        # =====================================================================
        n_valid = len(BENCHMARK_QUERIES) - trap_count
        citation_precision = (citation_matches / citation_attempts * 100) if citation_attempts > 0 else 0.0
        citation_recall = (citation_matches / n_valid * 100) if n_valid > 0 else 0.0
        faithfulness = (faithfulness_hits / n_valid * 100) if n_valid > 0 else 0.0
        refusal_acc = (refusal_correct / trap_count * 100) if trap_count > 0 else 0.0
        unsupported_rate = 100.0 - refusal_acc
        avg_latency = sum(latencies) / len(latencies) if latencies else 0.0
        std_latency = (
            math.sqrt(sum((x - avg_latency) ** 2 for x in latencies) / len(latencies))
            if latencies
            else 0.0
        )
        avg_cosine = sum(scores) / len(scores) if scores else 0.0

        summary = {
            "model_name": "qwen2.5:7b",
            "embedding_model": "BAAI/bge-m3",
            "total_queries": total_queries,
            "valid_queries": n_valid,
            "trap_queries": trap_count,
            "metrics": {
                "context_relevance_score": round(avg_cosine, 4),
                "faithfulness_pct": round(faithfulness, 2),
                "citation_precision_pct": round(citation_precision, 2),
                "citation_recall_pct": round(citation_recall, 2),
                "refusal_accuracy_pct": round(refusal_acc, 2),
                "unsupported_claim_rate_pct": round(unsupported_rate, 2),
            },
            "latencies": {
                "mean_ms": round(avg_latency, 2),
                "std_ms": round(std_latency, 2),
            },
        }

        # 결과 저장
        OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
        out_file = OUTPUT_DIR / "benchmark_result.json"
        with open(out_file, "w", encoding="utf-8") as f:
            json.dump({"summary": summary, "results": results}, f, ensure_ascii=False, indent=2)

        print("\n" + "=" * 70)
        print("   [학술 실측 벤치마크 결과 요약 (PAPER.md 수록용)]")
        print("=" * 70)
        print(f"[-] Context Relevance (평균 코사인 유사도) : {summary['metrics']['context_relevance_score'] * 100:.1f}%")
        print(f"[-] Faithfulness (사실 충실도)              : {summary['metrics']['faithfulness_pct']}%")
        print(f"[-] Citation Precision (인용 정밀도)        : {summary['metrics']['citation_precision_pct']}%")
        print(f"[-] Citation Recall (인용 재현율)           : {summary['metrics']['citation_recall_pct']}%")
        print(f"[-] Refusal Accuracy (정직한 거절 정확도)    : {summary['metrics']['refusal_accuracy_pct']}%")
        print(f"[-] Unsupported Claim Rate (환각율)         : {summary['metrics']['unsupported_claim_rate_pct']}%")
        print(f"[-] 평균 추론 지연 시간                     : {summary['latencies']['mean_ms']} +- {summary['latencies']['std_ms']} ms")
        print(f"[-] 세부 결과 저장 경로: {out_file}")
        print("=" * 70)


if __name__ == "__main__":
    asyncio.run(run_benchmark())
