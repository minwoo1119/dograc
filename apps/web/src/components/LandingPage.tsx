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
                지식 검색 플랫폼
              </span>
            </div>
          </div>

          {/* 중앙 네비게이션 링크 */}
          <nav className="hidden md:flex items-center space-x-8 text-sm font-medium text-kds-gray-600">
            <a href="#features" className="hover:text-kds-blue-700 transition-colors">
              주요 특징
            </a>
            <a href="#workflow" className="hover:text-kds-blue-700 transition-colors">
              처리 과정
            </a>
            <a href="#architecture" className="hover:text-kds-blue-700 transition-colors">
              기술 구성
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
          <span className="font-medium">검증 가능한 사내 문서 지식 검색 플랫폼</span>
        </div>

        <h1 className="text-3xl sm:text-5xl md:text-6xl font-extrabold tracking-tight text-kds-gray-900 leading-[1.2] max-w-4xl mx-auto">
          문서 속 근거를 투명하게 증명하는
          <br />
          <span className="text-kds-blue-600">신뢰 기반 지식 어시스턴트</span>
        </h1>

        <p className="mt-6 text-sm sm:text-base md:text-lg text-kds-gray-600 max-w-2xl mx-auto leading-relaxed">
          답변의 모든 문장에 원문 출처를 연결하고, 검색부터 생성까지 전 과정을 투명하게 기록합니다.
          로컬 모델 연동을 완벽히 지원하여 기업의 핵심 문서를 안전하게 지킵니다.
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
            주요 특징 살펴보기
          </a>
        </div>

        {/* Hero Visual Asset Card */}
        <div className="mt-16 max-w-5xl mx-auto rounded-xl border border-kds-gray-300 bg-white overflow-hidden shadow-dropdown">
          <div className="h-11 bg-kds-gray-100 border-b border-kds-gray-300 px-5 flex items-center justify-between text-xs text-kds-gray-600">
            <div className="flex items-center space-x-2">
              <span className="w-3 h-3 rounded-full bg-kds-gray-300" />
              <span className="w-3 h-3 rounded-full bg-kds-gray-300" />
              <span className="w-3 h-3 rounded-full bg-kds-gray-300" />
              <span className="ml-2 text-kds-gray-800 font-medium text-xs">
                사내 문서 검색 및 분석 작업대
              </span>
            </div>
            <div className="hidden sm:flex items-center space-x-3 text-[11px] text-kds-gray-500">
              <span>답변 모델: Qwen 2.5</span>
              <span>•</span>
              <span>임베딩: BGE-M3</span>
              <span>•</span>
              <span className="text-kds-blue-700 font-semibold">벡터 저장소: Qdrant 연결됨</span>
            </div>
          </div>

          <div className="relative aspect-[16/9] w-full bg-kds-gray-900">
            <Image
              src="/hero-illustration.jpg"
              alt="dograc 문서 검색 파이프라인 구조도"
              fill
              className="object-cover"
              priority
            />
          </div>
        </div>
      </section>

      {/* 3대 핵심 가치 */}
      <section id="features" className="py-24 bg-white border-y border-kds-gray-300">
        <div className="max-w-[1280px] mx-auto px-6 sm:px-8">
          <div className="text-center max-w-2xl mx-auto mb-16">
            <span className="text-xs font-bold text-kds-blue-700">
              핵심 가치
            </span>
            <h2 className="text-2xl sm:text-3xl font-bold tracking-tight text-kds-gray-900 mt-2">
              신뢰할 수 있는 답변을 위한 세 가지 원칙
            </h2>
            <p className="text-sm text-kds-gray-600 mt-3 leading-relaxed">
              블랙박스 인공지능의 한계를 넘어, 모든 답변의 근거를 밝히고 실행 과정을 투명하게 공개합니다.
            </p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-6 sm:gap-8">
            {/* 카드 1: 인라인 인용 */}
            <div className="rounded-lg border border-kds-gray-300 bg-kds-gray-50 p-6 sm:p-7 flex flex-col justify-between hover:border-kds-blue-600 transition-colors">
              <div>
                <div className="text-xs font-bold text-kds-blue-700 mb-3">01 투명한 출처 표기</div>
                <h3 className="text-lg sm:text-xl font-bold text-kds-gray-900">
                  근거 문서를 바로 확인하는 인라인 인용
                </h3>
                <p className="text-sm text-kds-gray-600 mt-3 leading-relaxed">
                  답변에 포함된 모든 주장에 <code className="text-kds-blue-700 bg-kds-blue-50 px-1.5 py-0.5 rounded text-xs">[파일명, p.숫자]</code> 형태의 인용 태그가 붙습니다.
                  이를 통해 원문의 어느 문맥을 참고했는지 즉시 교차 검증할 수 있습니다.
                </p>
              </div>

              <div className="mt-8 p-4 bg-white rounded-lg border border-kds-gray-300 text-xs space-y-2.5 shadow-sm">
                <div className="text-[11px] font-semibold text-kds-gray-500">답변 예시</div>
                <p className="text-kds-gray-800 leading-relaxed">
                  2026년도 사내 검색 파이프라인의 핵심 목표는 투명성 확보입니다
                  <span className="ml-1.5 inline-block bg-kds-blue-50 text-kds-blue-700 border border-kds-blue-200 px-2 py-0.5 text-[11px] font-semibold rounded">
                    [백서.pdf, p.4]
                  </span>.
                </p>
              </div>
            </div>

            {/* 카드 2: 파이프라인 투명성 */}
            <div className="rounded-lg border border-kds-gray-300 bg-kds-gray-50 p-6 sm:p-7 flex flex-col justify-between hover:border-kds-blue-600 transition-colors">
              <div>
                <div className="text-xs font-bold text-kds-blue-700 mb-3">02 실행 과정 추적</div>
                <h3 className="text-lg sm:text-xl font-bold text-kds-gray-900">
                  모든 검색과 추론을 기록하는 실행 분석
                </h3>
                <p className="text-sm text-kds-gray-600 mt-3 leading-relaxed">
                  검색과 답변 생성을 불투명하게 감추지 않습니다.
                  검색된 문서의 유사도 점수, 토큰 사용량, 처리 시간, 실제 전달된 프롬프트 전문을
                  언제든 분석 화면에서 직접 확인할 수 있습니다.
                </p>
              </div>

              <div className="mt-8 p-4 bg-white rounded-lg border border-kds-gray-300 text-xs space-y-2 shadow-sm">
                <div className="text-[11px] font-semibold text-kds-gray-500">실행 분석 지표</div>
                <div className="flex justify-between text-kds-gray-700 py-0.5 border-b border-kds-gray-100">
                  <span>문서 유사도 점수:</span>
                  <span className="font-bold text-kds-blue-700 font-mono">0.894</span>
                </div>
                <div className="flex justify-between text-kds-gray-700 py-0.5">
                  <span>응답 소요 시간:</span>
                  <span className="font-bold text-kds-gray-900 font-mono">324 ms</span>
                </div>
              </div>
            </div>

            {/* 카드 3: 완전한 격리 및 보안 */}
            <div className="rounded-lg border border-kds-gray-300 bg-kds-gray-50 p-6 sm:p-7 flex flex-col justify-between hover:border-kds-blue-600 transition-colors">
              <div>
                <div className="text-xs font-bold text-kds-blue-700 mb-3">03 안전한 데이터 보호</div>
                <h3 className="text-lg sm:text-xl font-bold text-kds-gray-900">
                  사내 기밀을 지키는 격리 저장과 로컬 추론
                </h3>
                <p className="text-sm text-kds-gray-600 mt-3 leading-relaxed">
                  모든 문서는 사용자와 워크스페이스 단위로 안전하게 분리 보관됩니다.
                  외부 API 종속 없이 사내 로컬 추론 엔진(Ollama, vLLM)을 연동하여
                  기업 기밀의 외부 유출 가능성을 원천 차단합니다.
                </p>
              </div>

              <div className="mt-8 p-4 bg-white rounded-lg border border-kds-gray-300 text-xs space-y-2 text-kds-gray-700 shadow-sm">
                <div className="flex items-center space-x-2 text-[11px] font-semibold text-kds-blue-700">
                  <span>✓</span>
                  <span>사용자 및 워크스페이스 단위 완벽 격리</span>
                </div>
                <div className="flex items-center space-x-2 text-[11px] font-semibold text-kds-blue-700">
                  <span>✓</span>
                  <span>사내 폐쇄망 로컬 오픈소스 엔진 연동 지원</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* 동작 원리 (4단계 파이프라인) */}
      <section id="workflow" className="py-24 max-w-[1280px] mx-auto px-6 sm:px-8 w-full">
        <div className="text-center max-w-2xl mx-auto mb-16">
          <span className="text-xs font-bold text-kds-blue-700">
            처리 과정
          </span>
          <h2 className="text-2xl sm:text-3xl font-bold tracking-tight text-kds-gray-900 mt-2">
            문서 수집부터 답변 도출까지 4단계 파이프라인
          </h2>
          <p className="text-sm text-kds-gray-600 mt-3 leading-relaxed">
            문서를 업로드하면 자동으로 의미를 분석하고 색인하여 정확한 질의응답을 준비합니다.
          </p>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5">
          <div className="p-6 rounded-lg border border-kds-gray-300 bg-white space-y-3 hover:border-kds-blue-600 transition-colors shadow-sm">
            <div className="text-xs font-bold text-kds-blue-700">1단계</div>
            <h4 className="text-base font-bold text-kds-gray-900">문서 등록 및 본문 분할</h4>
            <p className="text-xs sm:text-sm text-kds-gray-600 leading-relaxed">
              PDF 및 텍스트 문서를 수집하여 문맥 손실을 최소화하는 청크로 분할하고 메타데이터를 추출합니다.
            </p>
          </div>

          <div className="p-6 rounded-lg border border-kds-gray-300 bg-white space-y-3 hover:border-kds-blue-600 transition-colors shadow-sm">
            <div className="text-xs font-bold text-kds-blue-700">2단계</div>
            <h4 className="text-base font-bold text-kds-gray-900">벡터 임베딩 및 색인</h4>
            <p className="text-xs sm:text-sm text-kds-gray-600 leading-relaxed">
              BGE-M3 임베딩 모델로 정밀한 의미 벡터를 계산하고 Qdrant 벡터 저장소에 고속 색인합니다.
            </p>
          </div>

          <div className="p-6 rounded-lg border border-kds-gray-300 bg-white space-y-3 hover:border-kds-blue-600 transition-colors shadow-sm">
            <div className="text-xs font-bold text-kds-blue-700">3단계</div>
            <h4 className="text-base font-bold text-kds-gray-900">질문 문맥 유사도 검색</h4>
            <p className="text-xs sm:text-sm text-kds-gray-600 leading-relaxed">
              사용자의 질문과 코사인 유사도가 가장 높은 상위 핵심 근거 문맥을 정확하게 찾아냅니다.
            </p>
          </div>

          <div className="p-6 rounded-lg border border-kds-gray-300 bg-white space-y-3 hover:border-kds-blue-600 transition-colors shadow-sm">
            <div className="text-xs font-bold text-kds-blue-700">4단계</div>
            <h4 className="text-base font-bold text-kds-gray-900">근거 기반 답변 생성</h4>
            <p className="text-xs sm:text-sm text-kds-gray-600 leading-relaxed">
              찾아낸 문맥만을 토대로 정확한 답변을 작성하며, 문서에 근거가 부족하면 솔직하게 답변 불가를 안내합니다.
            </p>
          </div>
        </div>
      </section>

      {/* 기술 구성 섹션 */}
      <section id="architecture" className="py-20 bg-kds-gray-100/70 border-t border-kds-gray-300">
        <div className="max-w-[1280px] mx-auto px-6 sm:px-8">
          <div className="text-center max-w-2xl mx-auto mb-12">
            <span className="text-xs font-bold text-kds-blue-700">
              기술 구성
            </span>
            <h2 className="text-2xl sm:text-3xl font-bold tracking-tight text-kds-gray-900 mt-2">
              검증된 오픈소스 기반 기술 스택
            </h2>
          </div>

          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-center">
            <div className="p-5 bg-white border border-kds-gray-300 rounded-lg shadow-sm">
              <div className="text-xs text-kds-gray-500 font-medium">백엔드 엔진</div>
              <div className="text-base font-bold text-kds-gray-900 mt-1">FastAPI 비동기 코어</div>
              <div className="text-[11px] text-kds-gray-500 mt-1">Python 3.11+</div>
            </div>
            <div className="p-5 bg-white border border-kds-gray-300 rounded-lg shadow-sm">
              <div className="text-xs text-kds-gray-500 font-medium">사용자 인터페이스</div>
              <div className="text-base font-bold text-kds-gray-900 mt-1">Next.js 14</div>
              <div className="text-[11px] text-kds-gray-500 mt-1">한국형 디자인 시스템</div>
            </div>
            <div className="p-5 bg-white border border-kds-gray-300 rounded-lg shadow-sm">
              <div className="text-xs text-kds-gray-500 font-medium">벡터 저장소</div>
              <div className="text-base font-bold text-kds-gray-900 mt-1">Qdrant 엔진</div>
              <div className="text-[11px] text-kds-gray-500 mt-1">코사인 유사도 색인</div>
            </div>
            <div className="p-5 bg-white border border-kds-gray-300 rounded-lg shadow-sm">
              <div className="text-xs text-kds-gray-500 font-medium">언어 모델 추론</div>
              <div className="text-base font-bold text-kds-gray-900 mt-1">Ollama / vLLM</div>
              <div className="text-[11px] text-kds-gray-500 mt-1">로컬 오픈소스 엔진</div>
            </div>
          </div>
        </div>
      </section>

      {/* 하단 시작하기 CTA */}
      <section className="py-20 bg-kds-gray-900 text-white text-center px-6 sm:px-8">
        <div className="max-w-2xl mx-auto space-y-6">
          <h2 className="text-2xl sm:text-3xl md:text-4xl font-bold tracking-tight leading-snug">
            지금 사내 문서를 등록하고 직접 질문해 보세요
          </h2>
          <p className="text-sm sm:text-base text-kds-gray-400 leading-relaxed">
            별도의 복잡한 절차 없이 데모 계정으로 바로 로그인하여 사내 문서를 보관하고 질의응답을 시작할 수 있습니다.
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
            <span>투명한 문서 기반 사내 지식 검색 시스템</span>
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
