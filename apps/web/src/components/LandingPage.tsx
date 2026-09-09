"use client";

import { useState } from "react";
import Image from "next/image";
import { AuthModal } from "@/components/AuthModal";

interface LandingPageProps {
  onOpenAuth?: () => void;
}

export function LandingPage({ onOpenAuth }: LandingPageProps) {
  const [showAuthModal, setShowAuthModal] = useState(false);

  const handleOpenAuth = () => {
    if (onOpenAuth) {
      onOpenAuth();
    } else {
      setShowAuthModal(true);
    }
  };

  return (
    <div className="min-h-screen w-full bg-kds-gray-50 text-kds-gray-900 flex flex-col font-sans selection:bg-kds-blue-100 selection:text-kds-blue-900 scroll-smooth">
      {/* 상단 네비게이션 헤더 */}
      <header className="h-16 sm:h-[72px] bg-white/95 backdrop-blur-md border-b border-kds-gray-300 sticky top-0 z-40 flex-shrink-0">
        <div className="max-w-[1280px] mx-auto h-full px-6 sm:px-8 flex items-center justify-between">
          {/* 좌측 로고 및 브랜드 */}
          <div className="flex items-center space-x-3">
            <Image
              src="/logo.svg"
              alt="dograc 로고"
              width={36}
              height={36}
              className="w-9 h-9 rounded-md"
              priority
            />
            <div className="flex items-center space-x-2">
              <span className="text-xl font-bold tracking-tight text-kds-gray-900 font-sans">
                dograc
              </span>
              <span className="text-xs font-semibold text-kds-blue-700 bg-kds-blue-50 border border-kds-blue-200 px-2 py-0.5 rounded">
                RAG Platform
              </span>
            </div>
          </div>

          {/* 중앙 네비게이션 링크 */}
          <nav className="hidden md:flex items-center space-x-8 text-sm font-medium text-kds-gray-600">
            <a href="#features" className="hover:text-kds-blue-700 transition-colors">
              기능 소개
            </a>
            <a href="#workflow" className="hover:text-kds-blue-700 transition-colors">
              동작 원리
            </a>
            <a href="#architecture" className="hover:text-kds-blue-700 transition-colors">
              아키텍처
            </a>
          </nav>

          {/* 우측 로그인 액션 */}
          <div className="flex items-center space-x-3">
            <button
              onClick={handleOpenAuth}
              className="h-10 px-5 text-sm font-semibold bg-kds-blue-600 hover:bg-kds-blue-800 text-white rounded-lg transition-colors shadow-sm"
            >
              로그인
            </button>
          </div>
        </div>
      </header>

      {/* Hero Section */}
      <section className="pt-20 pb-24 px-6 sm:px-8 max-w-[1280px] mx-auto w-full text-center">
        <div className="inline-flex items-center space-x-2 bg-white border border-kds-gray-300 px-3.5 py-1.5 rounded-full text-xs text-kds-gray-700 mb-8 shadow-sm">
          <span className="w-2 h-2 rounded-full bg-kds-blue-600 animate-pulse" />
          <span className="font-medium">투명하고 검증 가능한 지식 검색 RAG 파이프라인</span>
        </div>

        <h1 className="text-3xl sm:text-5xl md:text-6xl font-extrabold tracking-tight text-kds-gray-900 leading-[1.2] max-w-4xl mx-auto">
          문서 속 진실을 투명하게 증명하는
          <br />
          <span className="text-kds-blue-600">신뢰 기반 RAG 어시스턴트</span>
        </h1>

        <p className="mt-6 text-sm sm:text-base md:text-lg text-kds-gray-600 max-w-2xl mx-auto leading-relaxed">
          환각(Hallucination) 없는 인라인 출처 인용, 검색 및 생성 전 과정의 투명한 감사 로그,
          로컬 LLM과 벡터 데이터베이스 기반의 완벽한 사내 데이터 격리를 제공합니다.
        </p>

        <div className="mt-10 flex flex-col sm:flex-row items-center justify-center gap-3">
          <button
            onClick={handleOpenAuth}
            className="w-full sm:w-auto h-12 px-8 text-base font-semibold bg-kds-blue-600 hover:bg-kds-blue-800 text-white rounded-lg transition-colors shadow-sm"
          >
            워크스페이스 시작하기
          </button>
          <a
            href="#features"
            className="w-full sm:w-auto h-12 px-6 text-base font-medium bg-white hover:bg-kds-gray-100 text-kds-gray-700 border border-kds-gray-300 rounded-lg transition-colors flex items-center justify-center"
          >
            기능 둘러보기
          </a>
        </div>

        {/* Hero Visual Asset Card */}
        <div className="mt-16 max-w-5xl mx-auto rounded-xl border border-kds-gray-300 bg-white overflow-hidden shadow-dropdown">
          <div className="h-11 bg-kds-gray-100 border-b border-kds-gray-300 px-5 flex items-center justify-between text-xs text-kds-gray-600 font-mono">
            <div className="flex items-center space-x-2">
              <span className="w-3 h-3 rounded-full bg-kds-gray-300" />
              <span className="w-3 h-3 rounded-full bg-kds-gray-300" />
              <span className="w-3 h-3 rounded-full bg-kds-gray-300" />
              <span className="ml-2 text-kds-gray-800 font-sans font-medium text-xs">
                dograc Engine Workspace
              </span>
            </div>
            <div className="hidden sm:flex items-center space-x-3 text-[11px]">
              <span>LLM: qwen2.5:7b</span>
              <span>•</span>
              <span>Embeddings: BAAI/bge-m3</span>
              <span>•</span>
              <span className="text-kds-blue-700 font-semibold">Qdrant: Connected</span>
            </div>
          </div>

          <div className="relative aspect-[16/9] w-full bg-kds-gray-900">
            <Image
              src="/hero-illustration.jpg"
              alt="dograc RAG 파이프라인 아키텍처 다이어그램"
              fill
              className="object-cover"
              priority
            />
          </div>
        </div>
      </section>

      {/* 3대 핵심 가치 (Core Pillars) */}
      <section id="features" className="py-24 bg-white border-y border-kds-gray-300">
        <div className="max-w-[1280px] mx-auto px-6 sm:px-8">
          <div className="text-center max-w-2xl mx-auto mb-16">
            <span className="text-xs font-bold uppercase tracking-wider text-kds-blue-700">
              Core Principles
            </span>
            <h2 className="text-2xl sm:text-3xl font-bold tracking-tight text-kds-gray-900 mt-2">
              타협 없는 신뢰와 투명성을 위한 3대 설계 원칙
            </h2>
            <p className="text-sm text-kds-gray-600 mt-3 leading-relaxed">
              블랙박스 AI의 한계를 넘어, 모든 답변의 출처를 검증하고 내부 실행 과정을 투명하게 추적합니다.
            </p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-6 sm:gap-8">
            {/* 카드 1: 인라인 인용 */}
            <div className="rounded-lg border border-kds-gray-300 bg-kds-gray-50 p-6 sm:p-7 flex flex-col justify-between hover:border-kds-blue-600 transition-colors">
              <div>
                <div className="text-xs font-mono font-bold text-kds-blue-700 mb-3">01 / CITATIONS</div>
                <h3 className="text-lg sm:text-xl font-bold text-kds-gray-900">
                  신뢰 기반 인라인 인용
                </h3>
                <p className="text-sm text-kds-gray-600 mt-3 leading-relaxed">
                  답변에 포함된 모든 주장은 검색된 원문 청크에 기반합니다.
                  정규화된 <code className="text-kds-blue-700 bg-kds-blue-50 px-1.5 py-0.5 rounded text-xs font-mono">[파일명, p.숫자]</code> 뱃지를 통해
                  사용자가 즉시 원문과 근거를 교차 검증할 수 있습니다.
                </p>
              </div>

              <div className="mt-8 p-4 bg-white rounded-lg border border-kds-gray-300 text-xs space-y-2.5 shadow-sm">
                <div className="text-[11px] font-semibold text-kds-gray-500 uppercase tracking-wide">생성된 답변 미리보기</div>
                <p className="text-kds-gray-800 leading-relaxed">
                  2026년도 RAG 파이프라인의 핵심 목표는 투명성 확보입니다
                  <span className="ml-1.5 inline-block bg-kds-blue-50 text-kds-blue-700 border border-kds-blue-200 px-2 py-0.5 text-[11px] font-semibold rounded">
                    [백서.pdf, p.4]
                  </span>.
                </p>
              </div>
            </div>

            {/* 카드 2: 파이프라인 투명성 */}
            <div className="rounded-lg border border-kds-gray-300 bg-kds-gray-50 p-6 sm:p-7 flex flex-col justify-between hover:border-kds-blue-600 transition-colors">
              <div>
                <div className="text-xs font-mono font-bold text-kds-blue-700 mb-3">02 / TRANSPARENCY</div>
                <h3 className="text-lg sm:text-xl font-bold text-kds-gray-900">
                  파이프라인 실행 감사 (Trace)
                </h3>
                <p className="text-sm text-kds-gray-600 mt-3 leading-relaxed">
                  검색 단계와 생성 단계를 불투명한 체인으로 감추지 않습니다.
                  청크 코사인 유사도 점수, 토큰 사용량, 지연 시간, 프롬프트 전문을
                  언제든 Trace 사이드 드로어에서 직접 열람할 수 있습니다.
                </p>
              </div>

              <div className="mt-8 p-4 bg-white rounded-lg border border-kds-gray-300 text-xs space-y-2 font-mono shadow-sm">
                <div className="text-[11px] font-semibold text-kds-gray-500 font-sans uppercase tracking-wide">실행 지표 (Metrics)</div>
                <div className="flex justify-between text-kds-gray-700 py-0.5 border-b border-kds-gray-100">
                  <span>Cosine Similarity:</span>
                  <span className="font-bold text-kds-blue-700">0.894</span>
                </div>
                <div className="flex justify-between text-kds-gray-700 py-0.5">
                  <span>Total Latency:</span>
                  <span className="font-bold text-kds-gray-900">324 ms</span>
                </div>
              </div>
            </div>

            {/* 카드 3: 완전한 격리 및 보안 */}
            <div className="rounded-lg border border-kds-gray-300 bg-kds-gray-50 p-6 sm:p-7 flex flex-col justify-between hover:border-kds-blue-600 transition-colors">
              <div>
                <div className="text-xs font-mono font-bold text-kds-blue-700 mb-3">03 / ISOLATION</div>
                <h3 className="text-lg sm:text-xl font-bold text-kds-gray-900">
                  철저한 데이터 격리 및 온프레미스
                </h3>
                <p className="text-sm text-kds-gray-600 mt-3 leading-relaxed">
                  모든 문서는 사용자 및 워크스페이스 단위로 엄격히 논리적 격리됩니다.
                  외부 API 종속 없이 사내 Ollama 및 vLLM 로컬 추론 엔진을 연동하여
                  기업 기밀 문서의 외부 유출 가능성을 원천 차단합니다.
                </p>
              </div>

              <div className="mt-8 p-4 bg-white rounded-lg border border-kds-gray-300 text-xs space-y-2 text-kds-gray-700 shadow-sm">
                <div className="flex items-center space-x-2 text-[11px] font-semibold text-kds-blue-700">
                  <span>✓</span>
                  <span>사용자 및 워크스페이스 단위 완전 격리</span>
                </div>
                <div className="flex items-center space-x-2 text-[11px] font-semibold text-kds-blue-700">
                  <span>✓</span>
                  <span>로컬 Ollama &amp; vLLM 오픈소스 어댑터 호환</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* 동작 원리 (4-Step Workflow) */}
      <section id="workflow" className="py-24 max-w-[1280px] mx-auto px-6 sm:px-8 w-full">
        <div className="text-center max-w-2xl mx-auto mb-16">
          <span className="text-xs font-bold uppercase tracking-wider text-kds-blue-700">
            Workflow
          </span>
          <h2 className="text-2xl sm:text-3xl font-bold tracking-tight text-kds-gray-900 mt-2">
            체계적인 4단계 RAG 파이프라인
          </h2>
          <p className="text-sm text-kds-gray-600 mt-3 leading-relaxed">
            문서 업로드부터 색인, 정밀 검색, 그리고 근거 기반 답변 생성까지 유기적으로 이어집니다.
          </p>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5">
          <div className="p-6 rounded-lg border border-kds-gray-300 bg-white space-y-3 hover:border-kds-blue-600 transition-colors shadow-sm">
            <div className="text-xs font-mono font-bold text-kds-blue-700">STEP 01</div>
            <h4 className="text-base font-bold text-kds-gray-900">문서 수집 및 청킹</h4>
            <p className="text-xs sm:text-sm text-kds-gray-600 leading-relaxed">
              PDF, 텍스트 문서를 수집하여 문맥 손실을 최소화하는 청크 분할 및 메타데이터를 추출합니다.
            </p>
          </div>

          <div className="p-6 rounded-lg border border-kds-gray-300 bg-white space-y-3 hover:border-kds-blue-600 transition-colors shadow-sm">
            <div className="text-xs font-mono font-bold text-kds-blue-700">STEP 02</div>
            <h4 className="text-base font-bold text-kds-gray-900">고밀도 벡터 색인</h4>
            <p className="text-xs sm:text-sm text-kds-gray-600 leading-relaxed">
              BAAI/bge-m3 임베딩 모델로 다차원 벡터를 계산하고 고성능 Qdrant 벡터 저장소에 색인합니다.
            </p>
          </div>

          <div className="p-6 rounded-lg border border-kds-gray-300 bg-white space-y-3 hover:border-kds-blue-600 transition-colors shadow-sm">
            <div className="text-xs font-mono font-bold text-kds-blue-700">STEP 03</div>
            <h4 className="text-base font-bold text-kds-gray-900">문맥 유사도 검색</h4>
            <p className="text-xs sm:text-sm text-kds-gray-600 leading-relaxed">
              사용자의 질문과 코사인 유사도가 가장 높은 상위 K개의 핵심 근거 청크를 정밀하게 추출합니다.
            </p>
          </div>

          <div className="p-6 rounded-lg border border-kds-gray-300 bg-white space-y-3 hover:border-kds-blue-600 transition-colors shadow-sm">
            <div className="text-xs font-mono font-bold text-kds-blue-700">STEP 04</div>
            <h4 className="text-base font-bold text-kds-gray-900">근거 기반 답변 생성</h4>
            <p className="text-xs sm:text-sm text-kds-gray-600 leading-relaxed">
              추출된 문맥만을 토대로 환각 없이 답변을 생성하며, 문맥 부재 시 정직하게 거절합니다.
            </p>
          </div>
        </div>
      </section>

      {/* 아키텍처 스펙 섹션 (Architecture) */}
      <section id="architecture" className="py-20 bg-kds-gray-100/70 border-t border-kds-gray-300">
        <div className="max-w-[1280px] mx-auto px-6 sm:px-8">
          <div className="text-center max-w-2xl mx-auto mb-12">
            <span className="text-xs font-bold uppercase tracking-wider text-kds-blue-700">
              Architecture &amp; Specs
            </span>
            <h2 className="text-2xl sm:text-3xl font-bold tracking-tight text-kds-gray-900 mt-2">
              엔터프라이즈 모듈형 기술 스택
            </h2>
          </div>

          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-center">
            <div className="p-5 bg-white border border-kds-gray-300 rounded-lg shadow-sm">
              <div className="text-xs text-kds-gray-500 font-medium">Core Backend</div>
              <div className="text-base font-bold text-kds-gray-900 mt-1">FastAPI (Async)</div>
              <div className="text-[11px] text-kds-gray-500 mt-1">Python 3.11+</div>
            </div>
            <div className="p-5 bg-white border border-kds-gray-300 rounded-lg shadow-sm">
              <div className="text-xs text-kds-gray-500 font-medium">Frontend UI</div>
              <div className="text-base font-bold text-kds-gray-900 mt-1">Next.js 14</div>
              <div className="text-[11px] text-kds-gray-500 mt-1">KDS Design System</div>
            </div>
            <div className="p-5 bg-white border border-kds-gray-300 rounded-lg shadow-sm">
              <div className="text-xs text-kds-gray-500 font-medium">Vector Database</div>
              <div className="text-base font-bold text-kds-gray-900 mt-1">Qdrant Engine</div>
              <div className="text-[11px] text-kds-gray-500 mt-1">Cosine Metric</div>
            </div>
            <div className="p-5 bg-white border border-kds-gray-300 rounded-lg shadow-sm">
              <div className="text-xs text-kds-gray-500 font-medium">LLM Engine</div>
              <div className="text-base font-bold text-kds-gray-900 mt-1">Ollama / vLLM</div>
              <div className="text-[11px] text-kds-gray-500 mt-1">Local &amp; Open Source</div>
            </div>
          </div>
        </div>
      </section>

      {/* 하단 시작하기 CTA */}
      <section className="py-20 bg-kds-gray-900 text-white text-center px-6 sm:px-8">
        <div className="max-w-2xl mx-auto space-y-6">
          <h2 className="text-2xl sm:text-3xl md:text-4xl font-bold tracking-tight leading-snug">
            지금 바로 신뢰할 수 있는 RAG 파이프라인을 경험하세요
          </h2>
          <p className="text-sm sm:text-base text-kds-gray-400 leading-relaxed">
            별도의 복잡한 절차 없이 데모 계정으로 즉시 로그인하여 사내 문서를 보관하고 질의응답을 시작할 수 있습니다.
          </p>
          <div className="pt-2">
            <button
              onClick={handleOpenAuth}
              className="h-12 px-8 text-base font-semibold bg-kds-blue-600 hover:bg-kds-blue-500 text-white rounded-lg transition-colors shadow-sm"
            >
              로그인하고 시작하기
            </button>
          </div>
        </div>
      </section>

      {/* 푸터 */}
      <footer className="py-10 bg-white border-t border-kds-gray-300 text-center text-xs text-kds-gray-500">
        <div className="max-w-[1280px] mx-auto px-6 sm:px-8 flex flex-col sm:flex-row items-center justify-between gap-4">
          <div className="flex items-center space-x-2.5">
            <Image
              src="/logo.svg"
              alt="dograc 로고"
              width={20}
              height={20}
              className="w-5 h-5 rounded"
            />
            <span className="font-bold text-kds-gray-900 text-sm">dograc</span>
            <span>•</span>
            <span>KDS Reading-First Enterprise RAG System</span>
          </div>
          <div>
            <span>© 2026 dograc. All rights reserved.</span>
          </div>
        </div>
      </footer>

      {/* AuthModal 연동 */}
      <AuthModal
        isOpen={showAuthModal}
        onClose={() => setShowAuthModal(false)}
      />
    </div>
  );
}
