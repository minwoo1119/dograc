"use client";

import { useState } from "react";
import { useAppStore } from "@/lib/store";
import { X, Check, Zap, Shield, Sparkles, Loader2 } from "lucide-react";

interface ProPricingModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export function ProPricingModal({ isOpen, onClose }: ProPricingModalProps) {
  const { isProSubscriber, setIsProSubscriber, setSelectedModelType } = useAppStore();
  const [isProcessing, setIsProcessing] = useState(false);

  if (!isOpen) return null;

  const handleSubscribe = () => {
    setIsProcessing(true);
    // 모의 결제 처리 시뮬레이션 (1초 대기)
    setTimeout(() => {
      setIsProSubscriber(true);
      setSelectedModelType("server");
      setIsProcessing(false);
      onClose();
    }, 1000);
  };

  const handleCancelSub = () => {
    setIsProSubscriber(false);
    setSelectedModelType("local");
    onClose();
  };

  return (
    <div className="fixed inset-0 bg-black/40 backdrop-blur-[2px] flex items-center justify-center z-50 p-4 animate-in fade-in duration-150">
      <div className="bg-white rounded-lg shadow-dropdown max-w-xl w-full border border-kds-gray-300 flex flex-col overflow-hidden">
        {/* 모달 헤더 */}
        <div className="h-14 px-6 border-b border-kds-gray-200 flex items-center justify-between bg-kds-gray-50 flex-shrink-0">
          <div className="flex items-center space-x-2">
            <span className="bg-kds-blue-600 text-white text-[10px] font-bold px-1.5 py-0.5 rounded uppercase">
              PRO
            </span>
            <h2 className="text-sm font-bold text-kds-gray-900">
              클라우드 서버 고성능 모델 안내
            </h2>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 text-kds-gray-400 hover:text-kds-gray-700 hover:bg-kds-gray-200 rounded transition-colors"
          >
            <X className="w-4 h-4 stroke-[1.75]" />
          </button>
        </div>

        {/* 모달 본문 */}
        <div className="p-6 space-y-6 text-xs text-kds-gray-700">
          <div className="text-center max-w-md mx-auto space-y-1.5">
            <h3 className="text-base font-bold text-kds-gray-900">
              서버 전용 고성능 GPU 모델을 이용해 보세요
            </h3>
            <p className="text-xs text-kds-gray-600 leading-relaxed">
              사용자 컴퓨터의 GPU 사양과 무관하게, 서버 클러스터의 Qwen 2.5 32B 엔터프라이즈 전용 GPU로 빠르고 정밀하게 답변을 생성합니다.
            </p>
          </div>

          {/* 플랜 비교 카드 그리드 */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-2">
            {/* 무료 플랜 */}
            <div className="rounded-lg border border-kds-gray-300 bg-kds-gray-50 p-4 flex flex-col justify-between space-y-4">
              <div>
                <div className="flex items-center justify-between">
                  <span className="font-bold text-kds-gray-900 text-xs">로컬 Ollama 연동</span>
                  <span className="text-kds-green-700 bg-kds-green-100 px-2 py-0.5 rounded text-[11px] font-semibold">
                    평생 무료
                  </span>
                </div>
                <div className="text-xl font-extrabold text-kds-gray-900 mt-2">
                  ₩ 0 <span className="text-xs font-normal text-kds-gray-500">/ 월</span>
                </div>
                <p className="text-[11px] text-kds-gray-500 mt-1">
                  내 컴퓨터의 로컬 GPU를 활용하여 100% 무료로 무제한 질의응답
                </p>

                <ul className="mt-4 space-y-2 text-[11px] text-kds-gray-700">
                  <li className="flex items-center space-x-2">
                    <Check className="w-3.5 h-3.5 text-kds-blue-600 flex-shrink-0" />
                    <span>내 로컬 Ollama 모델 무제한 연결</span>
                  </li>
                  <li className="flex items-center space-x-2">
                    <Check className="w-3.5 h-3.5 text-kds-blue-600 flex-shrink-0" />
                    <span>온프레미스 100% 내부망 보안 격리</span>
                  </li>
                  <li className="flex items-center space-x-2">
                    <Check className="w-3.5 h-3.5 text-kds-blue-600 flex-shrink-0" />
                    <span>무제한 워크스페이스 및 문서 업로드</span>
                  </li>
                </ul>
              </div>

              <button
                onClick={() => {
                  setSelectedModelType("local");
                  onClose();
                }}
                className="w-full h-9 rounded border border-kds-gray-300 bg-white hover:bg-kds-gray-100 text-kds-gray-800 text-xs font-medium transition-colors"
              >
                로컬 모델(무료) 계속 사용
              </button>
            </div>

            {/* 서버 PRO 플랜 */}
            <div className="rounded-lg border-2 border-kds-blue-600 bg-white p-4 flex flex-col justify-between space-y-4 shadow-sm relative">
              <div className="absolute -top-2.5 right-4 bg-kds-blue-600 text-white text-[10px] font-bold px-2 py-0.5 rounded-full shadow-xs">
                추천 플랜
              </div>

              <div>
                <div className="flex items-center justify-between">
                  <span className="font-bold text-kds-gray-900 text-xs">서버 전용 PRO GPU</span>
                  <span className="text-kds-blue-700 bg-kds-blue-50 border border-kds-blue-200 px-2 py-0.5 rounded text-[11px] font-semibold">
                    고성능 32B
                  </span>
                </div>
                <div className="text-xl font-extrabold text-kds-gray-900 mt-2">
                  ₩ 19,000 <span className="text-xs font-normal text-kds-gray-500">/ 월</span>
                </div>
                <p className="text-[11px] text-kds-gray-500 mt-1">
                  사내 전용 GPU 클러스터로 복잡한 질문도 1초 만에 추론
                </p>

                <ul className="mt-4 space-y-2 text-[11px] text-kds-gray-700">
                  <li className="flex items-center space-x-2">
                    <Check className="w-3.5 h-3.5 text-kds-blue-600 flex-shrink-0" />
                    <span>Qwen 2.5 32B 엔터프라이즈 모델</span>
                  </li>
                  <li className="flex items-center space-x-2">
                    <Check className="w-3.5 h-3.5 text-kds-blue-600 flex-shrink-0" />
                    <span>PC 발열/사양 부담 없는 초고속 클라우드</span>
                  </li>
                  <li className="flex items-center space-x-2">
                    <Check className="w-3.5 h-3.5 text-kds-blue-600 flex-shrink-0" />
                    <span>우선 처리 큐 및 무제한 긴 문맥 추론</span>
                  </li>
                </ul>
              </div>

              {isProSubscriber ? (
                <div className="space-y-2">
                  <div className="p-2 bg-kds-green-100 text-kds-green-800 rounded text-center text-xs font-semibold">
                    현재 PRO 플랜 구독 중입니다
                  </div>
                  <button
                    onClick={handleCancelSub}
                    className="w-full h-8 text-[11px] text-kds-gray-500 hover:text-kds-red-500 transition-colors"
                  >
                    구독 해지하기
                  </button>
                </div>
              ) : (
                <button
                  onClick={handleSubscribe}
                  disabled={isProcessing}
                  className="w-full h-9 rounded bg-kds-blue-600 hover:bg-kds-blue-800 text-white text-xs font-semibold transition-colors flex items-center justify-center space-x-1.5 shadow-xs disabled:opacity-50"
                >
                  {isProcessing ? (
                    <>
                      <Loader2 className="w-3.5 h-3.5 animate-spin" />
                      <span>결제 처리 중...</span>
                    </>
                  ) : (
                    <span>7일 무료 체험 후 구독하기</span>
                  )}
                </button>
              )}
            </div>
          </div>
        </div>

        {/* 모달 푸터 */}
        <div className="h-12 px-6 border-t border-kds-gray-200 bg-kds-gray-50 flex items-center justify-between flex-shrink-0 text-[11px] text-kds-gray-500">
          <span>구독은 언제든지 즉시 취소할 수 있으며 추가 비용이 발생하지 않습니다.</span>
          <button
            onClick={onClose}
            className="hover:text-kds-gray-800 transition-colors font-medium"
          >
            닫기
          </button>
        </div>
      </div>
    </div>
  );
}
